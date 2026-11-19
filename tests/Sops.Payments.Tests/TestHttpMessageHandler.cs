using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Sops.Payments.Tests
{
    public class TestHttpMessageHandler : HttpMessageHandler
    {
        private readonly List<HttpRequestMessage> _sentRequests = new List<HttpRequestMessage>();
        private readonly List<string> _sentRequestBodies = new List<string>();
        private readonly Queue<Func<HttpRequestMessage, Task<HttpResponseMessage>>> _responses =
            new Queue<Func<HttpRequestMessage, Task<HttpResponseMessage>>>();

        public IReadOnlyList<HttpRequestMessage> SentRequests => _sentRequests;
        public IReadOnlyList<string> SentRequestBodies => _sentRequestBodies;

        public void EnqueueResponse(HttpStatusCode statusCode, string jsonContent)
        {
            _responses.Enqueue(req =>
            {
                var response = new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json")
                };
                return Task.FromResult(response);
            });
        }

        public void EnqueueException(Exception exception)
        {
            _responses.Enqueue(req => throw exception);
        }

        public void EnqueueHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _responses.Enqueue(req => Task.FromResult(handler(req)));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            _sentRequests.Add(request);

            if (request.Content != null)
            {
                string body = await request.Content.ReadAsStringAsync();
                _sentRequestBodies.Add(body);
            }
            else
            {
                _sentRequestBodies.Add(string.Empty);
            }

            if (_responses.Count > 0)
            {
                var handler = _responses.Dequeue();
                return await handler(request);
            }

            return new HttpResponseMessage(HttpStatusCode.NotFound)
            {
                Content = new StringContent("{}", System.Text.Encoding.UTF8, "application/json")
            };
        }
    }
}
