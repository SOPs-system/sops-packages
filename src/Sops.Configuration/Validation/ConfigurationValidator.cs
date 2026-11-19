using System.Collections;
using System.ComponentModel.DataAnnotations;
using System.Reflection;
using System.Runtime.CompilerServices;
using Sops.Configuration.Abstractions;

namespace Sops.Configuration.Validation;

/// <summary>
/// Validates configuration settings objects using DataAnnotations, <see cref="IValidatableObject"/>,
/// <see cref="IValidatableSettings"/>, and recursive object-graph traversal.
/// Guarantees that sensitive property values are never leaked in error messages.
/// </summary>
public static class ConfigurationValidator
{
    private sealed class ReferenceEqualityComparer : IEqualityComparer<object>
    {
        public static readonly ReferenceEqualityComparer Instance = new();
        public new bool Equals(object? x, object? y) => ReferenceEquals(x, y);
        public int GetHashCode(object obj) => RuntimeHelpers.GetHashCode(obj);
    }

    /// <summary>
    /// Validates a settings instance. Throws a <see cref="ConfigurationValidationException"/> if validation fails.
    /// </summary>
    /// <param name="instance">The settings object to validate.</param>
    /// <param name="sectionName">The optional section name for error reporting.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="instance"/> is null.</exception>
    /// <exception cref="ConfigurationValidationException">Thrown when validation rules are violated.</exception>
    public static void Validate(object instance, string? sectionName = null)
    {
        if (instance == null)
            throw new ArgumentNullException(nameof(instance));

        var errors = new List<ConfigurationValidationError>();
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);

        ValidateInternal(instance, sectionName, string.Empty, errors, visited);

        if (errors.Count > 0)
        {
            throw new ConfigurationValidationException(instance.GetType(), sectionName, errors);
        }
    }

    /// <summary>
    /// Attempts to validate a settings instance, returning false and a list of errors if invalid.
    /// </summary>
    /// <param name="instance">The settings object to validate.</param>
    /// <param name="sectionName">The optional section name for error reporting.</param>
    /// <param name="errors">The list of discovered validation errors.</param>
    /// <returns>True if the object is valid; otherwise, false.</returns>
    public static bool TryValidate(object instance, string? sectionName, out IReadOnlyList<ConfigurationValidationError> errors)
    {
        if (instance == null)
        {
            errors = new[] { new ConfigurationValidationError(string.Empty, "Instance cannot be null.") };
            return false;
        }

        var errorList = new List<ConfigurationValidationError>();
        var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);

        ValidateInternal(instance, sectionName, string.Empty, errorList, visited);
        errors = errorList;
        return errorList.Count == 0;
    }

    private static void ValidateInternal(
        object instance,
        string? sectionName,
        string pathPrefix,
        List<ConfigurationValidationError> errors,
        HashSet<object> visited)
    {
        if (instance == null || !visited.Add(instance))
        {
            return;
        }

        var instanceType = instance.GetType();

        // 1. Validate object-level IValidatableSettings
        if (instance is IValidatableSettings selfValidating)
        {
            try
            {
                var customErrors = selfValidating.Validate();
                if (customErrors != null)
                {
                    foreach (var err in customErrors)
                    {
                        if (!string.IsNullOrWhiteSpace(err))
                        {
                            errors.Add(new ConfigurationValidationError(pathPrefix, err));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add(new ConfigurationValidationError(pathPrefix, $"Validation threw an exception: {ex.Message}"));
            }
        }

        // 2. Validate object-level IValidatableObject
        if (instance is IValidatableObject validatableObject)
        {
            var context = new ValidationContext(instance) { MemberName = pathPrefix };
            try
            {
                var results = validatableObject.Validate(context);
                if (results != null)
                {
                    foreach (var res in results)
                    {
                        if (res != null && res != ValidationResult.Success)
                        {
                            var memberPath = res.MemberNames != null && res.MemberNames.Any()
                                ? CombinePath(pathPrefix, string.Join(", ", res.MemberNames))
                                : pathPrefix;
                            errors.Add(new ConfigurationValidationError(memberPath, res.ErrorMessage ?? "Validation failed."));
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                errors.Add(new ConfigurationValidationError(pathPrefix, $"Validation threw an exception: {ex.Message}"));
            }
        }

        // 3. Inspect public readable instance properties
        var properties = instanceType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
        foreach (var property in properties)
        {
            if (!property.CanRead)
                continue;

            var currentPath = CombinePath(pathPrefix, property.Name);
            var isSensitive = SecretMasker.IsSensitive(property);

            object? value = null;
            try
            {
                value = property.GetValue(instance);
            }
            catch (Exception ex)
            {
                errors.Add(new ConfigurationValidationError(currentPath, $"Unable to read property: {ex.Message}", isSensitive));
                continue;
            }

            // Validate DataAnnotations attributes on the property
            var validationAttributes = property.GetCustomAttributes<ValidationAttribute>(inherit: true);
            foreach (var attribute in validationAttributes)
            {
                var context = new ValidationContext(instance)
                {
                    MemberName = property.Name,
                    DisplayName = property.Name
                };

                var result = attribute.GetValidationResult(value, context);
                if (result != null && result != ValidationResult.Success)
                {
                    var msg = result.ErrorMessage ?? $"The field {property.Name} is invalid.";
                    if (isSensitive && value != null)
                    {
                        msg = SecretMasker.RedactValueFromMessage(msg, value.ToString());
                    }
                    errors.Add(new ConfigurationValidationError(currentPath, msg, isSensitive));
                }
            }

            // Recurse into complex nested objects and collections
            if (value != null)
            {
                if (IsComplexType(property.PropertyType))
                {
                    ValidateInternal(value, sectionName, currentPath, errors, visited);
                }
                else if (value is IEnumerable enumerable && !(value is string))
                {
                    var index = 0;
                    foreach (var item in enumerable)
                    {
                        if (item != null && IsComplexType(item.GetType()))
                        {
                            var itemPath = $"{currentPath}[{index}]";
                            ValidateInternal(item, sectionName, itemPath, errors, visited);
                        }
                        index++;
                    }
                }
            }
        }
    }

    private static bool IsComplexType(Type type)
    {
        if (type.IsPrimitive || type.IsEnum || type.IsValueType)
            return false;

        if (type == typeof(string) || type == typeof(decimal) || type == typeof(DateTime) ||
            type == typeof(TimeSpan) || type == typeof(Guid) || type == typeof(Uri))
            return false;

        if (typeof(IEnumerable).IsAssignableFrom(type) && !typeof(IDictionary).IsAssignableFrom(type))
            return false;

        return type.IsClass;
    }

    private static string CombinePath(string prefix, string name)
    {
        if (string.IsNullOrEmpty(prefix))
            return name;
        if (string.IsNullOrEmpty(name))
            return prefix;
        return $"{prefix}:{name}";
    }
}
