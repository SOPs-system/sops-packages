using System.Windows;
using System.Windows.Markup;

// Default control styles live in Themes/Generic.xaml (no per-OS theme files).
[assembly: ThemeInfo(ResourceDictionaryLocation.None, ResourceDictionaryLocation.SourceAssembly)]

// One XAML namespace for the whole design system:
//   xmlns:ui="https://sops.dev/ui"
[assembly: XmlnsDefinition("https://sops.dev/ui", "Sop.Ui")]
[assembly: XmlnsDefinition("https://sops.dev/ui", "Sop.Ui.Controls")]
[assembly: XmlnsPrefix("https://sops.dev/ui", "ui")]
