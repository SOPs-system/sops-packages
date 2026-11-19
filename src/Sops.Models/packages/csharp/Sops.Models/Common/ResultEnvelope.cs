namespace Sops.Models.Common;

using System;
using System.Text.Json.Serialization;

/// <summary>
/// Universal outcome wrapper for API and communication endpoints.
/// </summary>
/// <typeparam name="T">Type of the successful response payload.</typeparam>
public sealed record ResultEnvelope<T>
{
    /// <summary>
    /// Gets whether the operation completed successfully.
    /// </summary>
    [JsonPropertyName("is_success")]
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Gets the distributed tracing or correlation ID.
    /// </summary>
    [JsonPropertyName("correlation_id")]
    public string CorrelationId { get; init; } = Guid.NewGuid().ToString("N");

    /// <summary>
    /// Gets the payload on success.
    /// </summary>
    [JsonPropertyName("data")]
    public T? Data { get; init; }

    /// <summary>
    /// Gets the structured error contract on failure.
    /// </summary>
    [JsonPropertyName("error")]
    public ErrorContract? Error { get; init; }

    /// <summary>
    /// Creates a successful outcome envelope.
    /// </summary>
    public static ResultEnvelope<T> Success(T data, string? correlationId = null) => new()
    {
        IsSuccess = true,
        Data = data,
        CorrelationId = correlationId ?? Guid.NewGuid().ToString("N")
    };

    /// <summary>
    /// Creates a failed outcome envelope with an error contract.
    /// </summary>
    public static ResultEnvelope<T> Failure(ErrorContract error, string? correlationId = null) => new()
    {
        IsSuccess = false,
        Error = error,
        CorrelationId = correlationId ?? Guid.NewGuid().ToString("N")
    };

    /// <summary>
    /// Creates a failed outcome envelope from code and message.
    /// </summary>
    public static ResultEnvelope<T> Failure(string code, string message, string? target = null, string? correlationId = null) =>
        Failure(ErrorContract.Create(code, message, target), correlationId);
}
