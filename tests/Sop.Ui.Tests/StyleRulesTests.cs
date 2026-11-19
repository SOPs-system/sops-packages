using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using Xunit;

namespace Sop.Ui.Tests
{
    /// <summary>
    /// The WPF twin of scripts/check-style-rules.mts: a visual value belongs
    /// in the token files, never in a control template, a screen, or code.
    /// </summary>
    public class StyleRulesTests
    {
        private static readonly HashSet<string> Allowed = new HashSet<string>
        {
            "src/Sop.Ui/Themes/Tokens.Light.xaml",
            "src/Sop.Ui/Themes/Tokens.Dark.xaml",
            // Print surfaces — receipts and labels. Scanners need literal black
            // on white and a thermal head has no theme: the same exception the
            // web app's check-style-rules.mts makes for them.
            "src/Sop.Features/Transactions/ReceiptTicket.cs",
            "src/Sop.Features/Common/LabelSheet.cs",
            "src/Sop.Features/Common/BarcodeView.cs",
            // The purchase order is a printed A5 page — paper, not a theme.
            "src/Sop.Features/Inventory/PurchaseOrderSheet.cs",
        };

        private static readonly (string name, Regex test)[] Rules =
        {
            ("raw hex colour", new Regex(@"#[0-9A-Fa-f]{3,8}\b")),
            ("named WPF brush", new Regex(@"\bBrushes\.[A-Z]\w+")),
            ("named WPF colour", new Regex(@"\bColors\.[A-Z]\w+")),
            ("Color.FromRgb / FromArgb", new Regex(@"\bColor\.From[AR]\w*\(")),
            ("literal FontSize (use a Text.* token)", new Regex(@"FontSize\s*=\s*""?\d")),
            ("literal FontFamily (use a Font.* token)", new Regex(@"FontFamily\s*=\s*""[^{]")),
        };

        [Fact]
        public void No_visual_values_outside_the_token_files()
        {
            var failures = new List<string>();
            foreach (var file in Repo.SourceFiles(".xaml", ".cs"))
            {
                var rel = Repo.Relative(file);
                if (Allowed.Contains(rel)) continue;
                var lines = File.ReadAllLines(file);
                for (var i = 0; i < lines.Length; i++)
                {
                    var code = StripComments(lines[i]);
                    foreach (var (name, test) in Rules)
                    {
                        if (test.IsMatch(code)) failures.Add($"{rel}:{i + 1}: {name}\n    {lines[i].Trim()}");
                    }
                }
            }
            Assert.True(failures.Count == 0,
                $"{failures.Count} style-rule violation(s). Values belong in src/Sop.Ui/Themes/Tokens.*.xaml.\n" + string.Join("\n", failures));
        }

        private static string StripComments(string line)
        {
            line = Regex.Replace(line, @"//.*$", string.Empty);
            line = Regex.Replace(line, @"<!--.*?-->", string.Empty);
            // A line inside a multi-line XML comment: crude but enough for
            // the token-name comments in the theme files.
            if (line.TrimStart().StartsWith("<!--") || line.TrimStart().StartsWith("-->")) return string.Empty;
            return line;
        }
    }
}
