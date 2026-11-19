namespace Sops.Drugs.Models
{
    /// <summary>
    /// Represents an active pharmaceutical ingredient with its concentration strength and unit.
    /// </summary>
    public class DrugIngredient
    {
        /// <summary>
        /// Gets or sets the scientific or chemical name of the active ingredient.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the numeric concentration or strength (e.g. 500, 10, 2.5).
        /// </summary>
        public double Concentration { get; set; }

        /// <summary>
        /// Gets or sets the measurement unit of the concentration.
        /// </summary>
        public ConcentrationUnit Unit { get; set; } = ConcentrationUnit.MG;

        /// <summary>
        /// Initializes a new instance of the <see cref="DrugIngredient"/> class.
        /// </summary>
        public DrugIngredient()
        {
        }

        /// <summary>
        /// Initializes a new instance with the specified name, concentration, and unit.
        /// </summary>
        public DrugIngredient(string name, double concentration, ConcentrationUnit unit = ConcentrationUnit.MG)
        {
            Name = name;
            Concentration = concentration;
            Unit = unit;
        }

        /// <inheritdoc />
        public override string ToString() => $"{Name} {Concentration} {Unit}".Trim();
    }
}
