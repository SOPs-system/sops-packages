using System.ComponentModel.DataAnnotations;
using Sops.Configuration.Abstractions;
using Sops.Configuration.Models;

namespace Sops.Configuration.Tests;

public class TestDatabaseSettings
{
    [Required(ErrorMessage = "The ConnectionString field is required.")]
    public string ConnectionString { get; set; } = string.Empty;

    [Range(1, 100, ErrorMessage = "MaxPoolSize must be between 1 and 100.")]
    public int MaxPoolSize { get; set; } = 10;

    public int TimeoutSeconds { get; set; } = 30;
}

public class TestEndpointSettings
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    [Url(ErrorMessage = "Url must be a valid URL.")]
    public string Url { get; set; } = string.Empty;
}

public class TestAppSettings
{
    [Required]
    public string ApplicationName { get; set; } = string.Empty;

    public TestDatabaseSettings Database { get; set; } = new();

    public List<TestEndpointSettings> Endpoints { get; set; } = new();
}

public class TestSecuritySettings
{
    [Required]
    [Sensitive]
    [MinLength(16, ErrorMessage = "ApiKey must be at least 16 characters long.")]
    public string ApiKey { get; set; } = string.Empty;

    [Required]
    [MinLength(8, ErrorMessage = "Password must be at least 8 characters long.")]
    public string Password { get; set; } = string.Empty;

    [Sensitive]
    public string SecretToken { get; set; } = string.Empty;
}

public class TestSelfValidatingSettings : IValidatableSettings
{
    public string Mode { get; set; } = string.Empty;

    public IEnumerable<string> Validate()
    {
        if (string.Equals(Mode, "InvalidMode", StringComparison.OrdinalIgnoreCase))
        {
            yield return "Mode cannot be InvalidMode.";
        }
    }
}

public class TestValidatableObjectSettings : IValidatableObject
{
    public int MinValue { get; set; }
    public int MaxValue { get; set; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (MinValue > MaxValue)
        {
            yield return new ValidationResult(
                "MinValue cannot be greater than MaxValue.",
                new[] { nameof(MinValue), nameof(MaxValue) });
        }
    }
}
