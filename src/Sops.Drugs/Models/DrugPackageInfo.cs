namespace Sops.Drugs.Models
{
    /// <summary>
    /// Packaging specification for a pharmaceutical product.
    /// </summary>
    public class DrugPackageInfo
    {
        /// <summary>
        /// Gets or sets the primary package unit name (e.g. "Strip", "Box", "Bottle").
        /// </summary>
        public string Name { get; set; } = "Box";

        /// <summary>
        /// Gets or sets the package type indicator ("UNIT" or "SUBPACKAGE").
        /// </summary>
        public string Type { get; set; } = "UNIT";

        /// <summary>
        /// Gets or sets the count of pieces or units contained in this package tier.
        /// </summary>
        public int AmountInPack { get; set; } = 1;

        /// <summary>
        /// Initializes a new instance of the <see cref="DrugPackageInfo"/> class.
        /// </summary>
        public DrugPackageInfo()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DrugPackageInfo"/> class.
        /// </summary>
        public DrugPackageInfo(string name, string type, int amountInPack)
        {
            Name = name;
            Type = type;
            AmountInPack = amountInPack;
        }

        /// <inheritdoc />
        public override string ToString() => $"{Name} ({Type}, {AmountInPack})";
    }
}
