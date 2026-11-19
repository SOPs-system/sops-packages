using System;
using System.Collections.Generic;

namespace Sops.DataLayer.Models.Requests
{
    /// <summary>
    /// Represents a typed request payload directed at a logical endpoint.
    /// </summary>
    public sealed class DataRequest
    {
        public string EndpointId { get; }
        public object? Body { get; }
        public RequestOptions Options { get; }
        public RequestContext Context { get; set; }

        public DataRequest(
            string endpointId,
            object? body = null,
            RequestOptions? options = null,
            RequestContext? context = null)
        {
            if (string.IsNullOrWhiteSpace(endpointId)) throw new ArgumentException("Endpoint ID cannot be empty.", nameof(endpointId));

            EndpointId = endpointId.Trim().ToLowerInvariant();
            Body = body;
            Options = options ?? RequestOptions.Default;
            Context = context ?? new RequestContext(correlationId: Options.CorrelationId, idempotencyKey: Options.IdempotencyKey);
        }

        public DataRequest(
            string endpointId,
            object? body,
            IDictionary<string, string>? queryParameters,
            RequestOptions? options = null,
            RequestContext? context = null)
            : this(endpointId, body, options ?? new RequestOptions { QueryParameters = queryParameters }, context)
        {
            if (queryParameters != null && Options.QueryParameters == null)
            {
                Options.QueryParameters = queryParameters;
            }
        }

        public static DataRequest Create(string endpointId, object? body = null, RequestOptions? options = null) =>
            new DataRequest(endpointId, body, options);

        public static DataRequest Get(string endpointId, IDictionary<string, string>? queryParameters = null, RequestOptions? options = null)
        {
            var opt = options ?? new RequestOptions();
            opt.QueryParameters = queryParameters;
            return new DataRequest(endpointId, null, opt);
        }
    }
}
