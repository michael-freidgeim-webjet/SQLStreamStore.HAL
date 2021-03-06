﻿namespace SqlStreamStore.HAL.Tests
{
    using System;
    using System.Net;
    using System.Net.Http.Headers;
    using System.Threading.Tasks;
    using Shouldly;
    using Xunit;

    public class StreamMessageTests : IDisposable
    {
        public StreamMessageTests()
        {
            _fixture = new SqlStreamStoreHalMiddlewareFixture();
        }

        public void Dispose() => _fixture.Dispose();
        private readonly SqlStreamStoreHalMiddlewareFixture _fixture;
        private const string HeadOfStream = "../a-stream?d=b&m=20&p=-1&e=0";

        [Fact]
        public async Task read_single_message_stream()
        {
            var writeResult = await _fixture.WriteNMessages("a-stream", 1);

            using(var response = await _fixture.HttpClient.GetAsync("/streams/a-stream/0"))
            {
                response.StatusCode.ShouldBe(HttpStatusCode.OK);
                response.Headers.ETag.ShouldBe(new EntityTagHeaderValue($@"""{writeResult.CurrentVersion}"""));

                var resource = await response.AsHal();

                resource.Links.Keys.ShouldBe(new[]
                {
                    Constants.Relations.Self,
                    Constants.Relations.First,
                    Constants.Relations.Next,
                    Constants.Relations.Last,
                    Constants.Relations.Feed,
                    Constants.Relations.Message,
                    Constants.Relations.Find
                });

                resource.ShouldLink(Constants.Relations.Self, "0");
                resource.ShouldLink(Constants.Relations.First, "0");
                resource.ShouldLink(Constants.Relations.Next, "1");
                resource.ShouldLink(Constants.Relations.Last, "-1");
                resource.ShouldLink(Constants.Relations.Feed, HeadOfStream);
                resource.ShouldLink(Constants.Relations.Message, "0");
                resource.ShouldLink(Constants.Relations.Find, "../../streams/{streamId}", "Find a Stream");
            }
        }

        [Fact]
        public async Task read_single_message_does_not_exist_stream()
        {
            using(var response = await _fixture.HttpClient.GetAsync("/streams/a-stream/0"))
            {
                response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
                response.Headers.ETag.ShouldBeNull();

                var resource = await response.AsHal();

                resource.Links.Keys.ShouldBe(new[]
                {
                    Constants.Relations.Self,
                    Constants.Relations.First,
                    Constants.Relations.Last,
                    Constants.Relations.Feed,
                    Constants.Relations.Message,
                    Constants.Relations.Find
                });

                resource.ShouldLink(Constants.Relations.Self, "0");
                resource.ShouldLink(Constants.Relations.First, "0");
                resource.ShouldLink(Constants.Relations.Last, "-1");
                resource.ShouldLink(Constants.Relations.Feed, HeadOfStream);
                resource.ShouldLink(Constants.Relations.Message, "0");
                resource.ShouldLink(Constants.Relations.Find, "../../streams/{streamId}", "Find a Stream");
            }
        }

        [Fact]
        public async Task delete_single_message_by_version()
        {
            var writeResult = await _fixture.WriteNMessages("a-stream", 1);

            using(var response = await _fixture.HttpClient.DeleteAsync("/streams/a-stream/0"))
            {
                response.StatusCode.ShouldBe(HttpStatusCode.OK);
            }

            using(var response = await _fixture.HttpClient.GetAsync("/streams/a-stream/0"))
            {
                response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
                response.Headers.ETag.ShouldBeNull();
            }
        }
    }
}