using System.Text.Json;
using System.Text.Json.Nodes;

namespace Sops.Configuration.Storage;

/// <summary>
/// Provides read, add, update, and delete (CRUD) operations directly on JSON configuration files.
/// </summary>
public sealed class JsonConfigurationFile
{
    private static readonly JsonSerializerOptions DefaultJsonOptions = new()
    {
        WriteIndented = true
    };

    private JsonObject _rootObject;

    private JsonConfigurationFile(string filePath, JsonObject rootObject)
    {
        FilePath = filePath ?? throw new ArgumentNullException(nameof(filePath));
        _rootObject = rootObject ?? new JsonObject();
    }

    /// <summary>
    /// Gets the path to the underlying JSON file.
    /// </summary>
    public string FilePath { get; }

    /// <summary>
    /// Opens an existing JSON configuration file, or initializes a new in-memory file if it does not yet exist.
    /// </summary>
    /// <param name="filePath">The path to the JSON configuration file.</param>
    /// <returns>A new <see cref="JsonConfigurationFile"/> instance.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="filePath"/> is null or whitespace.</exception>
    public static JsonConfigurationFile Open(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            throw new ArgumentException("File path cannot be null or whitespace.", nameof(filePath));

        if (!File.Exists(filePath))
        {
            return new JsonConfigurationFile(filePath, new JsonObject());
        }

        var content = File.ReadAllText(filePath);
        if (string.IsNullOrWhiteSpace(content))
        {
            return new JsonConfigurationFile(filePath, new JsonObject());
        }

        var node = JsonNode.Parse(content);
        var root = node as JsonObject ?? new JsonObject();
        return new JsonConfigurationFile(filePath, root);
    }

    /// <summary>
    /// Reads a configuration value at the specified key path.
    /// Supports colon-separated or slash-separated nested paths (e.g. "Database:ConnectionString").
    /// </summary>
    /// <param name="keyPath">The hierarchical path to the setting.</param>
    /// <returns>The string representation of the value, or null if not found.</returns>
    public string? Read(string keyPath)
    {
        if (string.IsNullOrWhiteSpace(keyPath))
            return null;

        var segments = SplitPath(keyPath);
        JsonNode? current = _rootObject;

        for (var i = 0; i < segments.Length; i++)
        {
            if (current is not JsonObject currentObj)
                return null;

            if (!currentObj.TryGetPropertyValue(segments[i], out current) || current == null)
                return null;
        }

        return current.ToString();
    }

    /// <summary>
    /// Adds or updates a setting at the specified hierarchical key path.
    /// Automatically creates intermediate JSON objects as needed.
    /// </summary>
    /// <param name="keyPath">The key path (e.g. "Database:MaxPoolSize" or "ApiKey").</param>
    /// <param name="value">The value to set (string, int, bool, double, etc.).</param>
    /// <returns>The current instance for fluent chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="keyPath"/> is null or whitespace.</exception>
    public JsonConfigurationFile Set(string keyPath, object? value)
    {
        if (string.IsNullOrWhiteSpace(keyPath))
            throw new ArgumentException("Key path cannot be null or whitespace.", nameof(keyPath));

        var segments = SplitPath(keyPath);
        JsonObject current = _rootObject;

        for (var i = 0; i < segments.Length - 1; i++)
        {
            var segment = segments[i];
            if (current.TryGetPropertyValue(segment, out var existingNode) && existingNode is JsonObject nextObj)
            {
                current = nextObj;
            }
            else
            {
                var newObj = new JsonObject();
                current[segment] = newObj;
                current = newObj;
            }
        }

        var leafKey = segments[segments.Length - 1];
        if (value == null)
        {
            current[leafKey] = null;
        }
        else if (value is JsonNode jsonNode)
        {
            current[leafKey] = jsonNode;
        }
        else if (value is string str)
        {
            current[leafKey] = JsonValue.Create(str);
        }
        else if (value is int intVal)
        {
            current[leafKey] = JsonValue.Create(intVal);
        }
        else if (value is long longVal)
        {
            current[leafKey] = JsonValue.Create(longVal);
        }
        else if (value is bool boolVal)
        {
            current[leafKey] = JsonValue.Create(boolVal);
        }
        else if (value is double doubleVal)
        {
            current[leafKey] = JsonValue.Create(doubleVal);
        }
        else
        {
            current[leafKey] = JsonValue.Create(value.ToString());
        }

        return this;
    }

    /// <summary>
    /// Deletes a setting at the specified key path.
    /// </summary>
    /// <param name="keyPath">The hierarchical path of the key to remove.</param>
    /// <returns>True if the key was found and removed; otherwise, false.</returns>
    public bool Remove(string keyPath)
    {
        if (string.IsNullOrWhiteSpace(keyPath))
            return false;

        var segments = SplitPath(keyPath);
        JsonObject? current = _rootObject;

        for (var i = 0; i < segments.Length - 1; i++)
        {
            if (!current.TryGetPropertyValue(segments[i], out var nextNode) || nextNode is not JsonObject nextObj)
            {
                return false;
            }
            current = nextObj;
        }

        var leafKey = segments[segments.Length - 1];
        return current.Remove(leafKey);
    }

    /// <summary>
    /// Saves the current configuration changes to disk.
    /// </summary>
    /// <param name="destinationPath">Optional destination path. If null, overwrites <see cref="FilePath"/>.</param>
    public void Save(string? destinationPath = null)
    {
        var targetPath = destinationPath ?? FilePath;
        var directory = Path.GetDirectoryName(targetPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        var json = _rootObject.ToJsonString(DefaultJsonOptions);
        File.WriteAllText(targetPath, json);
    }

    private static string[] SplitPath(string path)
    {
        return path.Split(new[] { ':', '/' }, StringSplitOptions.RemoveEmptyEntries);
    }
}
