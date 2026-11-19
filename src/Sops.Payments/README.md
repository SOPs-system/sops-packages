# Sops.Payments

`Sops.Payments` is a production-quality, provider-independent payment infrastructure NuGet package for SOPs applications, with first-class support for Paymob (Egypt and MENA). It provides typed payment contracts, monetary safety, idempotency guardrails, authoritative server-side verification, refund processing, and cryptographic HMAC-SHA512 webhook validation.

## Features

- **Provider-Independent Abstractions**: Core contracts (`IPaymentGateway`, `IPaymentGatewayRegistry`) decouple application business logic from vendor APIs.
- **Monetary Safety**: Dedicated `Money` value type enforcing decimal arithmetic, integer minor units (cents/piastres), and currency matching. Floating-point types (`float`, `double`) are completely avoided.
- **Dual Paymob Flow Support**:
  - **Modern Unified Checkout (Intention API)**: Native integration via `POST /v1/intention/` using API `SecretKey` and `PublicKey`.
  - **Legacy Flow**: 3-step token authentication, order registration, and payment key generation (`/api/auth/tokens`, `/api/ecommerce/orders`, `/api/acceptance/payment_keys`) with iframe integration.
- **Authoritative Server-Side Verification**: A browser redirect is never accepted as proof of payment. Payments are verified by querying Paymob's `/api/acceptance/transactions/{id}` directly.
- **Paymob Webhook Security**: Constant-time HMAC-SHA512 verification over the 20 documented Paymob transaction fields in strict lexicographical order to prevent timing and spoofing attacks.
- **Safe Refund Execution**: Explicit caller authorization and amount validation via Paymob's void/refund endpoint.
- **Idempotency & Retry Resilience**: Reuses caller-supplied idempotency tokens mapped to Paymob's `special_reference` / `merchant_order_id`. Ambiguous transaction states are classified explicitly to prevent duplicate charges.
- **PCI & Credential Protection**: API keys, secret keys, HMAC secrets, and cardholder data are masked; raw PANs and CVVs are never logged. Card numbers are truncated to safe last-4 masks (`****-****-****-1234`).
- **Startup Validation**: Integrated with `Sops.Configuration` for fast startup rejection of invalid base URLs, missing credentials, or invalid timeouts.
- **Cross-Framework Compatibility**: Targets `netstandard2.0`, compatible with .NET 6/8/9+ and .NET Framework 4.8.

---

## Installation

```bash
dotnet add package Sops.Payments
```

---

## Configuration

Configure payment settings in `appsettings.json`, environment variables, or in-memory collections:

```json
{
  "Payment": {
    "Provider": "Paymob",
    "Enabled": true,
    "Paymob": {
      "BaseUrl": "https://accept.paymob.com",
      "SecretKey": "sec_sk_live_...",
      "PublicKey": "sec_pk_live_...",
      "ApiKey": "api_key_...",
      "HmacSecret": "hmac_secret_from_dashboard",
      "IntegrationId": 123456,
      "IframeId": 789012,
      "DefaultCurrency": "EGP",
      "TimeoutSeconds": 30,
      "NotificationUrl": "https://api.yourdomain.com/webhooks/paymob",
      "RedirectionUrl": "https://yourdomain.com/checkout/complete"
    }
  }
}
```

Environment variables:

```bash
Payment__Provider=Paymob
Payment__Paymob__SecretKey=sec_sk_live_...
Payment__Paymob__PublicKey=sec_pk_live_...
Payment__Paymob__HmacSecret=hmac_secret_from_dashboard
Payment__Paymob__IntegrationId=123456
```

---

## Usage

### 1. Dependency Injection Setup

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Sops.Payments.Abstractions;
using Sops.Payments.Extensions;
using Sops.Payments.Models;

var services = new ServiceCollection();

// Register from configuration section "Payment"
services.AddSopsPayments(configuration);

using var provider = services.BuildServiceProvider();
var gateway = provider.GetRequiredService<IPaymentGateway>();
```

### 2. Initiating a Payment

```csharp
var customer = new PaymentCustomer("Ahmed", "Hassan", "ahmed@example.com", "+201000000000");

var request = new PaymentRequest
{
    Amount = Money.FromDecimal(250.00m, "EGP"), // 250.00 EGP = 25000 cents
    OrderId = "ORD-2026-001",
    IdempotencyKey = Guid.NewGuid().ToString("N"),
    Customer = customer
};
request.Items.Add(new PaymentItem("Annual Subscription", Money.FromDecimal(250.00m, "EGP"), quantity: 1));

PaymentResult result = await gateway.InitiatePaymentAsync(request);

if (result.IsSuccess)
{
    // Redirect customer to the checkout page:
    Console.WriteLine($"Redirect customer to: {result.CheckoutUrl}");
    Console.WriteLine($"Client secret: {result.ClientSecret}");
}
else
{
    Console.WriteLine($"Initiation failed ({result.ErrorClassification}): {result.ErrorMessage}");
}
```

### 3. Authoritative Server-Side Verification

Never assume payment success based solely on client browser redirects:

```csharp
PaymentVerificationResult verification = await gateway.VerifyPaymentAsync("12345678");

if (verification.IsSuccess)
{
    Console.WriteLine($"Confirmed payment of {verification.Amount} via {verification.PaymentMethod}!");
    Console.WriteLine($"Masked card: {verification.MaskedPan}");
}
else
{
    Console.WriteLine($"Payment status: {verification.Status}. Reason: {verification.ErrorMessage}");
}
```

### 4. Webhook Processing & HMAC-SHA512 Verification

```csharp
// Read incoming raw payload and hmac from query string (e.g. ?hmac=...)
string rawJson = await new StreamReader(Request.Body).ReadToEndAsync();
string? hmac = Request.Query["hmac"];

PaymentWebhookEvent? webhookEvent = await gateway.ProcessWebhookAsync(rawJson, hmac);

if (webhookEvent == null)
{
    // Signature rejected - return 400 or 401
    return Unauthorized("Invalid webhook signature.");
}

if (webhookEvent.Status == PaymentStatus.Successful)
{
    // Fulfill order:
    await FulfillOrderAsync(webhookEvent.OrderId, webhookEvent.TransactionId, webhookEvent.Amount);
}
```

### 5. Issuing Refunds

```csharp
var refundRequest = new RefundRequest
{
    TransactionId = "12345678",
    Amount = Money.FromDecimal(50.00m, "EGP"), // Partial refund
    Reason = "Customer return"
};

RefundResult refund = await gateway.RefundAsync(refundRequest);

if (refund.IsSuccess)
{
    Console.WriteLine($"Refund {refund.RefundTransactionId} completed for {refund.RefundedAmount}.");
}
```

---

## Idempotency Guarantees & Limitations

1. **Transient Retries**: Retrying a payment creation request with the same `IdempotencyKey` maps to Paymob's `special_reference` (Intention API) or `merchant_order_id` (Legacy).
2. **Ambiguous Outcomes**: If a socket disconnects or times out during payment processing, the outcome is classified as `PaymentErrorClassification.Ambiguous`. Applications **must not** automatically re-initiate payment without verifying the order status first.
3. **Durable Persistence**: Sops.Payments does not maintain a persistent database of idempotency keys; persistence and distributed locking belong to the host application or cache layer (e.g. `Sops.Cache`).

---

## Security Practices

- **Zero Credential Exposure**: `SecretKey`, `ApiKey`, and `HmacSecret` are decorated with `[Sensitive]` and masked in configuration dumps and exception traces.
- **PCI Compliance**: Full credit card numbers and CVVs are never handled directly by the backend library. Webhooks and verification responses extract only masked PAN values (`****-****-****-1234`).
- **Timing Attack Resistance**: Webhook HMAC comparison uses constant-time string byte comparison.
- **Provider Limitations**: Paymob refund availability is dependent on merchant account permissions and the underlying acquiring bank's settlement window.
