using Sops.Logging.Redaction;

namespace Sops.Logging.Tests;

public class SopsLogRedactorTests
{
    private readonly SopsLogRedactor _redactor = new();

    [Fact]
    public void Redact_CreditCardNumbers_MasksSuccessfully()
    {
        var input = "Payment failed for card 4111-2222-3333-4444 on gateway";
        var output = _redactor.Redact(input);

        Assert.DoesNotContain("4111-2222-3333-4444", output);
        Assert.Contains("[REDACTED_CARD]", output);
    }

    [Fact]
    public void Redact_Cvv_MasksSuccessfully()
    {
        var input = "Card validation: cvv: 123 and security_code=456";
        var output = _redactor.Redact(input);

        Assert.DoesNotContain("123", output);
        Assert.DoesNotContain("456", output);
        Assert.Contains("[REDACTED]", output);
    }

    [Fact]
    public void Redact_AuthorizationHeaders_MasksTokens()
    {
        var input = "HTTP Request header: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.xyz and Basic dXNlcjpwYXNz";
        var output = _redactor.Redact(input);

        Assert.DoesNotContain("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.xyz", output);
        Assert.DoesNotContain("dXNlcjpwYXNz", output);
        Assert.Contains("Bearer [REDACTED]", output);
        Assert.Contains("Basic [REDACTED]", output);
    }

    [Fact]
    public void Redact_Passwords_InTextAndConnectionStrings_MasksSuccessfully()
    {
        var input = "Failed to connect with password=SuperSecretPassword123; and json: {\"password\": \"MyPass99\"}";
        var output = _redactor.Redact(input);

        Assert.DoesNotContain("SuperSecretPassword123", output);
        Assert.DoesNotContain("MyPass99", output);
        Assert.Contains("[REDACTED]", output);
    }

    [Fact]
    public void Redact_ApiKeysAndSecrets_MasksSuccessfully()
    {
        var input = "Webhook received with api_key: secret_api_key_value and client_secret='topsecret'";
        var output = _redactor.Redact(input);

        Assert.DoesNotContain("secret_api_key_value", output);
        Assert.DoesNotContain("topsecret", output);
        Assert.Contains("[REDACTED]", output);
    }

    [Fact]
    public void Redact_OtpsAndVerificationCodes_MasksSuccessfully()
    {
        var input = "Customer OTP sent: otp: 789456 and verification_code=1234";
        var output = _redactor.Redact(input);

        Assert.DoesNotContain("789456", output);
        Assert.DoesNotContain("1234", output);
        Assert.Contains("[REDACTED]", output);
    }

    [Fact]
    public void Redact_KnownTokens_GitHubAndStripe_MasksSuccessfully()
    {
        var input = "Deploy token ghp_123456789012345678901234567890123456 and Stripe sk_live_999999999999999999999999";
        var output = _redactor.Redact(input);

        Assert.DoesNotContain("ghp_123456789012345678901234567890123456", output);
        Assert.DoesNotContain("sk_live_999999999999999999999999", output);
        Assert.Contains("[REDACTED_TOKEN]", output);
    }

    [Fact]
    public void Redact_CustomPatterns_MasksCustomFields()
    {
        var redactor = new SopsLogRedactor(new[] { @"\bSSN:\s*\d{3}-\d{2}-\d{4}\b" });
        var input = "Patient SSN: 000-12-3456 admitted";
        var output = redactor.Redact(input);

        Assert.DoesNotContain("000-12-3456", output);
        Assert.Contains("[REDACTED]", output);
    }

    [Fact]
    public void Redact_NonSensitiveLogMessage_IsPreservedIntact()
    {
        var input = "SOPs service started. Inventory synchronization delayed for 10 items.";
        var output = _redactor.Redact(input);

        Assert.Equal(input, output);
    }
}
