using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using BingWallTray.App.Services;
using Xunit;

namespace BingWallTray.Tests
{
    public class BingServiceTests
    {
        private class MockHttpMessageHandler : HttpMessageHandler
        {
            private readonly string _responseJson;

            public MockHttpMessageHandler(string responseJson)
            {
                _responseJson = responseJson;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                var response = new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_responseJson)
                };
                return Task.FromResult(response);
            }
        }

        [Fact]
        public async Task GetLatestImagesAsync_ParsesJsonAndConvertsRelativeUrls()
        {
            // Arrange
            string json = @"
{
  ""images"": [
    {
      ""startdate"": ""20260709"",
      ""url"": ""/th?id=OHR.TestImage_RU-RU.jpg"",
      ""urlbase"": ""/th?id=OHR.TestImage_RU-RU"",
      ""copyright"": ""Test copyright text"",
      ""copyrightlink"": ""/search?q=test"",
      ""title"": ""Test Title"",
      ""quiz"": ""/quiz?id=test""
    }
  ]
}";
            var handler = new MockHttpMessageHandler(json);
            var client = new HttpClient(handler);
            var logger = new MockLoggingService();
            var service = new BingService(logger, client);

            // Act
            var images = await service.GetLatestImagesAsync("ru-RU", 1, true);

            // Assert
            Assert.Single(images);
            var image = images[0];
            Assert.Equal("20260709", image.StartDate);
            Assert.Equal("ru-RU", image.Market);
            Assert.Equal("Test Title", image.Title);
            Assert.Equal("https://www.bing.com/th?id=OHR.TestImage_RU-RU.jpg", image.Url);
            Assert.Equal("https://www.bing.com/th?id=OHR.TestImage_RU-RU", image.UrlBase);
            Assert.Equal("https://www.bing.com/search?q=test", image.CopyrightLink);
            Assert.Equal("https://www.bing.com/quiz?id=test", image.Quiz);
        }

        [Fact]
        public async Task GetHistoricalArchiveImagesAsync_ParsesReadmeTableSuccessfully()
        {
            // Arrange
            string readmeContent = @"
# Bing Wallpaper Archive

| Image | Date | Download |
| --- | --- | --- |
| ![A beautiful forest (© John Doe)](/th?id=OHR.Forest_EN-US.jpg) 2026-07-09 [download 4k](/th?id=OHR.Forest_EN-US_4K.jpg) | ![A cute cat (© Jane Smith)](https://www.bing.com/th?id=OHR.Cat.jpg) 2026-07-08 [download 4k](https://www.bing.com/th?id=OHR.Cat_4K.jpg) |
";
            var handler = new MockHttpMessageHandler(readmeContent);
            var client = new HttpClient(handler);
            var logger = new MockLoggingService();
            var service = new BingService(logger, client);

            // Act
            var images = await service.GetHistoricalArchiveImagesAsync("en-US", true);

            // Assert
            Assert.Equal(2, images.Count);

            var first = images[0];
            Assert.Equal("20260709", first.StartDate);
            Assert.Equal("A beautiful forest", first.Title);
            Assert.Equal("© John Doe", first.Copyright);
            Assert.Equal("https://www.bing.com/th?id=OHR.Forest_EN-US_4K.jpg", first.Url);
            Assert.Equal("en-US", first.Market);

            var second = images[1];
            Assert.Equal("20260708", second.StartDate);
            Assert.Equal("A cute cat", second.Title);
            Assert.Equal("© Jane Smith", second.Copyright);
            Assert.Equal("https://www.bing.com/th?id=OHR.Cat_4K.jpg", second.Url);
        }
    }
}
