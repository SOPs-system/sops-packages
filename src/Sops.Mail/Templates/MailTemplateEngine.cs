using System;
using System.Collections.Generic;
using System.Net;
using System.Reflection;
using System.Text.RegularExpressions;
using Sops.Mail.Models;

namespace Sops.Mail.Templates
{
    /// <summary>
    /// Provides lightweight, secure templating and placeholder replacement for email content.
    /// </summary>
    public static class MailTemplateEngine
    {
        private static readonly Regex PlaceholderRegex = new Regex(
            @"\{\{\{([^{}]+)\}\}\}|\{\{([^{}]+)\}\}",
            RegexOptions.Compiled);

        /// <summary>
        /// Renders a template string by replacing placeholders with dictionary values.
        /// </summary>
        /// <param name="template">The template text containing placeholders like {{VariableName}} or {{{RawHtml}}}.</param>
        /// <param name="variables">A dictionary of variable names and their values.</param>
        /// <param name="htmlEncode">Whether to HTML-encode values for standard {{var}} placeholders. Triple braces {{{var}}} are always unencoded.</param>
        /// <param name="strict">Whether to throw an exception if a referenced variable is missing from the dictionary.</param>
        /// <returns>The rendered string.</returns>
        public static string Render(
            string template,
            IDictionary<string, object?> variables,
            bool htmlEncode = false,
            bool strict = false)
        {
            if (string.IsNullOrEmpty(template)) return template ?? string.Empty;
            if (variables == null) throw new ArgumentNullException(nameof(variables));

            // Ensure case-insensitive key lookup
            var lookup = variables as Dictionary<string, object?>;
            if (lookup == null || lookup.Comparer != StringComparer.OrdinalIgnoreCase)
            {
                lookup = new Dictionary<string, object?>(variables, StringComparer.OrdinalIgnoreCase);
            }

            return PlaceholderRegex.Replace(template, match =>
            {
                bool isRaw = match.Groups[1].Success;
                string key = (isRaw ? match.Groups[1].Value : match.Groups[2].Value).Trim();

                if (!lookup.TryGetValue(key, out var val))
                {
                    if (strict)
                    {
                        throw new KeyNotFoundException($"The required template variable '{key}' was not found in the supplied dictionary.");
                    }
                    return string.Empty;
                }

                string stringVal = val?.ToString() ?? string.Empty;

                if (isRaw || !htmlEncode)
                {
                    return stringVal;
                }

                return WebUtility.HtmlEncode(stringVal);
            });
        }

        /// <summary>
        /// Renders a template string by replacing placeholders with properties of an object or anonymous type.
        /// </summary>
        /// <param name="template">The template text.</param>
        /// <param name="model">The object whose public properties provide variable values.</param>
        /// <param name="htmlEncode">Whether to HTML-encode standard placeholder values.</param>
        /// <param name="strict">Whether to throw if a placeholder has no matching property.</param>
        /// <returns>The rendered string.</returns>
        public static string Render(
            string template,
            object model,
            bool htmlEncode = false,
            bool strict = false)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            IDictionary<string, object?> dict;
            if (model is IDictionary<string, object?> existingDict)
            {
                dict = existingDict;
            }
            else
            {
                dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                var properties = model.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance);
                foreach (var prop in properties)
                {
                    if (prop.CanRead)
                    {
                        dict[prop.Name] = prop.GetValue(model);
                    }
                }
            }

            return Render(template, dict, htmlEncode, strict);
        }

        /// <summary>
        /// Renders the subject, text body, and HTML body of a message template using the supplied variables.
        /// </summary>
        /// <param name="templateMessage">The message template.</param>
        /// <param name="variables">The variable values.</param>
        /// <param name="strict">Whether missing variables should throw.</param>
        /// <returns>A new <see cref="MailMessage"/> instance with rendered content.</returns>
        public static MailMessage RenderMessage(
            MailMessage templateMessage,
            IDictionary<string, object?> variables,
            bool strict = false)
        {
            if (templateMessage == null) throw new ArgumentNullException(nameof(templateMessage));
            if (variables == null) throw new ArgumentNullException(nameof(variables));

            var rendered = new MailMessage
            {
                From = templateMessage.From,
                Subject = Render(templateMessage.Subject, variables, htmlEncode: false, strict: strict),
                TextBody = templateMessage.TextBody != null ? Render(templateMessage.TextBody, variables, htmlEncode: false, strict: strict) : null,
                HtmlBody = templateMessage.HtmlBody != null ? Render(templateMessage.HtmlBody, variables, htmlEncode: true, strict: strict) : null
            };

            foreach (var to in templateMessage.To) rendered.To.Add(to);
            foreach (var cc in templateMessage.Cc) rendered.Cc.Add(cc);
            foreach (var bcc in templateMessage.Bcc) rendered.Bcc.Add(bcc);
            foreach (var replyTo in templateMessage.ReplyTo) rendered.ReplyTo.Add(replyTo);
            foreach (var attachment in templateMessage.Attachments) rendered.Attachments.Add(attachment);
            foreach (var header in templateMessage.Headers) rendered.Headers.Add(header.Key, header.Value);

            return rendered;
        }
    }
}
