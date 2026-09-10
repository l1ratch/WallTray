using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BingWallTray.App.Models;
using BingWallTray.App.Services;
using Xunit;

namespace BingWallTray.Tests
{
    public class DownloadServiceTests : IDisposable
    {
        private readonly string _testFolder;

        public DownloadServiceTests()
        {
            _testFolder = Path.Combine(Path.GetTempPath(), "BingWallTrayDownloadTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_testFolder);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_testFolder)) Directory.Delete(_testFolder, true); } catch { }
        }

        private class SequenceHandler : HttpMessageHandler
        {
            private readonly HttpResponseMessage[] _responses;
            public int Requests { get; private set; }

            public SequenceHandler(params HttpResponseMessage[] responses)
            {
                _responses = responses;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                int index = Math.Min(Requests, _responses.Length - 1);
                Requests++;
                return Task.FromResult(_responses[index]);
            }
        }

        private static HttpResponseMessage ErrorResponse()
            => new(HttpStatusCode.InternalServerError) { Content = new StringContent("boom") };

        private static HttpResponseMessage JpegResponse()
        {
            byte[] bytes = new byte[20 * 1024]; // > минимума 10 КБ
            bytes[0] = 0xFF; bytes[1] = 0xD8; bytes[2] = 0xFF; // JPEG magic
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        }

        private static BingImage TestImage() => new()
        {
            Url = "https://example.test/wall.jpg",
            Title = "Test",
            Market = "ru-RU",
            StartDate = "20260911"
        };

        [Fact]
        public async Task Retry_SucceedsAfterTransientFailures()
        {
            using var handler = new SequenceHandler(ErrorResponse(), ErrorResponse(), JpegResponse());
            using var client = new HttpClient(handler);

            string path = await DownloadService.DownloadImageWithRetryAsync(client, TestImage(), _testFolder, new MockLoggingService());

            Assert.True(File.Exists(path));
            Assert.Equal(3, handler.Requests);
            Assert.EndsWith(".jpg", path);
        }

        [Fact]
        public async Task Retry_ThrowsAfterAllAttemptsFail()
        {
            using var handler = new SequenceHandler(ErrorResponse());
            using var client = new HttpClient(handler);

            await Assert.ThrowsAsync<HttpRequestException>(() =>
                DownloadService.DownloadImageWithRetryAsync(client, TestImage(), _testFolder, new MockLoggingService()));

            Assert.Equal(3, handler.Requests);
            Assert.Empty(Directory.GetFiles(_testFolder, "*.tmp"));
        }

        [Fact]
        public async Task Retry_SucceedsOnFirstAttempt_WhenServerHealthy()
        {
            using var handler = new SequenceHandler(JpegResponse());
            using var client = new HttpClient(handler);

            string path = await DownloadService.DownloadImageWithRetryAsync(client, TestImage(), _testFolder, new MockLoggingService());

            Assert.True(File.Exists(path));
            Assert.Equal(1, handler.Requests);
        }
    }
}
