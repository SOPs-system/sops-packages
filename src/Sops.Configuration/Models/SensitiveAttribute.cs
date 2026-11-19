namespace Sops.Configuration.Models;

/// <summary>
/// Marks a configuration property as sensitive (e.g. passwords, API keys, secrets, tokens).
/// Properties with this attribute will have their values redacted from validation errors and diagnostic output.
/// </summary>
[AttributeUsage(AttributeTargets.Property | AttributeTargets.Field, AllowMultiple = false, Inherited = true)]
public sealed class SensitiveAttribute : Attribute
{
}
