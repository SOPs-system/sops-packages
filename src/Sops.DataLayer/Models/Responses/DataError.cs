using System;
using System.Collections.Generic;

namespace Sops.DataLayer.Models.Responses
{
    /// <summary>
    /// Structured, safe error representation that redacts sensitive internals and traces via correlation IDs.
    /// </summary>
    public sealed class DataError
    {
        public string Code { get; }
        public string Message { get; }
        public IReadOnlyDictionary<string, string[]>? ValidationErrors { get; }
        public string? CorrelationId { get; }
        public int? StatusCode { get; }

        /// <summary>True if this error was caused by a conflict or idempotency key replay collision.</summary>
        public bool IsIdempotencyConflict =>
            string.Equals(Code, "conflict", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(Code, "idempotency_conflict", StringComparison.OrdinalIgnoreCase);

        public DataError(
            string code,
            string message,
            IReadOnlyDictionary<string, string[]>? validationErrors = null,
            string? correlationId = null,
            int? statusCode = null)
        {
            Code = !string.IsNullOrWhiteSpace(code) ? code : "general_error";
            Message = !string.IsNullOrWhiteSpace(message) ? message : "An unexpected error occurred.";
            ValidationErrors = validationErrors;
            CorrelationId = correlationId;
            StatusCode = statusCode;
        }

        public static DataError Create(string code, string message, string? correlationId = null, int? statusCode = null) =>
            new DataError(code, message, correlationId: correlationId, statusCode: statusCode);

        public static DataError Validation(string message, IReadOnlyDictionary<string, string[]> errors, string? correlationId = null) =>
            new DataError("validation_failed", message, errors, correlationId, 400);

        public static DataError Unauthorized(string message = "Authentication required or session expired.", string? correlationId = null) =>
            new DataError("unauthorized", message, correlationId: correlationId, statusCode: 401);

        public static DataError Forbidden(string message = "You do not have permission to perform this operation.", string? correlationId = null) =>
            new DataError("forbidden", message, correlationId: correlationId, statusCode: 403);

        public static DataError NotFound(string message = "Requested resource was not found.", string? correlationId = null) =>
            new DataError("not_found", message, correlationId: correlationId, statusCode: 404);

        public static DataError Conflict(string message = "Operation conflicted with existing state.", string? correlationId = null) =>
            new DataError("conflict", message, correlationId: correlationId, statusCode: 409);

        public static DataError RateLimited(string message = "Request rate limit exceeded. Please wait and retry.", string? correlationId = null) =>
            new DataError("rate_limited", message, correlationId: correlationId, statusCode: 429);

        public static DataError Timeout(string message = "Operation timed out.", string? correlationId = null) =>
            new DataError("timeout", message, correlationId: correlationId, statusCode: 408);

        public static DataError Cancelled(string message = "Operation was cancelled.", string? correlationId = null) =>
            new DataError("cancelled", message, correlationId: correlationId, statusCode: 499);

        public static DataError Unavailable(string message = "Server or service is currently unavailable.", string? correlationId = null) =>
            new DataError("service_unavailable", message, correlationId: correlationId, statusCode: 503);

        public static DataError NetworkUnavailable(string message = "Network connection is unavailable.", string? correlationId = null) =>
            new DataError("network_unavailable", message, correlationId: correlationId);

        public static DataError UnknownOutcome(string message, string? correlationId = null) =>
            new DataError("unknown_outcome", message, correlationId: correlationId);

        public override string ToString() => $"[{Code}] {Message} (CorrelationId: {CorrelationId ?? "N/A"})";
    }
}
