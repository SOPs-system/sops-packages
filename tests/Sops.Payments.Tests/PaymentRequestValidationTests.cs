using System;
using Sops.Payments.Models;
using Xunit;

namespace Sops.Payments.Tests
{
    public class PaymentRequestValidationTests
    {
        [Fact]
        public void ValidRequest_PassesValidation()
        {
            var customer = new PaymentCustomer("Ahmed", "Ali", "ahmed@example.com", "+201012345678");
            var request = new PaymentRequest(Money.FromDecimal(100.00m, "EGP"), customer, "ORD-123", "IDEMP-123");

            request.Validate();

            Assert.Equal(100.00m, request.Amount.Amount);
            Assert.Equal("ORD-123", request.OrderId);
            Assert.Equal("IDEMP-123", request.IdempotencyKey);
            Assert.Equal("ahmed@example.com", request.Customer.Email);
        }

        [Fact]
        public void ZeroOrNegativeAmount_ThrowsInvalidOperationException()
        {
            var customer = new PaymentCustomer("Ahmed", "Ali", "ahmed@example.com", "+201012345678");
            var request = new PaymentRequest(Money.FromDecimal(0.00m, "EGP"), customer);

            var ex = Assert.Throws<InvalidOperationException>(() => request.Validate());
            Assert.Contains("greater than zero", ex.Message);
        }

        [Fact]
        public void MissingCustomerEmail_ThrowsInvalidOperationException()
        {
            var customer = new PaymentCustomer("Ahmed", "Ali", "", "+201012345678");
            var request = new PaymentRequest(Money.FromDecimal(50.00m, "EGP"), customer);

            var ex = Assert.Throws<InvalidOperationException>(() => request.Validate());
            Assert.Contains("email address is required", ex.Message);
        }

        [Fact]
        public void MissingCustomerPhone_ThrowsInvalidOperationException()
        {
            var customer = new PaymentCustomer("Ahmed", "Ali", "ahmed@example.com", "");
            var request = new PaymentRequest(Money.FromDecimal(50.00m, "EGP"), customer);

            var ex = Assert.Throws<InvalidOperationException>(() => request.Validate());
            Assert.Contains("phone number is required", ex.Message);
        }

        [Fact]
        public void RefundRequest_Validation_ChecksTransactionIdAndAmount()
        {
            var validRefund = new RefundRequest("TX-12345", Money.FromDecimal(25.00m, "EGP"));
            validRefund.Validate();

            var emptyTxRefund = new RefundRequest("", Money.FromDecimal(25.00m, "EGP"));
            Assert.Throws<InvalidOperationException>(() => emptyTxRefund.Validate());

            var zeroAmountRefund = new RefundRequest("TX-12345", Money.FromDecimal(0.00m, "EGP"));
            Assert.Throws<InvalidOperationException>(() => zeroAmountRefund.Validate());
        }
    }
}
