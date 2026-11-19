namespace Sops.Models.Notifications;

using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

/// <summary>
/// Delivery channel for customer or staff notifications.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NotificationChannel
{
    /// <summary>Electronic mail (SMTP / MailKit).</summary>
    Email,
    /// <summary>WhatsApp Business Cloud messaging.</summary>
    WhatsApp,
    /// <summary>SMS text message.</summary>
    SMS,
    /// <summary>Mobile push notification.</summary>
    Push,
    /// <summary>In-app workstation banner alert.</summary>
    InApp
}

/// <summary>
/// Delivery status of an outgoing notification.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum NotificationDeliveryStatus
{
    /// <summary>Enqueued for dispatch.</summary>
    Queued,
    /// <summary>Sent to provider gateway.</summary>
    Sent,
    /// <summary>Delivered to recipient handset/inbox.</summary>
    Delivered,
    /// <summary>Opened or read by recipient.</summary>
    Read,
    /// <summary>Failed to deliver.</summary>
    Failed
}

/// <summary>
/// Universal notification dispatch request contract.
/// Free from provider-specific payload secrets.
/// </summary>
public sealed record NotificationRequest
{
    /// <summary>
    /// Gets the unique notification ID.
    /// </summary>
    [JsonPropertyName("notification_id")]
    public Guid NotificationId { get; init; } = Guid.NewGuid();

    /// <summary>
    /// Gets the target delivery channel.
    /// </summary>
    [JsonPropertyName("channel")]
    public NotificationChannel Channel { get; init; } = NotificationChannel.WhatsApp;

    /// <summary>
    /// Gets the recipient address or phone number.
    /// </summary>
    [JsonPropertyName("recipient")]
    public string Recipient { get; init; } = string.Empty;

    /// <summary>
    /// Gets the notification subject or title.
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; init; } = string.Empty;

    /// <summary>
    /// Gets the notification message body.
    /// </summary>
    [JsonPropertyName("body")]
    public string Body { get; init; } = string.Empty;

    /// <summary>
    /// Gets an optional template name for templated messages.
    /// </summary>
    [JsonPropertyName("template_name")]
    public string? TemplateName { get; init; }

    /// <summary>
    /// Gets template substitution parameters.
    /// </summary>
    [JsonPropertyName("template_parameters")]
    public IReadOnlyDictionary<string, string>? TemplateParameters { get; init; }

    /// <summary>
    /// Gets the creation timestamp.
    /// </summary>
    [JsonPropertyName("created_at_utc")]
    public DateTimeOffset CreatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
}
