using System;
using System.Collections.Generic;

namespace Sops.DataLayer.Models.Responses
{
    /// <summary>
    /// Generic pagination wrapper for paged catalog and transaction queries.
    /// </summary>
    /// <typeparam name="T">Item entity type.</typeparam>
    public sealed class DataPage<T>
    {
        public IReadOnlyList<T> Items { get; }
        public int Total { get; }
        public int Offset { get; }
        public int Limit { get; }
        public bool HasMore => Offset + Items.Count < Total;

        public DataPage(IReadOnlyList<T>? items, int total, int offset = 0, int limit = 25)
        {
            Items = items ?? Array.Empty<T>();
            Total = Math.Max(0, total);
            Offset = Math.Max(0, offset);
            Limit = Math.Max(1, limit);
        }

        public static DataPage<T> Empty(int offset = 0, int limit = 25) =>
            new DataPage<T>(Array.Empty<T>(), 0, offset, limit);
    }
}
