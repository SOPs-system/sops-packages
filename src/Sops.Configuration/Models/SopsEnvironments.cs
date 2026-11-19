namespace Sops.Configuration.Models;

/// <summary>
/// Standard environment names and helper methods for environment resolution and comparison.
/// </summary>
public static class SopsEnvironments
{
    /// <summary>
    /// Development environment name.
    /// </summary>
    public const string Development = "Development";

    /// <summary>
    /// Testing environment name.
    /// </summary>
    public const string Testing = "Testing";

    /// <summary>
    /// Staging environment name.
    /// </summary>
    public const string Staging = "Staging";

    /// <summary>
    /// Production environment name.
    /// </summary>
    public const string Production = "Production";

    /// <summary>
    /// Normalizes an environment name string into canonical casing.
    /// Defaults to <see cref="Production"/> if the input is null, empty, or whitespace.
    /// </summary>
    /// <param name="environment">The environment name to normalize.</param>
    /// <returns>The normalized environment name.</returns>
    public static string Normalize(string? environment)
    {
        if (string.IsNullOrWhiteSpace(environment))
        {
            return Production;
        }

        var trimmed = environment!.Trim();
        if (string.Equals(trimmed, Development, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "Dev", StringComparison.OrdinalIgnoreCase))
            return Development;
        if (string.Equals(trimmed, Testing, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "Test", StringComparison.OrdinalIgnoreCase))
            return Testing;
        if (string.Equals(trimmed, Staging, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "Stage", StringComparison.OrdinalIgnoreCase))
            return Staging;
        if (string.Equals(trimmed, Production, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "Prod", StringComparison.OrdinalIgnoreCase))
            return Production;

        return trimmed;
    }

    /// <summary>
    /// Determines whether the specified environment is Development.
    /// </summary>
    /// <param name="environment">The environment name.</param>
    /// <returns>True if the environment is Development; otherwise, false.</returns>
    public static bool IsDevelopment(string? environment) =>
        string.Equals(Normalize(environment), Development, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Determines whether the specified environment is Testing.
    /// </summary>
    /// <param name="environment">The environment name.</param>
    /// <returns>True if the environment is Testing; otherwise, false.</returns>
    public static bool IsTesting(string? environment) =>
        string.Equals(Normalize(environment), Testing, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Determines whether the specified environment is Staging.
    /// </summary>
    /// <param name="environment">The environment name.</param>
    /// <returns>True if the environment is Staging; otherwise, false.</returns>
    public static bool IsStaging(string? environment) =>
        string.Equals(Normalize(environment), Staging, StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Determines whether the specified environment is Production.
    /// </summary>
    /// <param name="environment">The environment name.</param>
    /// <returns>True if the environment is Production; otherwise, false.</returns>
    public static bool IsProduction(string? environment) =>
        string.Equals(Normalize(environment), Production, StringComparison.OrdinalIgnoreCase);
}
