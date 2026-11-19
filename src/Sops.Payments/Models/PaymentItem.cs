namespace Sops.Payments.Models
{
    /// <summary>
    /// Represents an item or line item within a payment order.
    /// </summary>
    public sealed class PaymentItem
    {
        /// <summary>
        /// Gets or sets the name or title of the item.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the item unit amount.
        /// </summary>
        public Money Amount { get; set; }

        /// <summary>
        /// Gets or sets the quantity of this item. Defaults to 1.
        /// </summary>
        public int Quantity { get; set; } = 1;

        /// <summary>
        /// Gets or sets an optional description of the item.
        /// </summary>
        public string? Description { get; set; }

        /// <summary>
        /// Initializes a new empty instance of the <see cref="PaymentItem"/> class.
        /// </summary>
        public PaymentItem()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PaymentItem"/> class with basic details.
        /// </summary>
        /// <param name="name">The item name.</param>
        /// <param name="amount">The item price.</param>
        /// <param name="quantity">The item quantity.</param>
        /// <param name="description">Optional item description.</param>
        public PaymentItem(string name, Money amount, int quantity = 1, string? description = null)
        {
            Name = name;
            Amount = amount;
            Quantity = quantity;
            Description = description;
        }
    }
}
