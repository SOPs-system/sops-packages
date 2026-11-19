using System.Collections.Generic;
using Sops.Mail.Models;
using Sops.Mail.Templates;
using Xunit;

namespace Sops.Mail.Tests
{
    public class TemplateTests
    {
        [Fact]
        public void Render_BasicSubstitution_Works()
        {
            string template = "Hello {{Name}}, welcome to {{Company}}!";
            var vars = new Dictionary<string, object?>
            {
                ["Name"] = "Alice",
                ["Company"] = "SOPs"
            };

            string result = MailTemplateEngine.Render(template, vars);

            Assert.Equal("Hello Alice, welcome to SOPs!", result);
        }

        [Fact]
        public void Render_IsCaseInsensitive_AndTrimsWhitespace()
        {
            string template = "Order: {{ OrderId }}, Total: {{ total }}";
            var vars = new Dictionary<string, object?>
            {
                ["ORDERID"] = "ORD-998",
                ["Total"] = "$100"
            };

            string result = MailTemplateEngine.Render(template, vars);

            Assert.Equal("Order: ORD-998, Total: $100", result);
        }

        [Fact]
        public void Render_HtmlEncode_EncodesDangerousHtmlEntities()
        {
            string template = "<p>User: {{User}}</p>";
            var vars = new Dictionary<string, object?>
            {
                ["User"] = "<script>alert('xss')</script> & \"quotes\""
            };

            string result = MailTemplateEngine.Render(template, vars, htmlEncode: true);

            Assert.Equal("<p>User: &lt;script&gt;alert(&#39;xss&#39;)&lt;/script&gt; &amp; &quot;quotes&quot;</p>", result);
        }

        [Fact]
        public void Render_TripleBraces_AreNeverEncoded_EvenWhenHtmlEncodeIsTrue()
        {
            string template = "<div>Safe: {{User}}, Raw: {{{RawHtml}}}</div>";
            var vars = new Dictionary<string, object?>
            {
                ["User"] = "<b>Bold</b>",
                ["RawHtml"] = "<span class=\"badge\">Active</span>"
            };

            string result = MailTemplateEngine.Render(template, vars, htmlEncode: true);

            Assert.Equal("<div>Safe: &lt;b&gt;Bold&lt;/b&gt;, Raw: <span class=\"badge\">Active</span></div>", result);
        }

        [Fact]
        public void Render_StrictMode_ThrowsOnMissingVariable()
        {
            string template = "Hello {{Name}}, your code is {{Code}}";
            var vars = new Dictionary<string, object?>
            {
                ["Name"] = "Bob"
            };

            var ex = Assert.Throws<KeyNotFoundException>(() =>
                MailTemplateEngine.Render(template, vars, strict: true));

            Assert.Contains("Code", ex.Message);
        }

        [Fact]
        public void Render_LenientMode_ReplacesMissingVariableWithEmptyString()
        {
            string template = "Hello {{Name}}, optional: '{{Extra}}'!";
            var vars = new Dictionary<string, object?>
            {
                ["Name"] = "Bob"
            };

            string result = MailTemplateEngine.Render(template, vars, strict: false);

            Assert.Equal("Hello Bob, optional: ''!", result);
        }

        [Fact]
        public void Render_WithAnonymousObject_ExtractsProperties()
        {
            string template = "Hello {{FirstName}} {{LastName}}, status: {{Status}}";
            var model = new
            {
                FirstName = "John",
                LastName = "Doe",
                Status = "Approved"
            };

            string result = MailTemplateEngine.Render(template, model);

            Assert.Equal("Hello John Doe, status: Approved", result);
        }

        [Fact]
        public void RenderMessage_RendersSubjectTextAndHtmlCorrectly()
        {
            var template = new MailMessage
            {
                Subject = "Notice for {{User}}",
                TextBody = "Plain: {{User}} - <tag>",
                HtmlBody = "<p>Html: {{User}}</p>"
            };
            template.AddTo("user@example.com");

            var vars = new Dictionary<string, object?>
            {
                ["User"] = "Jane & Co."
            };

            var rendered = MailTemplateEngine.RenderMessage(template, vars);

            Assert.Equal("Notice for Jane & Co.", rendered.Subject);
            Assert.Equal("Plain: Jane & Co. - <tag>", rendered.TextBody);
            Assert.Equal("<p>Html: Jane &amp; Co.</p>", rendered.HtmlBody);
            Assert.Single(rendered.To);
        }
    }
}
