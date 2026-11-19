using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Xunit;
using Xunit.Abstractions;

namespace Sop.Ui.Tests
{
    /// <summary>
    /// Visual regression: run the gallery in capture mode and compare every
    /// PNG against the approved baseline in Visual/baseline.
    ///
    /// - No baseline yet → the capture is copied to Visual/actual and the
    ///   test passes with a note (nothing to compare against).
    /// - SOP_UPDATE_BASELINE=1 → the capture becomes the new baseline
    ///   (scripts/visual.ps1 -Update). Review the PNGs before committing them.
    /// - Otherwise a diff image is written to Visual/actual and the test fails
    ///   when more than the tolerance of pixels changed.
    /// </summary>
    public class VisualTests
    {
        private static readonly string[] Names =
        {
            "gallery-light-ltr", "gallery-dark-ltr", "gallery-light-rtl", "gallery-dark-rtl",
            "modal-light-ltr", "modal-dark-ltr", "modal-light-rtl",
        };

        private const int ChannelTolerance = 12;
        private const double MaxChangedFraction = 0.002; // 0.2% of pixels

        private readonly ITestOutputHelper _output;

        public VisualTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public void Gallery_matches_the_approved_baselines_in_every_mode()
        {
            var exe = Path.Combine(AppContext.BaseDirectory, "Sop.Gallery.exe");
            Assert.True(File.Exists(exe), "Sop.Gallery.exe not found next to the tests: " + exe);

            var captureDir = Path.Combine(Path.GetTempPath(), "sop-visual-" + Guid.NewGuid().ToString("N"));
            var process = Process.Start(new ProcessStartInfo(exe, "--capture \"" + captureDir + "\"") { UseShellExecute = false });
            Assert.NotNull(process);
            Assert.True(process!.WaitForExit(120_000), "gallery capture timed out");
            var errorFile = Path.Combine(captureDir, "capture-error.txt");
            Assert.True(process.ExitCode == 0, "gallery capture failed: " + (File.Exists(errorFile) ? File.ReadAllText(errorFile) : "exit " + process.ExitCode));

            var baselineDir = Path.Combine(Repo.Root, "tests", "Sop.Ui.Tests", "Visual", "baseline");
            var actualDir = Path.Combine(Repo.Root, "tests", "Sop.Ui.Tests", "Visual", "actual");
            Directory.CreateDirectory(baselineDir);
            Directory.CreateDirectory(actualDir);
            var update = Environment.GetEnvironmentVariable("SOP_UPDATE_BASELINE") == "1";

            var failures = new List<string>();
            foreach (var name in Names)
            {
                var actual = Path.Combine(captureDir, name + ".png");
                Assert.True(File.Exists(actual), "capture missing: " + name);
                var baseline = Path.Combine(baselineDir, name + ".png");
                File.Copy(actual, Path.Combine(actualDir, name + ".png"), overwrite: true);

                if (update || !File.Exists(baseline))
                {
                    File.Copy(actual, baseline, overwrite: true);
                    _output.WriteLine((update ? "updated" : "no baseline yet — recorded") + " baseline: " + name);
                    continue;
                }

                var result = Compare(baseline, actual, Path.Combine(actualDir, name + ".diff.png"));
                _output.WriteLine($"{name}: {result.changed} of {result.total} pixels differ ({result.fraction:P3})");
                if (result.sizeMismatch) failures.Add($"{name}: size changed ({result.detail})");
                else if (result.fraction > MaxChangedFraction) failures.Add($"{name}: {result.fraction:P2} of pixels changed (limit {MaxChangedFraction:P1}); see Visual/actual/{name}.diff.png");
            }

            Directory.Delete(captureDir, recursive: true);
            Assert.True(failures.Count == 0, "Visual regressions:\n" + string.Join("\n", failures) +
                "\nIf the change is intended, approve it with scripts/visual.ps1 -Update and commit the baseline.");
        }

        private static (bool sizeMismatch, string detail, int changed, int total, double fraction) Compare(string baselinePath, string actualPath, string diffPath)
        {
            var a = Load(baselinePath);
            var b = Load(actualPath);
            if (a.PixelWidth != b.PixelWidth || a.PixelHeight != b.PixelHeight)
                return (true, $"{a.PixelWidth}x{a.PixelHeight} → {b.PixelWidth}x{b.PixelHeight}", 0, 0, 1);

            var width = a.PixelWidth;
            var height = a.PixelHeight;
            var stride = width * 4;
            var pa = new byte[stride * height];
            var pb = new byte[stride * height];
            a.CopyPixels(pa, stride, 0);
            b.CopyPixels(pb, stride, 0);
            var diff = new byte[stride * height];
            var changed = 0;
            for (var i = 0; i < pa.Length; i += 4)
            {
                var d = Math.Max(Math.Max(Math.Abs(pa[i] - pb[i]), Math.Abs(pa[i + 1] - pb[i + 1])), Math.Abs(pa[i + 2] - pb[i + 2]));
                var hit = d > ChannelTolerance;
                if (hit) changed++;
                // Diff image: faded actual, changed pixels in full red.
                diff[i] = hit ? (byte)0 : (byte)(pb[i] / 3 + 170);
                diff[i + 1] = hit ? (byte)0 : (byte)(pb[i + 1] / 3 + 170);
                diff[i + 2] = hit ? (byte)255 : (byte)(pb[i + 2] / 3 + 170);
                diff[i + 3] = 255;
            }
            var total = width * height;
            if (changed > 0)
            {
                var image = BitmapSource.Create(width, height, 96, 96, PixelFormats.Bgra32, null, diff, stride);
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));
                using var stream = File.Create(diffPath);
                encoder.Save(stream);
            }
            return (false, string.Empty, changed, total, (double)changed / total);
        }

        private static BitmapSource Load(string path)
        {
            using var stream = File.OpenRead(path);
            var decoder = new PngBitmapDecoder(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
            var frame = decoder.Frames[0];
            return frame.Format == PixelFormats.Bgra32 ? frame : new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
        }
    }
}
