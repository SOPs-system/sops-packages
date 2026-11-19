using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Sop.Ui.Controls
{
    public enum BarcodeSymbology { Ean13, Ean8, Code128 }

    public sealed class BarcodeBar
    {
        public double X { get; }
        public double Width { get; }
        public double Height { get; }

        public BarcodeBar(double x, double width, double height)
        {
            X = x;
            Width = width;
            Height = height;
        }
    }

    public sealed class BarcodeDigit
    {
        public double X { get; }
        public string Value { get; }

        public BarcodeDigit(double x, string value)
        {
            X = x;
            Value = value;
        }
    }

    public sealed class BarcodeGeometry
    {
        public BarcodeSymbology Symbology { get; set; }
        public string Digits { get; set; } = string.Empty;
        public double Width { get; set; }
        public double Height { get; set; }
        public IReadOnlyList<BarcodeBar> Bars { get; set; } = Array.Empty<BarcodeBar>();
        public IReadOnlyList<BarcodeDigit> Texts { get; set; } = Array.Empty<BarcodeDigit>();
        public double FontSize { get; set; }
        public double TextBaseline { get; set; }
    }

    public sealed class PrintableCode
    {
        public string Value { get; }
        public BarcodeSymbology Symbology { get; }

        public PrintableCode(string value, BarcodeSymbology symbology)
        {
            Value = value;
            Symbology = symbology;
        }
    }

    /// <summary>
    /// Vector barcode geometry generator: EAN-13 / EAN-8 for retail items, Code 128 for general alphanumeric SKUs.
    /// Computes precise bar coordinates and check digits without rasterization.
    /// </summary>
    public static class Barcode
    {
        private static readonly string[] LCodes =
        {
            "0001101", "0011001", "0010011", "0111101", "0100011",
            "0110001", "0101111", "0111011", "0110111", "0001011"
        };

        private static readonly string[] GCodes =
        {
            "0100111", "0110011", "0011011", "0100001", "0011101",
            "0111001", "0000101", "0010001", "0001001", "0010111"
        };

        private static readonly string[] RCodes =
        {
            "1110010", "1100110", "1101100", "1000010", "1011100",
            "1001110", "1010000", "1000100", "1001000", "1110100"
        };

        private static readonly string[] Parity =
        {
            "LLLLLL", "LLGLGG", "LLGGLG", "LLGGGL", "LGLLGG",
            "LGGLLG", "LGGGLL", "LGLGLG", "LGLGGL", "LGGLGL"
        };

        private const string Guard = "101";
        private const string Center = "01010";

        private const double BarHeightNominal = 68;
        private const double GuardOvershoot = 5;
        private const double CaptionBlock = 14;
        private const double FontSizeNominal = 9;
        private const double DigitWidth = 7;
        private const double MinBarHeight = 12;

        private static readonly string[] Code128Table =
        {
            "212222", "222122", "222221", "121223", "121322", "131222", "122213", "122312", "132212", "221213",
            "221312", "231212", "112232", "122132", "122231", "113222", "123122", "123221", "223211", "221132",
            "221231", "213212", "223112", "312131", "311222", "321122", "321221", "312212", "322112", "322211",
            "212123", "212321", "232121", "111323", "131123", "131321", "112313", "132113", "132311", "211313",
            "231113", "231311", "112133", "112331", "132131", "113123", "113321", "133121", "313121", "211331",
            "231131", "213113", "213311", "213131", "311123", "311321", "331121", "312113", "312311", "332111",
            "314111", "221411", "431111", "111224", "111422", "121124", "121421", "141122", "141221", "112214",
            "112412", "122114", "122411", "142112", "142211", "241211", "221114", "413111", "241112", "134111",
            "111242", "121142", "121241", "114212", "124112", "124211", "411212", "421112", "421211", "212141",
            "214121", "412121", "111143", "111341", "131141", "114113", "114311", "411113", "411311", "113141",
            "114131", "311141", "411131", "211412", "211214", "211232", "2331112"
        };

        private const int Code128StartB = 104;
        private const int Code128Stop = 106;
        private const double Code128Quiet = 10;
        private static readonly Regex Code128Chars = new Regex(@"^[\x20-\x7e]+$");
        private static readonly Regex AllDigits = new Regex(@"^\d+$");
        private static readonly Regex Separators = new Regex(@"[\s-]");

        public static string CheckDigit(string body)
        {
            var sum = 0;
            var weight = 3;
            for (var i = body.Length - 1; i >= 0; i--)
            {
                sum += (body[i] - '0') * weight;
                weight = weight == 3 ? 1 : 3;
            }
            return ((10 - (sum % 10)) % 10).ToString();
        }

        public static PrintableCode? GetPrintableCode(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return null;
            var trimmed = raw!.Trim();
            var digitsOnly = Separators.Replace(trimmed, "");

            if (AllDigits.IsMatch(digitsOnly))
            {
                if (digitsOnly.Length == 12)
                {
                    return new PrintableCode(digitsOnly + CheckDigit(digitsOnly), BarcodeSymbology.Ean13);
                }
                if (digitsOnly.Length == 13)
                {
                    if (CheckDigit(digitsOnly.Substring(0, 12)) == digitsOnly.Substring(12, 1))
                        return new PrintableCode(digitsOnly, BarcodeSymbology.Ean13);
                }
                if (digitsOnly.Length == 7)
                {
                    return new PrintableCode(digitsOnly + CheckDigit(digitsOnly), BarcodeSymbology.Ean8);
                }
                if (digitsOnly.Length == 8)
                {
                    if (CheckDigit(digitsOnly.Substring(0, 7)) == digitsOnly.Substring(7, 1))
                        return new PrintableCode(digitsOnly, BarcodeSymbology.Ean8);
                }
            }

            if (trimmed.Length >= 1 && trimmed.Length <= 80 && Code128Chars.IsMatch(trimmed))
            {
                return new PrintableCode(trimmed, BarcodeSymbology.Code128);
            }

            return null;
        }

        public static BarcodeGeometry? Geometry(string? value, double? barHeight = null)
        {
            var printable = GetPrintableCode(value);
            if (printable == null) return null;
            var height = Math.Max(MinBarHeight, barHeight ?? BarHeightNominal);
            return printable.Symbology == BarcodeSymbology.Code128
                ? Code128Geometry(printable.Value, height)
                : EanGeometry(printable.Value, height);
        }

        private static BarcodeGeometry Code128Geometry(string value, double barHeight)
        {
            var codes = new List<int> { Code128StartB };
            codes.AddRange(value.Select(ch => ch - 32));
            var checksum = codes.Select((code, index) => code * (index == 0 ? 1 : index)).Sum() % 103;
            codes.Add(checksum);
            codes.Add(Code128Stop);

            var bars = new List<BarcodeBar>();
            var x = Code128Quiet;
            foreach (var code in codes)
            {
                var pattern = Code128Table[code];
                for (var i = 0; i < pattern.Length; i++)
                {
                    var width = pattern[i] - '0';
                    if (i % 2 == 0) bars.Add(new BarcodeBar(x, width, barHeight));
                    x += width;
                }
            }

            var totalWidth = x + Code128Quiet;
            var fontSize = Math.Max(5, Math.Min(FontSizeNominal, (totalWidth - 4) / (0.62 * value.Length)));
            return new BarcodeGeometry
            {
                Symbology = BarcodeSymbology.Code128,
                Digits = value,
                Width = totalWidth,
                Height = barHeight + CaptionBlock,
                Bars = bars,
                Texts = new[] { new BarcodeDigit(totalWidth / 2, value) },
                FontSize = fontSize,
                TextBaseline = barHeight + FontSizeNominal + 2
            };
        }

        private static BarcodeGeometry EanGeometry(string digits, double barHeight)
        {
            var symbology = digits.Length == 13 ? BarcodeSymbology.Ean13 : BarcodeSymbology.Ean8;
            var guardHeight = barHeight + GuardOvershoot;
            var (quietStart, quietEnd) = symbology == BarcodeSymbology.Ean13 ? (11.0, 7.0) : (7.0, 7.0);
            var bars = new List<BarcodeBar>();
            var texts = new List<BarcodeDigit>();

            var leadingDigit = symbology == BarcodeSymbology.Ean13 ? digits.Substring(0, 1) : "";
            var body = symbology == BarcodeSymbology.Ean13 ? digits.Substring(1) : digits;
            var half = body.Length / 2;
            var parity = symbology == BarcodeSymbology.Ean13 ? Parity[leadingDigit[0] - '0'] : "LLLL";

            var x = quietStart;
            x = PushBars(bars, Guard, x, guardHeight);
            var leftStart = x;
            for (var i = 0; i < half; i++)
            {
                var digit = body[i] - '0';
                var bits = parity[i] == 'G' ? GCodes[digit] : LCodes[digit];
                x = PushBars(bars, bits, x, barHeight);
            }
            x = PushBars(bars, Center, x, guardHeight);
            var rightStart = x;
            for (var i = half; i < body.Length; i++)
            {
                var digit = body[i] - '0';
                x = PushBars(bars, RCodes[digit], x, barHeight);
            }
            x = PushBars(bars, Guard, x, guardHeight);

            if (leadingDigit != "") texts.Add(new BarcodeDigit(quietStart / 2, leadingDigit));
            for (var i = 0; i < body.Length; i++)
            {
                var group = i < half ? leftStart : rightStart;
                var index = i < half ? i : i - half;
                texts.Add(new BarcodeDigit(group + index * DigitWidth + DigitWidth / 2, body[i].ToString()));
            }

            return new BarcodeGeometry
            {
                Symbology = symbology,
                Digits = digits,
                Width = x + quietEnd,
                Height = barHeight + CaptionBlock,
                Bars = bars,
                Texts = texts,
                FontSize = FontSizeNominal,
                TextBaseline = barHeight + FontSizeNominal + 3
            };
        }

        private static double PushBars(List<BarcodeBar> bars, string bits, double x, double height)
        {
            var i = 0;
            while (i < bits.Length)
            {
                var bit = bits[i];
                var run = 0;
                while (i < bits.Length && bits[i] == bit)
                {
                    run++;
                    i++;
                }
                if (bit == '1')
                {
                    bars.Add(new BarcodeBar(x, run, height));
                }
                x += run;
            }
            return x;
        }
    }
}
