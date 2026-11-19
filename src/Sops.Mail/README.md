# Sops.Mail

`Sops.Mail` is a production-quality, reusable email infrastructure NuGet package for SOPs applications and services. Built on `MailKit` and `MimeKit`, it provides robust SMTP email transmission, MIME composition, template variable replacement, failure classification, and security guardrails.

## Features

- **Provider-Independent Contract**: Clean, testable `IMailSender` and `IMailProviderRegistry` abstractions.
- **Production SMTP Transport**: Implemented using MailKit/MimeKit with explicit TLS control, connection timeouts, and authentication.
- **Rich MIME Message Composition**: Full support for `To`, `Cc`, `Bcc`, `ReplyTo`, custom headers, HTML bodies, plain-text bodies, standard attachments, and inline image/asset attachments with `cid:` references.
- **Actionable Failure Classification**: `MailSendResult` classifies errors into `Transient`, `Permanent`, or `Ambiguous` to drive intelligent retry policies (e.g. preventing duplicate sends after socket timeouts during DATA transmission).
- **Safe, Lightweight Templating**: `MailTemplateEngine` provides fast placeholder replacement (`{{var}}`, `{{{raw}}}`), automatic HTML encoding to prevent XSS, and configurable strict/lenient validation.
- **Enterprise Security Guardrails**: `[Sensitive]` attribute masking on SMTP credentials, zero credential leakage in logs or exceptions, and RFC-compliant CRLF header injection protection.
- **Startup Validation**: Integrated with `Sops.Configuration` for early failure detection on invalid host, port, security mode, or credentials.
- **Dual Consumption**: Full `Microsoft.Extensions.DependencyInjection` integration or direct standalone instantiation without DI containers.
- **Cross-Framework Compatibility**: Targets `netstandard2.0`, compatible with modern .NET (6/8/9+) and .NET Framework 4.8.

---

## Installation

```bash
dotnet add package Sops.Mail
```

---

## Configuration

Configure email settings in `appsettings.json`, environment variables, or in-memory settings:

```json
{
  "Mail": {
    "Provider": "Smtp",
    "Enabled": true,
    "FromAddress": "no-reply@sops.internal",
    "FromName": "SOPs Notification Service",
    "Smtp": {
      "Host": "smtp.sendgrid.net",
      "Port": 587,
      "Security": "StartTls",
      "Username": "apikey",
      "Password": "your-secure-api-key",
      "TimeoutSeconds": 30,
      "RequireAuthentication": true
    }
  }
}
```

Environment variables can also be used:

```bash
Mail__Provider=Smtp
Mail__FromAddress=no-reply@sops.internal
Mail__Smtp__Host=smtp.office365.com
Mail__Smtp__Port=587
Mail__Smtp__Security=StartTls
Mail__Smtp__Username=svc-sops@company.com
Mail__Smtp__Password=YourSecretPassword
```

---

## Usage

### 1. Dependency Injection Setup

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sops.Mail.Abstractions;
using Sops.Mail.Extensions;
using Sops.Mail.Models;

var services = new ServiceCollection();

// Register from IConfiguration section "Mail"
services.AddSopsMail(configuration);

// Or configure via action delegate
services.AddSopsMail(options =>
{
    options.FromAddress = "system@example.com";
    options.FromName = "SOPs System";
    options.Smtp.Host = "smtp.example.com";
    options.Smtp.Port = 587;
    options.Smtp.Security = SmtpSecurity.StartTls;
    options.Smtp.Username = "smtp-user";
    options.Smtp.Password = "smtp-secret";
});

using var serviceProvider = services.BuildServiceProvider();
var mailSender = serviceProvider.GetRequiredService<IMailSender>();

var message = new MailMessage
{
    Subject = "Monthly Inventory Audit",
    TextBody = "Please find the attached inventory audit report.",
    HtmlBody = "<p>Please find the attached <strong>inventory audit report</strong>.</p>"
};
message.AddTo("warehouse-mgr@example.com", "Warehouse Manager");
message.AddAttachment(MailAttachment.FromBytes(reportPdfBytes, "AuditReport.pdf", "application/pdf"));

MailSendResult result = await mailSender.SendAsync(message);

if (result.IsSuccess)
{
    Console.WriteLine($"Message sent! ID: {result.MessageId}");
}
else
{
    Console.WriteLine($"Send failed ({result.ErrorClassification}): {result.ErrorMessage}");
}
```

### 2. Standalone Usage (Without DI)

```csharp
using Sops.Mail.Configuration;
using Sops.Mail.Models;
using Sops.Mail.Providers;

var settings = new MailSettings
{
    FromAddress = "notifications@sops.internal",
    FromName = "SOPs Alerting",
    Smtp = new SmtpSettings
    {
        Host = "smtp.internal.local",
        Port = 25,
        Security = SmtpSecurity.None
    }
};

using var sender = SmtpMailSender.Create(settings);

var message = new MailMessage("admin@sops.internal", "System Alert", "Node 3 memory pressure detected.");
var result = await sender.SendAsync(message);
```

### 3. Template Engine

`MailTemplateEngine` renders template strings or complete messages with variables, ensuring HTML escaping for safe injection:

```csharp
using Sops.Mail.Models;
using Sops.Mail.Templates;

string templateHtml = "<h1>Welcome, {{UserName}}!</h1><p>Your activation code is <strong>{{Code}}</strong>.</p>";

var variables = new Dictionary<string, object?>
{
    ["UserName"] = "Alice <admin>",
    ["Code"] = "123-456"
};

// Safe rendering: Alice <admin> is automatically encoded to Alice &lt;admin&gt;
string html = MailTemplateEngine.Render(templateHtml, variables, htmlEncode: true);

// Or render an entire MailMessage template
var templateMessage = new MailMessage
{
    Subject = "Welcome to SOPs, {{UserName}}",
    TextBody = "Hello {{UserName}}, your code is {{Code}}.",
    HtmlBody = templateHtml
};
templateMessage.AddTo("alice@example.com");

MailMessage readyToSend = MailTemplateEngine.RenderMessage(templateMessage, variables);
```

---

## Security Practices

1. **Credential Protection**: `SmtpSettings.Password` is marked with `[Sensitive]`. Sops configuration validators, secret maskers, and logging pipelines automatically redact the password.
2. **Never Downgrade TLS**: Explicit security modes (`SslOnConnect`, `StartTls`) will fail loudly rather than silently transmitting in cleartext if the server does not support the expected encryption protocol.
3. **No Header Injection**: `MailRecipient`, `MailMessage`, and `MailAttachment` enforce RFC-compliant validation that rejects carriage return (`\r`) and newline (`\n`) characters in addresses, display names, file names, headers, and subjects.
4. **Error Classification for Retries**:
   - `Transient`: Network timeout, 4xx error. Safe to retry with exponential backoff.
   - `Permanent`: Bad recipient, 5xx error, auth failed. Do not retry without user intervention.
   - `Ambiguous`: Socket disconnected during or after `DATA` command. Do NOT blindly auto-retry to prevent sending duplicate emails.
