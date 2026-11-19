using Sops.Configuration.Extensions;
using Sops.Configuration.Validation;

namespace Sops.Configuration.Tests;

public class ConfigurationSecurityTests
{
    [Fact]
    public void Security_NeverExposeSecretValuesInExceptionMessage_WhenValidationFails()
    {
        const string rawSecretApiKey = "SUPER_SECRET_KEY_123456789";
        const string rawSecretPassword = "TopSecretPassword99!";

        // ApiKey is only 10 chars (violates MinLength 16), Password is 5 chars (violates MinLength 8)
        var inMemory = new Dictionary<string, string?>
        {
            { "ApiKey", rawSecretApiKey.Substring(0, 10) },
            { "Password", rawSecretPassword.Substring(0, 5) }
        };

        var config = SopsConfiguration.CreateBuilder()
            .AddInMemoryCollection(inMemory)
            .Build();

        var ex = Assert.Throws<ConfigurationValidationException>(() =>
        {
            config.GetValid<TestSecuritySettings>();
        });

        // The raw secrets must NEVER appear in the exception message
        Assert.DoesNotContain(rawSecretApiKey, ex.Message);
        Assert.DoesNotContain(rawSecretPassword, ex.Message);
        Assert.DoesNotContain(rawSecretApiKey.Substring(0, 10), ex.Message);
        Assert.DoesNotContain(rawSecretPassword.Substring(0, 5), ex.Message);

        // Nor in ex.ToString()
        Assert.DoesNotContain(rawSecretApiKey, ex.ToString());
        Assert.DoesNotContain(rawSecretPassword, ex.ToString());

        // Nor in any individual error message
        foreach (var error in ex.Errors)
        {
            Assert.DoesNotContain(rawSecretApiKey, error.ErrorMessage);
            Assert.DoesNotContain(rawSecretPassword, error.ErrorMessage);
            Assert.DoesNotContain(rawSecretApiKey.Substring(0, 10), error.ErrorMessage);
            Assert.DoesNotContain(rawSecretPassword.Substring(0, 5), error.ErrorMessage);
        }
    }

    [Theory]
    [InlineData("password", true)]
    [InlineData("UserPassword", true)]
    [InlineData("db_password", true)]
    [InlineData("ApiKey", true)]
    [InlineData("api_key", true)]
    [InlineData("secret", true)]
    [InlineData("ClientSecret", true)]
    [InlineData("token", true)]
    [InlineData("AuthToken", true)]
    [InlineData("ConnectionString", true)]
    [InlineData("private_key", true)]
    [InlineData("credential", true)]
    [InlineData("Timeout", false)]
    [InlineData("Host", false)]
    [InlineData("Port", false)]
    [InlineData("UserName", false)]
    [InlineData("AppName", false)]
    public void Security_SecretMasker_IdentifiesSensitiveNames(string propertyName, bool expectedSensitive)
    {
        var isSensitive = SecretMasker.IsSensitivePropertyName(propertyName);
        Assert.Equal(expectedSensitive, isSensitive);
    }

    [Fact]
    public void Security_SecretMasker_RedactsRawSecretFromErrorMessage()
    {
        const string rawSecret = "my_db_secret_pass";
        var message = $"Validation failed: Value '{rawSecret}' does not meet requirements.";

        var sanitized = SecretMasker.RedactValueFromMessage(message, rawSecret);

        Assert.DoesNotContain(rawSecret, sanitized);
        Assert.Contains(SecretMasker.RedactedPlaceholder, sanitized);
    }

    [Fact]
    public void Security_RequiredValue_MissingSensitiveKey_DoesNotLeakValueOrContext()
    {
        var config = SopsConfiguration.CreateBuilder().Build();

        var ex = Assert.Throws<ConfigurationValidationException>(() =>
        {
            config.GetRequiredValue("App:Database:Password");
        });

        Assert.True(ex.Errors[0].IsSensitive);
        Assert.Contains("App:Database:Password", ex.Message);
        Assert.Contains("missing or empty", ex.Message);
    }

    [Fact]
    public void Security_AssemblyContainsNoHardcodedCredentials()
    {
        var assembly = typeof(SopsConfiguration).Assembly;
        var types = assembly.GetTypes();

        foreach (var type in types)
        {
            var fields = type.GetFields(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static);
            foreach (var field in fields)
            {
                if (field.FieldType == typeof(string) && field.IsLiteral && !field.IsInitOnly)
                {
                    var val = (string?)field.GetRawConstantValue();
                    if (!string.IsNullOrEmpty(val))
                    {
                        Assert.False(val.StartsWith("ghp_"), $"Found potential token in {type.Name}.{field.Name}");
                        Assert.False(val.StartsWith("sk_live"), $"Found potential token in {type.Name}.{field.Name}");
                    }
                }
            }
        }
    }
}
