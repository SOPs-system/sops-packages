using System;
using System.Net.Mail;

namespace Sops.Mail.Models
{
    /// <summary>
    /// Represents an email address and an optional display name.
    /// </summary>
    public sealed class MailRecipient : IEquatable<MailRecipient>
    {
        /// <summary>
        /// Gets the normalized email address.
        /// </summary>
        public string Address { get; }

        /// <summary>
        /// Gets the optional display name.
        /// </summary>
        public string? DisplayName { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MailRecipient"/> class.
        /// </summary>
        /// <param name="address">The email address.</param>
        /// <param name="displayName">Optional display name associated with the address.</param>
        /// <exception cref="ArgumentException">Thrown when the address is null, whitespace, or an invalid format.</exception>
        public MailRecipient(string address, string? displayName = null)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("Email address cannot be null or whitespace.", nameof(address));
            }

            // Prevent CRLF injection attacks
            if (address.IndexOf('\r') >= 0 || address.IndexOf('\n') >= 0)
            {
                throw new ArgumentException("Email address cannot contain carriage return or newline characters.", nameof(address));
            }

            string trimmed = address.Trim();

            try
            {
                var parsed = new MailAddress(trimmed);
                Address = parsed.Address;
            }
            catch (Exception ex) when (ex is FormatException || ex is ArgumentException)
            {
                throw new ArgumentException($"'{address}' is not a valid email address.", nameof(address), ex);
            }

            if (!string.IsNullOrWhiteSpace(displayName))
            {
                if (displayName!.IndexOf('\r') >= 0 || displayName!.IndexOf('\n') >= 0)
                {
                    throw new ArgumentException("Display name cannot contain carriage return or newline characters.", nameof(displayName));
                }

                DisplayName = displayName!.Trim();
            }
            else
            {
                DisplayName = null;
            }
        }

        /// <summary>
        /// Implicitly converts a string email address to a <see cref="MailRecipient"/>.
        /// </summary>
        /// <param name="address">The email address string.</param>
        public static implicit operator MailRecipient(string address) => new MailRecipient(address);

        /// <inheritdoc />
        public bool Equals(MailRecipient? other)
        {
            if (other is null) return false;
            if (ReferenceEquals(this, other)) return true;

            return string.Equals(Address, other.Address, StringComparison.OrdinalIgnoreCase) &&
                   string.Equals(DisplayName, other.DisplayName, StringComparison.Ordinal);
        }

        /// <inheritdoc />
        public override bool Equals(object? obj) => Equals(obj as MailRecipient);

        /// <inheritdoc />
        public override int GetHashCode()
        {
            unchecked
            {
                int hash = StringComparer.OrdinalIgnoreCase.GetHashCode(Address);
                if (DisplayName != null)
                {
                    hash = (hash * 397) ^ StringComparer.Ordinal.GetHashCode(DisplayName);
                }
                return hash;
            }
        }

        /// <summary>
        /// Determines whether two recipient instances are equal.
        /// </summary>
        public static bool operator ==(MailRecipient? left, MailRecipient? right)
        {
            if (left is null) return right is null;
            return left.Equals(right);
        }

        /// <summary>
        /// Determines whether two recipient instances are not equal.
        /// </summary>
        public static bool operator !=(MailRecipient? left, MailRecipient? right) => !(left == right);

        /// <inheritdoc />
        public override string ToString()
        {
            return string.IsNullOrEmpty(DisplayName)
                ? Address
                : $"{DisplayName} <{Address}>";
        }
    }
}
