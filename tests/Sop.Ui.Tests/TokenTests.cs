using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Sop.Ui.Tests
{
    public class TokenTests
    {
        private static readonly XNamespace X = "http://schemas.microsoft.com/winfx/2006/xaml";

        private static HashSet<string> Keys(string relativePath)
        {
            var doc = XDocument.Load(Path.Combine(Repo.Root, relativePath));
            return doc.Descendants()
                .Select(e => e.Attribute(X + "Key")?.Value)
                .Where(k => k != null)
                .Select(k => k!)
                .ToHashSet();
        }

        [Fact]
        public void Light_and_dark_define_exactly_the_same_keys()
        {
            var light = Keys("src/Sop.Ui/Themes/Tokens.Light.xaml");
            var dark = Keys("src/Sop.Ui/Themes/Tokens.Dark.xaml");
            var missingInDark = light.Except(dark).OrderBy(k => k).ToList();
            var missingInLight = dark.Except(light).OrderBy(k => k).ToList();
            Assert.True(missingInDark.Count == 0 && missingInLight.Count == 0,
                "Theme.Mode swaps the whole dictionary, so both must define every key.\n" +
                $"Missing in dark: {string.Join(", ", missingInDark)}\n" +
                $"Missing in light: {string.Join(", ", missingInLight)}");
        }

        [Fact]
        public void Every_resource_reference_resolves_to_a_defined_key()
        {
            // WPF silently ignores a missing DynamicResource — a typo would just
            // render unstyled. Catch it statically instead.
            var defined = new HashSet<string>();
            foreach (var file in Repo.SourceFiles(".xaml")) defined.UnionWith(Keys(Repo.Relative(file)));

            var reference = new Regex(@"\{(?:Dynamic|Static)Resource\s+([A-Za-z0-9_.]+)\}");
            var codeLookup = new Regex(@"(?:TryFindResource|FindResource|Resources\[)\s*\(?""([A-Za-z0-9_.]+)""");
            var failures = new List<string>();
            foreach (var file in Repo.SourceFiles(".xaml", ".cs"))
            {
                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    var matches = reference.Matches(lines[i]).Cast<Match>().Concat(codeLookup.Matches(lines[i]).Cast<Match>());
                    foreach (var match in matches)
                    {
                        var key = match.Groups[1].Value;
                        if (!defined.Contains(key)) failures.Add($"{Repo.Relative(file)}:{i + 1}: '{key}' is not defined in any dictionary");
                    }
                }
            }
            Assert.True(failures.Count == 0, string.Join("\n", failures));
        }

        [Fact]
        public void Token_names_are_semantic_not_hues()
        {
            // Brush.* keys must not be named after a colour; the primitives
            // (Color.Teal500) are the only hue-named entries, and only Colors.
            var light = Keys("src/Sop.Ui/Themes/Tokens.Light.xaml");
            var hue = new Regex(@"^Brush\.(Teal|Red|Green|Blue|Orange|Gray|Grey|Slate|Emerald)", RegexOptions.IgnoreCase);
            var offenders = light.Where(k => hue.IsMatch(k)).ToList();
            Assert.True(offenders.Count == 0, "Hue-named brushes: " + string.Join(", ", offenders));
        }
    }
}
