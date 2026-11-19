using System;
using System.IO;
using Sop.Ui;
using Sop.Ui.Localization;
using Xunit;

namespace Sop.Ui.Tests
{
    public class LocalizationTests
    {
        [Fact]
        public void LoadJson_FlattensNestedKeysCorrectly()
        {
            var loc = new SopsLocalization();
            const string json = @"
            {
                ""app"": {
                    ""title"": ""Pharmacy POS"",
                    ""version"": 10
                },
                ""flat_key"": ""Hello World""
            }";

            loc.LoadJson("en", json);
            loc.CurrentLanguage = "en";

            Assert.Equal("Pharmacy POS", loc["app.title"]);
            Assert.Equal("10", loc["app.version"]);
            Assert.Equal("Hello World", loc["flat_key"]);
        }

        [Fact]
        public void Get_FallsBackToEnglish_WhenKeyMissingInTargetLanguage()
        {
            var loc = new SopsLocalization();
            loc.LoadJson("en", @"{ ""greeting"": ""Welcome"", ""only_en"": ""English only"" }");
            loc.LoadJson("ar", @"{ ""greeting"": ""مرحبا"" }");

            loc.CurrentLanguage = "ar";

            Assert.Equal("مرحبا", loc["greeting"]);
            Assert.Equal("English only", loc["only_en"]);
            Assert.Equal("missing.key", loc["missing.key"]);
        }

        [Fact]
        public void LoadFromFile_LoadsTranslationsFromExternalFilePath()
        {
            var tempFile = Path.GetTempFileName();
            try
            {
                File.WriteAllText(tempFile, @"{ ""btn"": { ""save"": ""Save Record"" } }");

                var loc = new SopsLocalization();
                loc.LoadFromFile("en", tempFile);
                loc.CurrentLanguage = "en";

                Assert.Equal("Save Record", loc["btn.save"]);
            }
            finally
            {
                if (File.Exists(tempFile)) File.Delete(tempFile);
            }
        }

        [Fact]
        public void LoadFromDirectory_LoadsAllMatchingLanguageFiles()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "sops_loc_" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                File.WriteAllText(Path.Combine(tempDir, "en.json"), @"{ ""title"": ""Title EN"" }");
                File.WriteAllText(Path.Combine(tempDir, "ar.json"), @"{ ""title"": ""العنوان"" }");

                var loc = new SopsLocalization();
                int count = loc.LoadFromDirectory(tempDir);

                Assert.Equal(2, count);

                loc.CurrentLanguage = "en";
                Assert.Equal("Title EN", loc["title"]);

                loc.CurrentLanguage = "ar";
                Assert.Equal("العنوان", loc["title"]);
            }
            finally
            {
                if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true);
            }
        }

        [Fact]
        public void ChangingLanguage_FiresPropertyChangedForItemIndexer()
        {
            var loc = new SopsLocalization();
            loc.LoadJson("en", @"{ ""k"": ""v1"" }");
            loc.LoadJson("ar", @"{ ""k"": ""v2"" }");

            bool itemNotified = false;
            loc.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == "Item[]")
                {
                    itemNotified = true;
                }
            };

            loc.CurrentLanguage = "ar";
            Assert.True(itemNotified, "Item[] indexer property change should be raised for dynamic WPF bindings");
        }

        [Fact]
        public void ChangingLanguageToAr_SyncsThemeIsRtl()
        {
            var loc = new SopsLocalization { SyncWithThemeDirection = true };
            loc.CurrentLanguage = "en";
            Assert.False(Theme.IsRtl);

            loc.CurrentLanguage = "ar";
            Assert.True(Theme.IsRtl);

            loc.CurrentLanguage = "en";
            Assert.False(Theme.IsRtl);
        }
    }
}
