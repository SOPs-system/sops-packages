namespace Sops.Drugs.Models
{
    /// <summary>
    /// Represents a pharmaceutical or therapeutic category node in the catalog.
    /// </summary>
    public class DrugCategory
    {
        /// <summary>
        /// Gets or sets the unique category identifier (e.g. "CAT-0001").
        /// </summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the English category name.
        /// </summary>
        public string NameEn { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the Arabic category name, if available.
        /// </summary>
        public string? NameAr { get; set; }

        /// <summary>
        /// Gets or sets the optional parent category identifier for hierarchical categorization.
        /// </summary>
        public string? ParentId { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DrugCategory"/> class.
        /// </summary>
        public DrugCategory()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="DrugCategory"/> class.
        /// </summary>
        public DrugCategory(string id, string nameEn, string? nameAr = null, string? parentId = null)
        {
            Id = id;
            NameEn = nameEn;
            NameAr = nameAr;
            ParentId = parentId;
        }

        /// <inheritdoc />
        public override string ToString() => $"{NameEn} ({Id})";
    }
}
