using System.Text.RegularExpressions;
using Sops.Logging.Abstractions;

namespace Sops.Logging.Redaction;

/// <summary>
/// Default implementation of <see cref="ISopsLogRedactor"/> that masks sensitive customer data,
/// credit cards, OTPs, authorization headers, passwords, and API keys using optimized pattern matching.
/// </summary>
public sealed class SopsLogRedactor : ISopsLogRedactor
{
    private static readonly RegexOptions DefaultRegexOptions = RegexOptions.Compiled | RegexOptions.IgnoreCase;

    // 1. Credit Cards: 16 digits (or groups of 4)
    private static readonly Regex CreditCardRegex = new(
        @"\b(?:\d{4}[ -]?){3}\d{4}\b",
        DefaultRegexOptions);

    // 2. CVV / CVC
    private static readonly Regex CvvRegex = new(
        @"\b(cvv|cvc|security[_-]?code)\s*[:=]\s*[""']?\d{3,4}\b",
        DefaultRegexOptions);

    // 3. Authorization headers (Bearer / Basic)
    private static readonly Regex AuthHeaderRegex = new(
        @"\b(Bearer|Basic)\s+[A-Za-z0-9\-._~+/]+=*",
        DefaultRegexOptions);

    // 4. Passwords in key-value, JSON, or connection strings
    private static readonly Regex PasswordRegex = new(
        @"[""']?\b(password|passwd|pwd)[""']?\s*([:=])\s*[""']?([^\s""',;{}]+)[""']?",
        DefaultRegexOptions);

    // 5. API Keys, Tokens, Client Secrets
    private static readonly Regex ApiKeyRegex = new(
        @"[""']?\b(api[_-]?key|apikey|client[_-]?secret|private[_-]?key|secret|token)\s*([:=])\s*[""']?([^\s""',;{}]+)[""']?",
        DefaultRegexOptions);

    // 6. OTPs and verification codes
    private static readonly Regex OtpRegex = new(
        @"[""']?\b(otp|verification[_-]?code|auth[_-]?code|passcode)\s*([:=])\s*[""']?([^\s""',;{}]+)[""']?",
        DefaultRegexOptions);

    // 7. Known token prefixes (GitHub tokens, Stripe secret keys)
    private static readonly Regex KnownTokensRegex = new(
        @"\b(ghp_[A-Za-z0-9]{36}|sk_(?:live|test)_[A-Za-z0-9]{20,})\b",
        DefaultRegexOptions);

    private readonly List<Regex> _customRegexes = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="SopsLogRedactor"/> class.
    /// </summary>
    /// <param name="customPatterns">Optional list of additional regex patterns to redact.</param>
    public SopsLogRedactor(IEnumerable<string>? customPatterns = null)
    {
        if (customPatterns != null)
        {
            foreach (var pattern in customPatterns)
            {
                if (!string.IsNullOrWhiteSpace(pattern))
                {
                    _customRegexes.Add(new Regex(pattern, DefaultRegexOptions));
                }
            }
        }
    }

    /// <inheritdoc />
    public string Redact(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return text ?? string.Empty;

        var result = text!;

        // 1. Redact credit card numbers
        result = CreditCardRegex.Replace(result, "[REDACTED_CARD]");

        // 2. Redact CVV
        result = CvvRegex.Replace(result, "$1: [REDACTED]");

        // 3. Redact Authorization headers
        result = AuthHeaderRegex.Replace(result, "$1 [REDACTED]");

        // 4. Redact Passwords
        result = PasswordRegex.Replace(result, "$1$2\"[REDACTED]\"");

        // 5. Redact API keys and tokens
        result = ApiKeyRegex.Replace(result, "$1$2\"[REDACTED]\"");

        // 6. Redact OTPs
        result = OtpRegex.Replace(result, "$1$2\"[REDACTED]\"");

        // 7. Redact known token prefixes
        result = KnownTokensRegex.Replace(result, "[REDACTED_TOKEN]");

        // 8. Custom user-defined patterns
        for (var i = 0; i < _customRegexes.Count; i++)
        {
            result = _customRegexes[i].Replace(result, "[REDACTED]");
        }

        return result;
    }
}
