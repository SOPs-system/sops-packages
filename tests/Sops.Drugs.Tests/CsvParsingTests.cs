using System.IO;
using System.Linq;
using System.Text;
using Sops.Drugs.Csv;
using Xunit;

namespace Sops.Drugs.Tests
{
    public class CsvParsingTests
    {
        [Fact]
        public void ReadRows_StandardCsv_ParsesCorrectly()
        {
            string csvContent = "id,name,price\n1,Panadol,40.0\n2,Congestal,35.0";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
            using var reader = new SopsCsvReader(stream);

            Assert.True(reader.ReadHeader());
            Assert.Equal(new[] { "id", "name", "price" }, reader.Header);

            var rows = reader.ReadRows().ToList();
            Assert.Equal(2, rows.Count);
            Assert.Equal("Panadol", rows[0][1]);
            Assert.Equal("40.0", rows[0][2]);
            Assert.Equal("Congestal", rows[1][1]);
        }

        [Fact]
        public void ReadRows_QuotedFieldsAndEscapedQuotes_HandlesRfc4180()
        {
            string csvContent = "id,name,description\n1,\"Augmentin 1 \"\"GM\"\"\",\"Antibiotic, broad spectrum\"\n2,\"Cataflam\",\"Anti-inflammatory\"";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
            using var reader = new SopsCsvReader(stream);

            reader.ReadHeader();
            var rows = reader.ReadRows().ToList();

            Assert.Equal("Augmentin 1 \"GM\"", rows[0][1]);
            Assert.Equal("Antibiotic, broad spectrum", rows[0][2]);
            Assert.Equal("Cataflam", rows[1][1]);
        }

        [Fact]
        public void ReadRows_ArabicCharactersAndUtf8Bom_PreservesTextCorrectly()
        {
            // Include UTF-8 BOM
            byte[] bom = new byte[] { 0xEF, 0xBB, 0xBF };
            string csvContent = "id,name_en,name_ar\n1,Panadol,بانادول إكسترا\n2,Congestal,كونجيستال أقراص";
            byte[] body = Encoding.UTF8.GetBytes(csvContent);
            byte[] full = new byte[bom.Length + body.Length];
            Buffer.BlockCopy(bom, 0, full, 0, bom.Length);
            Buffer.BlockCopy(body, 0, full, bom.Length, body.Length);

            using var stream = new MemoryStream(full);
            using var reader = new SopsCsvReader(stream);

            Assert.True(reader.ReadHeader());
            Assert.Equal("id", reader.Header[0]);

            var records = reader.ReadRecords().ToList();
            Assert.Equal("بانادول إكسترا", records[0]["name_ar"]);
            Assert.Equal("كونجيستال أقراص", records[1]["name_ar"]);
        }

        [Fact]
        public void ReadRows_MultilineQuotedFields_PreservesNewlines()
        {
            string csvContent = "id,notes\n1,\"Line 1\nLine 2\"\n2,SingleLine";
            using var stream = new MemoryStream(Encoding.UTF8.GetBytes(csvContent));
            using var reader = new SopsCsvReader(stream);

            reader.ReadHeader();
            var rows = reader.ReadRows().ToList();

            Assert.Equal("Line 1\nLine 2", rows[0][1]);
            Assert.Equal("SingleLine", rows[1][1]);
        }

        [Fact]
        public void Writer_ProducesValidEscapedCsv()
        {
            using var ms = new MemoryStream();
            using (var writer = new SopsCsvWriter(ms))
            {
                writer.WriteRow("id", "title", "value");
                writer.WriteRow("1", "Hello, World!", "42");
                writer.WriteRow("2", "He said \"Yes\"", "99");
            }

            string result = Encoding.UTF8.GetString(ms.ToArray());
            Assert.Contains("\"Hello, World!\"", result);
            Assert.Contains("\"He said \"\"Yes\"\"\"", result);
        }
    }
}
