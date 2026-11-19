namespace Sops.Configuration.Abstractions;

/// <summary>
/// Provides custom self-validation logic for strongly typed settings objects.
/// </summary>
public interface IValidatableSettings
{
    /// <summary>
    /// Validates the current instance and yields validation error messages.
    /// </summary>
    /// <returns>An enumeration of validation error messages, or an empty sequence if valid.</returns>
    IEnumerable<string> Validate();
}
