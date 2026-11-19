using System.Text.Json;
using Sops.Payments.Providers.Paymob;
using Xunit;

namespace Sops.Payments.Tests
{
    public class PaymobHmacTests
    {
        [Fact]
        public void RequiredFieldsInOrder_MatchesPaymobSpecification()
        {
            Assert.Equal(20, PaymobHmacValidator.RequiredFieldsInOrder.Length);
            Assert.Equal("amount_cents", PaymobHmacValidator.RequiredFieldsInOrder[0]);
            Assert.Equal("created_at", PaymobHmacValidator.RequiredFieldsInOrder[1]);
            Assert.Equal("currency", PaymobHmacValidator.RequiredFieldsInOrder[2]);
            Assert.Equal("error_occured", PaymobHmacValidator.RequiredFieldsInOrder[3]);
            Assert.Equal("has_parent_transaction", PaymobHmacValidator.RequiredFieldsInOrder[4]);
            Assert.Equal("id", PaymobHmacValidator.RequiredFieldsInOrder[5]);
            Assert.Equal("integration_id", PaymobHmacValidator.RequiredFieldsInOrder[6]);
            Assert.Equal("is_3d_secure", PaymobHmacValidator.RequiredFieldsInOrder[7]);
            Assert.Equal("is_auth", PaymobHmacValidator.RequiredFieldsInOrder[8]);
            Assert.Equal("is_capture", PaymobHmacValidator.RequiredFieldsInOrder[9]);
            Assert.Equal("is_refunded", PaymobHmacValidator.RequiredFieldsInOrder[10]);
            Assert.Equal("is_standalone_payment", PaymobHmacValidator.RequiredFieldsInOrder[11]);
            Assert.Equal("is_voided", PaymobHmacValidator.RequiredFieldsInOrder[12]);
            Assert.Equal("order.id", PaymobHmacValidator.RequiredFieldsInOrder[13]);
            Assert.Equal("owner", PaymobHmacValidator.RequiredFieldsInOrder[14]);
            Assert.Equal("pending", PaymobHmacValidator.RequiredFieldsInOrder[15]);
            Assert.Equal("source_data.pan", PaymobHmacValidator.RequiredFieldsInOrder[16]);
            Assert.Equal("source_data.sub_type", PaymobHmacValidator.RequiredFieldsInOrder[17]);
            Assert.Equal("source_data.type", PaymobHmacValidator.RequiredFieldsInOrder[18]);
            Assert.Equal("success", PaymobHmacValidator.RequiredFieldsInOrder[19]);
        }

        [Fact]
        public void Validate_ValidPayloadAndSignature_ReturnsTrue()
        {
            string hmacSecret = "test_hmac_secret_key_12345";

            string json = @"{
                ""type"": ""TRANSACTION"",
                ""obj"": {
                    ""id"": 1234567,
                    ""pending"": false,
                    ""amount_cents"": 10000,
                    ""success"": true,
                    ""is_auth"": false,
                    ""is_capture"": false,
                    ""is_standalone_payment"": true,
                    ""is_voided"": false,
                    ""is_refunded"": false,
                    ""is_3d_secure"": true,
                    ""integration_id"": 9988,
                    ""order"": {
                        ""id"": 554433
                    },
                    ""created_at"": ""2026-11-17T12:00:00.000000"",
                    ""currency"": ""EGP"",
                    ""source_data"": {
                        ""pan"": ""2345"",
                        ""sub_type"": ""MasterCard"",
                        ""type"": ""card""
                    },
                    ""has_parent_transaction"": false,
                    ""owner"": ""1234"",
                    ""error_occured"": false
                }
            }";

            using var doc = JsonDocument.Parse(json);
            string concatenated = PaymobHmacValidator.BuildConcatenatedString(doc.RootElement.GetProperty("obj"));

            // Expected concatenated string:
            // amount_cents: 10000
            // created_at: 2026-11-17T12:00:00.000000
            // currency: EGP
            // error_occured: false
            // has_parent_transaction: false
            // id: 1234567
            // integration_id: 9988
            // is_3d_secure: true
            // is_auth: false
            // is_capture: false
            // is_refunded: false
            // is_standalone_payment: true
            // is_voided: false
            // order.id: 554433
            // owner: 1234
            // pending: false
            // source_data.pan: 2345
            // source_data.sub_type: MasterCard
            // source_data.type: card
            // success: true
            string expectedConcatenated = "100002026-11-17T12:00:00.000000EGPfalsefalse12345679988truefalsefalsefalsetruefalse5544331234false2345MasterCardcardtrue";
            Assert.Equal(expectedConcatenated, concatenated);

            string validSignature = PaymobHmacValidator.ComputeHmacSha512(expectedConcatenated, hmacSecret);

            bool isValid = PaymobHmacValidator.Validate(json, validSignature, hmacSecret);
            Assert.True(isValid);
        }

        [Fact]
        public void Validate_TamperedPayload_ReturnsFalse()
        {
            string hmacSecret = "test_hmac_secret_key_12345";
            string originalJson = @"{
                ""obj"": {
                    ""id"": 1234567,
                    ""pending"": false,
                    ""amount_cents"": 10000,
                    ""success"": true,
                    ""is_auth"": false,
                    ""is_capture"": false,
                    ""is_standalone_payment"": true,
                    ""is_voided"": false,
                    ""is_refunded"": false,
                    ""is_3d_secure"": true,
                    ""integration_id"": 9988,
                    ""order"": { ""id"": 554433 },
                    ""created_at"": ""2026-11-17T12:00:00.000000"",
                    ""currency"": ""EGP"",
                    ""source_data"": { ""pan"": ""2345"", ""sub_type"": ""MasterCard"", ""type"": ""card"" },
                    ""has_parent_transaction"": false,
                    ""owner"": ""1234"",
                    ""error_occured"": false
                }
            }";

            using var doc = JsonDocument.Parse(originalJson);
            string concatenated = PaymobHmacValidator.BuildConcatenatedString(doc.RootElement.GetProperty("obj"));
            string validSignature = PaymobHmacValidator.ComputeHmacSha512(concatenated, hmacSecret);

            // Tamper the payload: change amount from 10000 to 50000
            string tamperedJson = originalJson.Replace("10000", "50000");

            bool isValid = PaymobHmacValidator.Validate(tamperedJson, validSignature, hmacSecret);
            Assert.False(isValid);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        [InlineData(" ")]
        [InlineData("wrong_signature_hex")]
        public void Validate_InvalidSignature_ReturnsFalse(string? badSignature)
        {
            string hmacSecret = "secret";
            string json = @"{ ""obj"": { ""id"": 1 } }";

            bool isValid = PaymobHmacValidator.Validate(json, badSignature, hmacSecret);
            Assert.False(isValid);
        }

        [Fact]
        public void ConstantTimeEquals_ComparesCorrectlyAndCaseInsensitively()
        {
            string hash1 = "a1b2c3d4e5f6";
            string hash2 = "A1B2C3D4E5F6";
            string hash3 = "a1b2c3d4e5f7";

            Assert.True(PaymobHmacValidator.ConstantTimeEquals(hash1, hash2));
            Assert.False(PaymobHmacValidator.ConstantTimeEquals(hash1, hash3));
            Assert.False(PaymobHmacValidator.ConstantTimeEquals(hash1, "shorter"));
        }
    }
}
