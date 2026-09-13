using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BingWallTray.App.Utils;
using Xunit;

namespace BingWallTray.Tests
{
    public class ThumbnailCacheTests : IDisposable
    {
        private readonly string _cacheDir;

        public ThumbnailCacheTests()
        {
            _cacheDir = Path.Combine(Path.GetTempPath(), "BingWallTrayThumbCacheTests_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_cacheDir);
        }

        public void Dispose()
        {
            try { if (Directory.Exists(_cacheDir)) Directory.Delete(_cacheDir, true); } catch { }
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
            byte[] bytes = new byte[4 * 1024]; // > минимума 1 КБ
            bytes[0] = 0xFF; bytes[1] = 0xD8; bytes[2] = 0xFF; // JPEG magic
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) };
        }

        [Fact]
        public async Task GetOrCreate_SucceedsAfterTransientFailures()
        {
            using var handler = new SequenceHandler(ErrorResponse(), ErrorResponse(), JpegResponse());
            using var client = new HttpClient(handler);

            string? path = await ThumbnailCache.GetOrCreateAsync(client, "https://example.test/t.jpg", _cacheDir, new MockLoggingService());

            Assert.NotNull(path);
            Assert.True(File.Exists(path));
            Assert.Equal(3, handler.Requests);
        }

        [Fact]
        public async Task GetOrCreate_UsesCacheWithoutNetwork()
        {
            string url = "https://example.test/cached.jpg";
            string cachedPath = ThumbnailCache.GetCachePath(url, _cacheDir);
            File.WriteAllBytes(cachedPath, new byte[] { 0xFF, 0xD8, 0xFF, 0x00 });

            using var handler = new SequenceHandler(ErrorResponse()); // любая сеть = провал теста
            using var client = new HttpClient(handler);

            string? path = await ThumbnailCache.GetOrCreateAsync(client, url, _cacheDir, new MockLoggingService());

            Assert.Equal(cachedPath, path);
            Assert.Equal(0, handler.Requests);
        }

        [Fact]
        public async Task GetOrCreate_ReturnsNullAfterAllAttemptsFail()
        {
            using var handler = new SequenceHandler(ErrorResponse());
            using var client = new HttpClient(handler);

            string? path = await ThumbnailCache.GetOrCreateAsync(client, "https://example.test/bad.jpg", _cacheDir, new MockLoggingService());

            Assert.Null(path);
            Assert.Equal(3, handler.Requests);
            Assert.Empty(Directory.GetFiles(_cacheDir, "*.jpg"));
        }

        [Fact]
        public async Task GetOrCreate_RejectsNonImageData()
        {
            using var handler = new SequenceHandler(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(new byte[4 * 1024]) });
            using var client = new HttpClient(handler);

            string? path = await ThumbnailCache.GetOrCreateAsync(client, "https://example.test/not-an-image.jpg", _cacheDir, new MockLoggingService());

            Assert.Null(path);
            Assert.Equal(3, handler.Requests);
            Assert.Empty(Directory.GetFiles(_cacheDir, "*.jpg"));
        }

        [Fact]
        public async Task GetOrCreate_IgnoresLocalPaths()
        {
            string? path = await ThumbnailCache.GetOrCreateAsync(new HttpClient(), @"C:\Wallpapers\local.jpg", _cacheDir, new MockLoggingService());
            Assert.Null(path);
        }

        private class AnyJpegHandler : HttpMessageHandler
        {
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                byte[] bytes = new byte[4 * 1024];
                bytes[0] = 0xFF; bytes[1] = 0xD8; bytes[2] = 0xFF; // JPEG magic
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(bytes) });
            }
        }

        [Fact]
        public async Task PrefetchAsync_SwapsThumbsThenPreviewsToCachedPaths()
        {
            var images = new List<BingWallTray.App.Models.BingImage>();
            for (int i = 0; i < 5; i++)
            {
                images.Add(new BingWallTray.App.Models.BingImage
                {
                    Url = $"https://example.test/img{i}.jpg",
                    Title = $"Test {i}",
                    Market = "ru-RU",
                    StartDate = "20260911"
                });
            }

            using var client = new HttpClient(new AnyJpegHandler());
            await ThumbnailCache.PrefetchAsync(client, _cacheDir, images, new MockLoggingService());

            foreach (var img in images)
            {
                Assert.StartsWith(_cacheDir, img.ThumbnailUrl, StringComparison.OrdinalIgnoreCase);
                Assert.StartsWith(_cacheDir, img.PreviewUrl, StringComparison.OrdinalIgnoreCase);
                Assert.True(File.Exists(img.ThumbnailUrl), $"thumb file missing: {img.ThumbnailUrl}");
                Assert.True(File.Exists(img.PreviewUrl), $"preview file missing: {img.PreviewUrl}");
            }

            // Второй проход — всё из дискового кэша, сеть не используется и файлов не прибавляется
            int filesBefore = Directory.GetFiles(_cacheDir, "*.jpg").Length;
            using var client2 = new HttpClient(new SequenceHandler(ErrorResponse())); // любая сеть = провал теста
            await ThumbnailCache.PrefetchAsync(client2, _cacheDir, images, new MockLoggingService());
            Assert.Equal(filesBefore, Directory.GetFiles(_cacheDir, "*.jpg").Length);
        }
    }
}
