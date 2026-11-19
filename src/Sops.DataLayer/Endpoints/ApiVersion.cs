using System;

namespace Sops.DataLayer.Endpoints
{
    /// <summary>
    /// Represents an API version for contract versioning and negotiation.
    /// </summary>
    public sealed class ApiVersion : IEquatable<ApiVersion>, IComparable<ApiVersion>
    {
        public static readonly ApiVersion V1 = new ApiVersion(1, 0);
        public static readonly ApiVersion V2 = new ApiVersion(2, 0);

        public int Major { get; }
        public int Minor { get; }

        public ApiVersion(int major, int minor = 0)
        {
            if (major < 0) throw new ArgumentOutOfRangeException(nameof(major));
            if (minor < 0) throw new ArgumentOutOfRangeException(nameof(minor));
            Major = major;
            Minor = minor;
        }

        public override string ToString() => $"v{Major}.{Minor}";

        public bool Equals(ApiVersion? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;
            return Major == other.Major && Minor == other.Minor;
        }

        public override bool Equals(object? obj) => Equals(obj as ApiVersion);

        public override int GetHashCode() => (Major * 397) ^ Minor;

        public int CompareTo(ApiVersion? other)
        {
            if (other is null) return 1;
            int majorComparison = Major.CompareTo(other.Major);
            if (majorComparison != 0) return majorComparison;
            return Minor.CompareTo(other.Minor);
        }

        public static bool operator ==(ApiVersion? left, ApiVersion? right) => Equals(left, right);
        public static bool operator !=(ApiVersion? left, ApiVersion? right) => !Equals(left, right);
    }
}
