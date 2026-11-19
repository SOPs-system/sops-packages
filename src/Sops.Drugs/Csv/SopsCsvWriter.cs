using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Sops.Drugs.Csv
{
    /// <summary>
    /// Writes CSV data conforming to RFC-4180 with proper quoting and UTF-8 encoding.
    /// </summary>
    public class SopsCsvWriter : IDisposable
    {
        private readonly TextWriter _writer;
        private readonly bool _ownsWriter;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="SopsCsvWriter"/> class writing to a stream.
        /// </summary>
        public SopsCsvWriter(Stream stream, Encoding? encoding = null)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            _writer = new StreamWriter(stream, encoding ?? new UTF8Encoding(false), 8192);
            _ownsWriter = true;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SopsCsvWriter"/> class writing to a TextWriter.
        /// </summary>
        public SopsCsvWriter(TextWriter writer)
        {
            _writer = writer ?? throw new ArgumentNullException(nameof(writer));
            _ownsWriter = false;
        }

        /// <summary>
        /// Writes a single CSV record row.
        /// </summary>
        /// <param name="fields">The field values.</param>
        public void WriteRow(params string?[] fields)
        {
            WriteRow((IEnumerable<string?>)fields);
        }

        /// <summary>
        /// Writes a single CSV record row.
        /// </summary>
        /// <param name="fields">The field values.</param>
        public void WriteRow(IEnumerable<string?> fields)
        {
            if (fields == null) return;

            bool first = true;
            foreach (var field in fields)
            {
                if (!first)
                {
                    _writer.Write(',');
                }

                _writer.Write(EscapeField(field));
                first = false;
            }

            _writer.WriteLine();
        }

        private static string EscapeField(string? field)
        {
            if (string.IsNullOrEmpty(field))
                return string.Empty;

            bool mustQuote = field!.IndexOfAny(new[] { ',', '"', '\r', '\n' }) != -1;
            if (!mustQuote)
            {
                return field;
            }

            return "\"" + field.Replace("\"", "\"\"") + "\"";
        }

        /// <inheritdoc />
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>Releases resources.</summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing && _ownsWriter)
                {
                    _writer.Flush();
                    _writer.Dispose();
                }
                _disposed = true;
            }
        }
    }
}
