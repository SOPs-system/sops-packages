using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Sops.Drugs.Csv
{
    /// <summary>
    /// Resilient, high-performance streaming RFC-4180 compliant CSV reader.
    /// Fully supports UTF-8, Arabic characters, escaped quotes, multiline text fields, and malformed rows.
    /// </summary>
    public class SopsCsvReader : IDisposable
    {
        private readonly TextReader _reader;
        private readonly bool _ownsReader;
        private string[]? _header;
        private Dictionary<string, int>? _headerIndexMap;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="SopsCsvReader"/> class from a text stream.
        /// </summary>
        /// <param name="stream">The stream to read.</param>
        /// <param name="encoding">The character encoding (defaults to UTF-8).</param>
        public SopsCsvReader(Stream stream, Encoding? encoding = null)
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            _reader = new StreamReader(stream, encoding ?? Encoding.UTF8, true, 8192);
            _ownsReader = true;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="SopsCsvReader"/> class from a TextReader.
        /// </summary>
        public SopsCsvReader(TextReader reader)
        {
            _reader = reader ?? throw new ArgumentNullException(nameof(reader));
            _ownsReader = false;
        }

        /// <summary>
        /// Gets the parsed header column names.
        /// </summary>
        public IReadOnlyList<string> Header => _header ?? Array.Empty<string>();

        /// <summary>
        /// Reads and initializes the header row.
        /// </summary>
        public bool ReadHeader()
        {
            var row = ReadRow();
            if (row == null) return false;

            _header = row;
            _headerIndexMap = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < row.Length; i++)
            {
                string col = row[i].Trim().TrimStart('\uFEFF'); // Strip UTF-8 BOM if present
                if (!string.IsNullOrWhiteSpace(col) && !_headerIndexMap.ContainsKey(col))
                {
                    _headerIndexMap[col] = i;
                }
            }

            return true;
        }

        /// <summary>
        /// Enumerates all remaining records as string arrays.
        /// </summary>
        public IEnumerable<string[]> ReadRows()
        {
            string[]? row;
            while ((row = ReadRow()) != null)
            {
                // Skip completely empty lines
                if (row.Length == 1 && string.IsNullOrWhiteSpace(row[0]))
                    continue;

                yield return row;
            }
        }

        /// <summary>
        /// Enumerates all remaining records as dictionaries mapping column names to values.
        /// </summary>
        public IEnumerable<Dictionary<string, string>> ReadRecords()
        {
            if (_headerIndexMap == null)
            {
                ReadHeader();
            }

            string[]? row;
            while ((row = ReadRow()) != null)
            {
                if (row.Length == 1 && string.IsNullOrWhiteSpace(row[0]))
                    continue;

                var dict = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (_headerIndexMap != null)
                {
                    foreach (var pair in _headerIndexMap)
                    {
                        string val = pair.Value < row.Length ? row[pair.Value] : string.Empty;
                        dict[pair.Key] = val;
                    }
                }

                yield return dict;
            }
        }

        /// <summary>
        /// Reads a single row conforming to RFC-4180 rules.
        /// </summary>
        public string[]? ReadRow()
        {
            var fields = new List<string>();
            var currentField = new StringBuilder();
            bool inQuotes = false;
            int nextChar;

            while ((nextChar = _reader.Read()) != -1)
            {
                char c = (char)nextChar;

                if (inQuotes)
                {
                    if (c == '"')
                    {
                        int peek = _reader.Peek();
                        if (peek == '"')
                        {
                            // Escaped quote: "" -> "
                            _reader.Read();
                            currentField.Append('"');
                        }
                        else
                        {
                            // Closing quote
                            inQuotes = false;
                        }
                    }
                    else
                    {
                        currentField.Append(c);
                    }
                }
                else
                {
                    if (c == '"')
                    {
                        inQuotes = true;
                    }
                    else if (c == ',')
                    {
                        fields.Add(currentField.ToString());
                        currentField.Clear();
                    }
                    else if (c == '\r')
                    {
                        if (_reader.Peek() == '\n')
                        {
                            _reader.Read();
                        }
                        fields.Add(currentField.ToString());
                        return fields.ToArray();
                    }
                    else if (c == '\n')
                    {
                        fields.Add(currentField.ToString());
                        return fields.ToArray();
                    }
                    else
                    {
                        currentField.Append(c);
                    }
                }
            }

            if (fields.Count > 0 || currentField.Length > 0)
            {
                fields.Add(currentField.ToString());
                return fields.ToArray();
            }

            return null;
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
                if (disposing && _ownsReader)
                {
                    _reader.Dispose();
                }
                _disposed = true;
            }
        }
    }
}
