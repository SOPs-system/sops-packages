namespace Sops.Models.Common;

using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Machine-readable standardized error contract.
/// </summary>
public sealed record ErrorContract
{
    /// <summary>
    /// Gets the stable machine-readable error code (e.g. "ERR_NOT_FOUND", "ERR_PRICE_VIOLATION").
    /// </summary>
    [JsonPropertyName("code")]
    public string Code { get; init; } = string.Empty;

    /// <summary>
    /// Gets the human-readable description of the error.
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; init; } = string.Empty;

    /// <summary>
    /// Gets the optional field or resource name associated with the error.
    /// </summary>
    [JsonPropertyName("target")]
    public string? Target { get; init; }

    /// <summary>
    /// Gets optional structured diagnostic details.
    /// </summary>
    [JsonPropertyName("details")]
    public IReadOnlyDictionary<string, string>? Details { get; init; }

    /// <summary>
    /// Creates a simple error contract.
    /// </summary>
    public static ErrorContract Create(string code, string message, string? target = null) =>
        new() { Code = code, Message = message, Target = target };
}
