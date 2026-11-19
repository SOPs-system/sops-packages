using System;
using System.Windows.Data;
using System.Windows.Markup;

namespace Sop.Ui.Localization
{
    /// <summary>
    /// XAML markup extension providing live, reactive string localization from JSON files.
    /// Usage: <c>Text="{loc:Loc 'app.title'}"</c> or <c>Tag="{loc:Loc 'search.placeholder'}"</c>.
    /// Updates all UI elements automatically when <see cref="SopsLocalization.CurrentLanguage"/> changes.
    /// </summary>
    [MarkupExtensionReturnType(typeof(string))]
    public sealed class LocExtension : MarkupExtension
    {
        /// <summary>Gets or sets the translation key.</summary>
        public string Key { get; set; } = string.Empty;

        /// <summary>Gets or sets an optional string format (e.g. "{0}:").</summary>
        public string? StringFormat { get; set; }

        public LocExtension()
        {
        }

        public LocExtension(string key)
        {
            Key = key;
        }

        public override object ProvideValue(IServiceProvider serviceProvider)
        {
            if (string.IsNullOrWhiteSpace(Key))
            {
                return string.Empty;
            }

            // Create a dynamic OneWay binding to SopsLocalization.Instance[Key]
            var binding = new Binding($"[{Key}]")
            {
                Source = SopsLocalization.Instance,
                Mode = BindingMode.OneWay
            };

            if (!string.IsNullOrEmpty(StringFormat))
            {
                binding.StringFormat = StringFormat;
            }

            return binding.ProvideValue(serviceProvider);
        }
    }
}
