# Sops.WhatsApp

`Sops.WhatsApp` is a production-quality, lightweight .NET library integrating Meta's official WhatsApp Business Platform Cloud API for SOPs applications and enterprise services. It supports text messaging, pre-approved templates (including authentication and OTP codes without in-transport generation), media messages, direct media uploads and metadata inspection, and cryptographic webhook verification and processing.

## Features

- **Meta Cloud API Native**: Built according to Meta's official WhatsApp Business Platform Cloud API documentation (v21.0+).
- **Text Messaging**: Direct text messaging with URL preview control and recipient validation.
- **Pre-Approved Templates**: Supports header, body, and button components with parameter substitution (text, currency, date_time, media).
- **Authentication & OTP Templates**: Clean abstractions for OTP delivery templates without generating OTPs inside the transport layer. Callers generate the OTP securely and pass it to formatted template parameters.
- **Privacy & Redaction**: Automatic phone number normalization and diagnostic masking (`+201*****1234`). Bearer access tokens, app secrets, and OTP values are strictly scrubbed from logs and traces.
- **Media Messaging & Uploads**: Supports images, documents, audio, video, and stickers either by uploaded Meta media ID or hosted HTTPS URL. Includes multipart file uploads and media metadata retrieval.
- **Robust Webhook Handling**:
  - Full support for Meta's GET challenge verification protocol (`hub.mode`, `hub.verify_token`, `hub.challenge`).
  - Constant-time HMAC-SHA256 signature verification over raw payloads (`X-Hub-Signature-256`) using Meta App Secret to prevent timing attacks.
  - Strongly typed parsing of inbound messages and message delivery status updates (`sent`, `delivered`, `read`, `failed`).
- **Resilience & Error Classification**: Classifies upstream API responses and network conditions into `None`, `Transient`, `Permanent`, or `Ambiguous` (preventing blind retries on network timeouts).
- **Extensible & Pluggable**: `IWhatsAppClientRegistry` supports custom client implementations and multi-provider configurations.
- **Fast-Fail Configuration**: Full integration with `Sops.Configuration` for fast startup validation via `IValidatableObject` and `ConfigurationValidator`.
- **Broad Compatibility**: Targets `netstandard2.0` (compatible with .NET 6/8/9+ and .NET Framework 4.8).

---

## Installation

```bash
dotnet add package Sops.WhatsApp
```

---

## Configuration

Add WhatsApp settings to `appsettings.json` or your environment variables:

```json
{
  "WhatsApp": {
    "Provider": "MetaCloud",
    "Enabled": true,
    "BaseUrl": "https://graph.facebook.com",
    "GraphApiVersion": "v21.0",
    "PhoneNumberId": "102553952458",
    "BusinessAccountId": "100000000000",
    "AccessToken": "EAAB...",
    "AppSecret": "your_meta_app_secret",
    "WebhookVerifyToken": "your_webhook_verify_token",
    "TimeoutSeconds": 30,
    "DefaultLanguage": "en_US"
  }
}
```

---

## Usage

### 1. Register with Dependency Injection

```csharp
using Microsoft.Extensions.DependencyInjection;
using Sops.WhatsApp.Extensions;

services.AddSopsWhatsApp(configuration);
```

### 2. Sending a Text Message

```csharp
using Sops.WhatsApp.Abstractions;
using Sops.WhatsApp.Models;

public class NotificationService
{
    private readonly IWhatsAppClient _whatsAppClient;

    public NotificationService(IWhatsAppClient whatsAppClient)
    {
        _whatsAppClient = whatsAppClient;
    }

    public async Task SendAlertAsync(string customerPhone)
    {
        var request = new WhatsAppTextMessageRequest
        {
            To = customerPhone,
            Body = "Your order #1042 has been dispatched and is on its way!",
            PreviewUrl = false
        };

        var result = await _whatsAppClient.SendTextMessageAsync(request);
        if (result.IsSuccess)
        {
            Console.WriteLine($"Dispatched message with ID: {result.MessageId}");
        }
    }
}
```

### 3. Sending an Authentication / OTP Template

```csharp
var otpCode = GenerateSecureOtp(); // Generated securely by your application logic

var request = new WhatsAppTemplateMessageRequest
{
    To = "+201001234567",
    TemplateName = "auth_otp_code",
    LanguageCode = "en_US",
    Components = new List<WhatsAppTemplateComponent>
    {
        WhatsAppTemplateComponent.CreateOtpBody(otpCode),
        WhatsAppTemplateComponent.CreateOtpButton(index: 0, otpCode: otpCode)
    }
};

var result = await _whatsAppClient.SendTemplateAsync(request);
```

### 4. Sending Media (Document Invoice)

```csharp
var request = new WhatsAppMediaMessageRequest
{
    To = "+201001234567",
    MediaType = WhatsAppMediaType.Document,
    Link = "https://cdn.example.com/invoices/inv_1042.pdf",
    Filename = "Invoice_1042.pdf",
    Caption = "Here is your invoice for order #1042."
};

var result = await _whatsAppClient.SendMediaAsync(request);
```

### 5. Webhook Validation and Processing

```csharp
// 1. Meta GET verification challenge
[HttpGet("webhook")]
public IActionResult VerifyWebhook(
    [FromQuery(Name = "hub.mode")] string mode,
    [FromQuery(Name = "hub.verify_token")] string token,
    [FromQuery(Name = "hub.challenge")] string challenge)
{
    if (_whatsAppClient.VerifyWebhookChallenge(mode, token, challenge, out var response))
    {
        return Content(response!, "text/plain");
    }
    return Forbid();
}

// 2. Meta POST event handling
[HttpPost("webhook")]
public async Task<IActionResult> HandleWebhook()
{
    string signature = Request.Headers["X-Hub-Signature-256"].FirstOrDefault();
    using var reader = new StreamReader(Request.Body);
    string rawPayload = await reader.ReadToEndAsync();

    if (!_whatsAppClient.ValidateWebhookSignature(signature, rawPayload))
    {
        return Unauthorized();
    }

    var webhookEvent = _whatsAppClient.ProcessWebhook(rawPayload);

    foreach (var status in webhookEvent.StatusUpdates)
    {
        Console.WriteLine($"Message {status.MessageId} transitioned to {status.Status}");
    }

    foreach (var msg in webhookEvent.IncomingMessages)
    {
        Console.WriteLine($"Received message from {msg.From}: {msg.TextBody}");
    }

    return Ok();
}
```

---

## Standalone Usage (No DI)

```csharp
var settings = new WhatsAppSettings
{
    PhoneNumberId = "102553952458",
    AccessToken = "EAAB...",
    AppSecret = "your_app_secret",
    WebhookVerifyToken = "your_token"
};

using var client = new MetaCloudWhatsAppClient(settings);
var result = await client.SendTextMessageAsync(new WhatsAppTextMessageRequest
{
    To = "+201001234567",
    Body = "Hello from standalone SOPs!"
});
```
