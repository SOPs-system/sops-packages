using System;

namespace Sops.DataOperations.Abstractions.Models
{
    /// <summary>
    /// Universal result envelope for long-running data operations in the SOPs ecosystem.
    /// </summary>
    /// <typeparam name="T">Type of data payload returned upon successful completion.</typeparam>
    public sealed class DataOperationResult<T>
    {
        public Guid OperationId { get; set; } = Guid.NewGuid();
        public bool Success { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTimeOffset StartTimeUtc { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset? CompletionTimeUtc { get; set; }
        public string? ErrorCode { get; set; }
        public string? ErrorMessage { get; set; }
        public T? Data { get; set; }
        public string? CorrelationId { get; set; }
        public bool IsRetryEligible { get; set; }

        public static DataOperationResult<T> Succeeded(T data, string status = "Completed", string? correlationId = null)
        {
            return new DataOperationResult<T>
            {
                Success = true,
                Status = status,
                Data = data,
                CompletionTimeUtc = DateTimeOffset.UtcNow,
                CorrelationId = correlationId
            };
        }

        public static DataOperationResult<T> Failed(string errorCode, string errorMessage, bool isRetryEligible = false, string? correlationId = null)
        {
            return new DataOperationResult<T>
            {
                Success = false,
                Status = "Failed",
                ErrorCode = errorCode,
                ErrorMessage = errorMessage,
                IsRetryEligible = isRetryEligible,
                CompletionTimeUtc = DateTimeOffset.UtcNow,
                CorrelationId = correlationId
            };
        }
    }
}
