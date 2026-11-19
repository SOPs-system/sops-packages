using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace Sop.Ui.Localization
{
    /// <summary>
    /// File-based JSON localization engine for the SOPs ecosystem.
    /// Loads translation dictionaries from external JSON file paths, allowing
    /// multiple independent projects to share or configure translation dictionaries
    /// without baking localized strings into assembly packages.
    /// </summary>
    public sealed class SopsLocalization : INotifyPropertyChanged
    {
        private static readonly Lazy<SopsLocalization> _lazy = new Lazy<SopsLocalization>(() => new SopsLocalization());

        /// <summary>Gets the global singleton instance.</summary>
        public static SopsLocalization Instance => _lazy.Value;

        private readonly ConcurrentDictionary<string, Dictionary<string, string>> _languages =
            new ConcurrentDictionary<string, Dictionary<string, string>>(StringComparer.OrdinalIgnoreCase);

        private string _currentLanguage = "en";
        private bool _syncWithThemeDirection = true;

        /// <summary>Occurs when a property value changes (e.g. language switch for dynamic XAML bindings).</summary>
        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>Occurs when the active language changes.</summary>
        public event EventHandler<string>? LanguageChanged;

        public SopsLocalization()
        {
        }

        /// <summary>
        /// Gets or sets whether changing language automatically synchronizes <see cref="Theme.IsRtl"/>.
        /// Default is <c>true</c> (e.g. "ar" sets RTL, "en" sets LTR).
        /// </summary>
        public bool SyncWithThemeDirection
        {
            get => _syncWithThemeDirection;
            set => _syncWithThemeDirection = value;
        }

        /// <summary>
        /// Gets or sets the active language code (e.g. "en", "ar").
        /// Changing this property updates all active XAML bindings.
        /// </summary>
        public string CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                if (string.IsNullOrWhiteSpace(value)) return;
                var normalized = value.Trim().ToLowerInvariant();
                bool isAr = normalized.StartsWith("ar", StringComparison.OrdinalIgnoreCase);

                if (_syncWithThemeDirection)
                {
                    Theme.IsRtl = isAr;
                }

                if (string.Equals(_currentLanguage, normalized, StringComparison.OrdinalIgnoreCase)) return;

                _currentLanguage = normalized;

                OnPropertyChanged(nameof(CurrentLanguage));
                OnPropertyChanged("Item[]");
                LanguageChanged?.Invoke(this, _currentLanguage);
            }
        }

        /// <summary>
        /// Gets all loaded language codes.
        /// </summary>
        public IReadOnlyCollection<string> LoadedLanguages => new List<string>(_languages.Keys);

        /// <summary>
        /// Indexer for dynamic WPF XAML binding: <c>{Binding [key], Source={x:Static loc:SopsLocalization.Instance}}</c>.
        /// </summary>
        /// <param name="key">The translation key (e.g. "app.title" or "search.placeholder").</param>
        /// <returns>The localized text, or the key itself if not found.</returns>
        [IndexerName("Item")]
        public string this[string key] => Get(key);

        /// <summary>
        /// Retrieves the localized string for the specified key in the current language.
        /// </summary>
        /// <param name="key">The translation key (e.g. "app.title").</param>
        /// <param name="args">Optional format arguments.</param>
        /// <returns>The formatted localized string, or the key if not found.</returns>
        public string Get(string key, params object[] args)
        {
            if (string.IsNullOrEmpty(key)) return string.Empty;

            string? translation = null;

            if (_languages.TryGetValue(_currentLanguage, out var dict) && dict.TryGetValue(key, out var val))
            {
                translation = val;
            }
            else if (_languages.TryGetValue("en", out var enDict) && enDict.TryGetValue(key, out var fallback))
            {
                translation = fallback;
            }

            if (translation == null)
            {
                return key;
            }

            if (args != null && args.Length > 0)
            {
                try
                {
                    return string.Format(translation, args);
                }
                catch
                {
                    return translation;
                }
            }

            return translation;
        }

        /// <summary>
        /// Loads all JSON translation files from the specified directory path.
        /// Expected file naming convention: <c>{languageCode}.json</c> (e.g. <c>en.json</c>, <c>ar.json</c>).
        /// </summary>
        /// <param name="directoryPath">The absolute or relative directory path.</param>
        /// <returns>The count of language files loaded.</returns>
        public int LoadFromDirectory(string directoryPath)
        {
            if (string.IsNullOrWhiteSpace(directoryPath) || !Directory.Exists(directoryPath))
            {
                return 0;
            }

            int count = 0;
            foreach (var file in Directory.EnumerateFiles(directoryPath, "*.json", SearchOption.TopDirectoryOnly))
            {
                var lang = Path.GetFileNameWithoutExtension(file);
                if (!string.IsNullOrWhiteSpace(lang))
                {
                    LoadFromFile(lang, file);
                    count++;
                }
            }

            return count;
        }

        /// <summary>
        /// Loads translations for a specific language from an external JSON file path.
        /// </summary>
        /// <param name="languageCode">The language identifier (e.g. "en", "ar").</param>
        /// <param name="filePath">Path to the JSON file.</param>
        public void LoadFromFile(string languageCode, string filePath)
        {
            if (string.IsNullOrWhiteSpace(languageCode)) throw new ArgumentNullException(nameof(languageCode));
            if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentNullException(nameof(filePath));
            if (!File.Exists(filePath)) throw new FileNotFoundException($"Localization file not found: {filePath}", filePath);

            var json = File.ReadAllText(filePath);
            LoadJson(languageCode, json);
        }

        /// <summary>
        /// Parses a raw JSON string and registers its keys under the specified language code.
        /// Nested JSON objects are flattened into dot-separated keys (e.g. "app.title").
        /// </summary>
        /// <param name="languageCode">The language identifier.</param>
        /// <param name="jsonContent">JSON content string.</param>
        public void LoadJson(string languageCode, string jsonContent)
        {
            if (string.IsNullOrWhiteSpace(languageCode)) throw new ArgumentNullException(nameof(languageCode));
            if (string.IsNullOrWhiteSpace(jsonContent)) return;

            var langKey = languageCode.Trim().ToLowerInvariant();
            var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            using (var doc = JsonDocument.Parse(jsonContent))
            {
                FlattenElement(string.Empty, doc.RootElement, dict);
            }

            _languages[langKey] = dict;
            OnPropertyChanged("Item[]");
        }

        private static void FlattenElement(string prefix, JsonElement element, Dictionary<string, string> dict)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var prop in element.EnumerateObject())
                    {
                        var key = string.IsNullOrEmpty(prefix) ? prop.Name : $"{prefix}.{prop.Name}";
                        FlattenElement(key, prop.Value, dict);
                    }
                    break;

                case JsonValueKind.String:
                    dict[prefix] = element.GetString() ?? string.Empty;
                    break;

                case JsonValueKind.Number:
                    dict[prefix] = element.GetRawText();
                    break;

                case JsonValueKind.True:
                case JsonValueKind.False:
                    dict[prefix] = element.GetBoolean().ToString();
                    break;
            }
        }

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
