using System;

namespace Sops.WhatsApp.Models
{
    /// <summary>
    /// Represents a dynamic parameter substituted into a WhatsApp template component.
    /// </summary>
    public class WhatsAppTemplateParameter
    {
        /// <summary>
        /// Gets or sets the parameter type (e.g. "text", "currency", "date_time", "image", "document", "video").
        /// </summary>
        public string Type { get; set; } = "text";

        /// <summary>
        /// Gets or sets the text value when <see cref="Type"/> is "text".
        /// </summary>
        public string? Text { get; set; }

        /// <summary>
        /// Gets or sets the currency parameter payload when <see cref="Type"/> is "currency".
        /// </summary>
        public WhatsAppCurrencyParameter? Currency { get; set; }

        /// <summary>
        /// Gets or sets the date/time parameter payload when <see cref="Type"/> is "date_time".
        /// </summary>
        public WhatsAppDateTimeParameter? DateTime { get; set; }

        /// <summary>
        /// Gets or sets the media reference when <see cref="Type"/> is an image, document, or video parameter.
        /// </summary>
        public WhatsAppMediaReference? Media { get; set; }

        /// <summary>
        /// Creates a text parameter.
        /// </summary>
        /// <param name="text">The text value.</param>
        /// <returns>A new <see cref="WhatsAppTemplateParameter"/>.</returns>
        public static WhatsAppTemplateParameter CreateText(string text)
        {
            if (text == null)
            {
                throw new ArgumentNullException(nameof(text));
            }

            return new WhatsAppTemplateParameter
            {
                Type = "text",
                Text = text
            };
        }

        /// <summary>
        /// Creates a currency parameter.
        /// </summary>
        /// <param name="fallbackValue">Default localized string representation.</param>
        /// <param name="currencyCode">ISO 4217 currency code (e.g. "EGP", "USD").</param>
        /// <param name="amount1000">Amount multiplied by 1000.</param>
        /// <returns>A new <see cref="WhatsAppTemplateParameter"/>.</returns>
        public static WhatsAppTemplateParameter CreateCurrency(string fallbackValue, string currencyCode, long amount1000)
        {
            return new WhatsAppTemplateParameter
            {
                Type = "currency",
                Currency = new WhatsAppCurrencyParameter
                {
                    FallbackValue = fallbackValue,
                    Code = currencyCode,
                    Amount1000 = amount1000
                }
            };
        }

        /// <summary>
        /// Creates a date/time parameter.
        /// </summary>
        /// <param name="fallbackValue">Default localized string representation.</param>
        /// <returns>A new <see cref="WhatsAppTemplateParameter"/>.</returns>
        public static WhatsAppTemplateParameter CreateDateTime(string fallbackValue)
        {
            return new WhatsAppTemplateParameter
            {
                Type = "date_time",
                DateTime = new WhatsAppDateTimeParameter
                {
                    FallbackValue = fallbackValue
                }
            };
        }

        /// <summary>
        /// Creates a media parameter for a header component.
        /// </summary>
        /// <param name="mediaType">The media type.</param>
        /// <param name="mediaId">Optional uploaded media ID.</param>
        /// <param name="link">Optional publicly accessible HTTPS URL.</param>
        /// <returns>A new <see cref="WhatsAppTemplateParameter"/>.</returns>
        public static WhatsAppTemplateParameter CreateMedia(WhatsAppMediaType mediaType, string? mediaId = null, string? link = null)
        {
            string typeStr = mediaType.ToString().ToLowerInvariant();
            return new WhatsAppTemplateParameter
            {
                Type = typeStr,
                Media = new WhatsAppMediaReference
                {
                    Id = mediaId,
                    Link = link
                }
            };
        }
    }

    /// <summary>
    /// Currency parameter details for a template.
    /// </summary>
    public class WhatsAppCurrencyParameter
    {
        /// <summary>
        /// Gets or sets the fallback display string.
        /// </summary>
        public string FallbackValue { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the ISO 4217 currency code (e.g. "EGP", "USD").
        /// </summary>
        public string Code { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the amount multiplied by 1000 (e.g. 100.50 EGP is 100500).
        /// </summary>
        public long Amount1000 { get; set; }
    }

    /// <summary>
    /// Date and time parameter details for a template.
    /// </summary>
    public class WhatsAppDateTimeParameter
    {
        /// <summary>
        /// Gets or sets the fallback display string for the date/time.
        /// </summary>
        public string FallbackValue { get; set; } = string.Empty;
    }

    /// <summary>
    /// Media reference specifying either a Meta media ID or a direct URL link.
    /// </summary>
    public class WhatsAppMediaReference
    {
        /// <summary>
        /// Gets or sets the Meta Graph API media ID.
        /// </summary>
        public string? Id { get; set; }

        /// <summary>
        /// Gets or sets the publicly accessible HTTPS URL of the media file.
        /// </summary>
        public string? Link { get; set; }

        /// <summary>
        /// Gets or sets the optional caption for the media.
        /// </summary>
        public string? Caption { get; set; }

        /// <summary>
        /// Gets or sets the optional filename when sending a document.
        /// </summary>
        public string? Filename { get; set; }
    }
}
