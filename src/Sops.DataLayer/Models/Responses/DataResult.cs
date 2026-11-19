using System;

namespace Sops.DataLayer.Models.Responses
{
    /// <summary>
    /// Base typed outcome for DataLayer operations.
    /// </summary>
    public class DataResult
    {
        public bool IsSuccess => Outcome == DataOutcome.Success;
        public DataOutcome Outcome { get; }
        public DataError? Error { get; }
        public ResponseMetadata? Metadata { get; }

        protected DataResult(DataOutcome outcome, DataError? error = null, ResponseMetadata? metadata = null)
        {
            Outcome = outcome;
            Error = error;
            Metadata = metadata;
        }

        public static DataResult Success(ResponseMetadata? metadata = null) =>
            new DataResult(DataOutcome.Success, metadata: metadata);

        public static DataResult Failure(DataOutcome outcome, DataError error, ResponseMetadata? metadata = null) =>
            new DataResult(outcome, error ?? throw new ArgumentNullException(nameof(error)), metadata);

        public static DataResult Failure(DataError error, ResponseMetadata? metadata = null) =>
            new DataResult(DataOutcome.Failure, error ?? throw new ArgumentNullException(nameof(error)), metadata);

        public static DataResult Cancelled(string? correlationId = null) =>
            new DataResult(DataOutcome.Cancelled, DataError.Cancelled("Operation was cancelled.", correlationId));

        public static DataResult Timeout(string? correlationId = null) =>
            new DataResult(DataOutcome.Timeout, DataError.Timeout("Operation timed out.", correlationId));

        public static DataResult Unauthorized(string message = "Unauthorized", string? correlationId = null) =>
            new DataResult(DataOutcome.Unauthorized, DataError.Unauthorized(message, correlationId));

        public static DataResult Forbidden(string message = "Forbidden", string? correlationId = null) =>
            new DataResult(DataOutcome.Forbidden, DataError.Forbidden(message, correlationId));

        public static DataResult NotFound(string message = "Not Found", string? correlationId = null) =>
            new DataResult(DataOutcome.NotFound, DataError.NotFound(message, correlationId));

        public static DataResult Conflict(string message = "Conflict", string? correlationId = null) =>
            new DataResult(DataOutcome.Conflict, DataError.Conflict(message, correlationId));

        public static DataResult RateLimited(string message = "Too many requests", string? correlationId = null) =>
            new DataResult(DataOutcome.RateLimited, DataError.RateLimited(message, correlationId));

        public static DataResult ServerUnavailable(string message = "Server unavailable", string? correlationId = null) =>
            new DataResult(DataOutcome.ServerUnavailable, DataError.Unavailable(message, correlationId));

        public static DataResult NetworkUnavailable(string message = "Network unavailable", string? correlationId = null) =>
            new DataResult(DataOutcome.NetworkUnavailable, DataError.NetworkUnavailable(message, correlationId));

        public static DataResult UnknownOutcome(string message, string? correlationId = null) =>
            new DataResult(DataOutcome.UnknownOutcome, DataError.UnknownOutcome(message, correlationId));
    }

    /// <summary>
    /// Generic strongly typed response wrapper containing result payload, outcome status, errors, and metadata.
    /// </summary>
    /// <typeparam name="T">Payload data type.</typeparam>
    public sealed class DataResult<T> : DataResult
    {
        public T? Data { get; }

        private DataResult(T? data, DataOutcome outcome, DataError? error = null, ResponseMetadata? metadata = null)
            : base(outcome, error, metadata)
        {
            Data = data;
        }

        public static DataResult<T> Success(T data, ResponseMetadata? metadata = null) =>
            new DataResult<T>(data, DataOutcome.Success, metadata: metadata);

        public static DataResult<T> Empty(ResponseMetadata? metadata = null) =>
            new DataResult<T>(default, DataOutcome.Success, metadata: metadata);

        public new static DataResult<T> Failure(DataOutcome outcome, DataError error, ResponseMetadata? metadata = null) =>
            new DataResult<T>(default, outcome, error ?? throw new ArgumentNullException(nameof(error)), metadata);

        public new static DataResult<T> Failure(DataError error, ResponseMetadata? metadata = null) =>
            new DataResult<T>(default, DataOutcome.Failure, error ?? throw new ArgumentNullException(nameof(error)), metadata);

        public new static DataResult<T> Cancelled(string? correlationId = null) =>
            new DataResult<T>(default, DataOutcome.Cancelled, DataError.Cancelled("Operation was cancelled.", correlationId));

        public new static DataResult<T> Timeout(string? correlationId = null) =>
            new DataResult<T>(default, DataOutcome.Timeout, DataError.Timeout("Operation timed out.", correlationId));

        public new static DataResult<T> Unauthorized(string message = "Unauthorized", string? correlationId = null) =>
            new DataResult<T>(default, DataOutcome.Unauthorized, DataError.Unauthorized(message, correlationId));

        public new static DataResult<T> Forbidden(string message = "Forbidden", string? correlationId = null) =>
            new DataResult<T>(default, DataOutcome.Forbidden, DataError.Forbidden(message, correlationId));

        public new static DataResult<T> NotFound(string message = "Not Found", string? correlationId = null) =>
            new DataResult<T>(default, DataOutcome.NotFound, DataError.NotFound(message, correlationId));

        public new static DataResult<T> Conflict(string message = "Conflict", string? correlationId = null) =>
            new DataResult<T>(default, DataOutcome.Conflict, DataError.Conflict(message, correlationId));

        public new static DataResult<T> RateLimited(string message = "Too many requests", string? correlationId = null) =>
            new DataResult<T>(default, DataOutcome.RateLimited, DataError.RateLimited(message, correlationId));

        public new static DataResult<T> ServerUnavailable(string message = "Server unavailable", string? correlationId = null) =>
            new DataResult<T>(default, DataOutcome.ServerUnavailable, DataError.Unavailable(message, correlationId));

        public new static DataResult<T> NetworkUnavailable(string message = "Network unavailable", string? correlationId = null) =>
            new DataResult<T>(default, DataOutcome.NetworkUnavailable, DataError.NetworkUnavailable(message, correlationId));

        public new static DataResult<T> UnknownOutcome(string message, string? correlationId = null) =>
            new DataResult<T>(default, DataOutcome.UnknownOutcome, DataError.UnknownOutcome(message, correlationId));

        /// <summary>Implicit conversion to boolean checking IsSuccess.</summary>
        public static implicit operator bool(DataResult<T> result) => result != null && result.IsSuccess;
    }
}
