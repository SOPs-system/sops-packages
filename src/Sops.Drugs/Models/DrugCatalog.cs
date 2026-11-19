using System;
using System.Collections.Generic;

namespace Sops.Drugs.Models
{
    /// <summary>
    /// Represents the complete in-memory catalog of pharmaceutical drugs and categories.
    /// </summary>
    public class DrugCatalog
    {
        /// <summary>
        /// Gets or sets the list of drugs in the catalog.
        /// </summary>
        public List<Drug> Drugs { get; set; } = new List<Drug>();

        /// <summary>
        /// Gets or sets the list of product categories in the catalog.
        /// </summary>
        public List<DrugCategory> Categories { get; set; } = new List<DrugCategory>();

        /// <summary>
        /// Gets or sets the provenance and build manifest metadata.
        /// </summary>
        public DrugCatalogManifest Manifest { get; set; } = new DrugCatalogManifest();
    }

    /// <summary>
    /// Metadata recording provenance, source commit hashes, timestamps, and build statistics.
    /// </summary>
    public class DrugCatalogManifest
    {
        /// <summary>
        /// Gets or sets the ISO-8601 UTC timestamp when the catalog was built.
        /// </summary>
        public DateTimeOffset BuildTimestampUtc { get; set; } = DateTimeOffset.UtcNow;

        /// <summary>
        /// Gets or sets the catalog version.
        /// </summary>
        public string Version { get; set; } = "1.0.0";

        /// <summary>
        /// Gets or sets the total number of drugs in the catalog.
        /// </summary>
        public int TotalDrugs { get; set; }

        /// <summary>
        /// Gets or sets the total number of categories in the catalog.
        /// </summary>
        public int TotalCategories { get; set; }

        /// <summary>
        /// Gets or sets the count of products matched and merged across multiple sources.
        /// </summary>
        public int MatchedCount { get; set; }

        /// <summary>
        /// Gets or sets the count of products present in only a single source.
        /// </summary>
        public int UnmatchedCount { get; set; }

        /// <summary>
        /// Gets or sets the count of barcode collision/conflict records detected.
        /// </summary>
        public int BarcodeConflictCount { get; set; }

        /// <summary>
        /// Gets or sets the count of price discrepancy records detected between sources.
        /// </summary>
        public int PriceDiscrepancyCount { get; set; }

        /// <summary>
        /// Gets or sets the count of cold-chain / refrigerated drugs in the catalog.
        /// </summary>
        public int RefrigeratedCount { get; set; }

        /// <summary>
        /// Gets or sets the list of source repository provenance entries.
        /// </summary>
        public List<SourceProvenance> Sources { get; set; } = new List<SourceProvenance>();
    }

    /// <summary>
    /// Information about an upstream data source repository.
    /// </summary>
    public class SourceProvenance
    {
        /// <summary>
        /// Gets or sets the source repository name or identifier.
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the repository URL.
        /// </summary>
        public string RepositoryUrl { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the pinned Git commit hash used for the build.
        /// </summary>
        public string CommitHash { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the relative file path within the repository.
        /// </summary>
        public string FilePath { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the SHA-256 cryptographic hash of the downloaded source file.
        /// </summary>
        public string Sha256Hash { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the file size in bytes.
        /// </summary>
        public long FileSizeBytes { get; set; }

        /// <summary>
        /// Gets or sets the license type governing the source data.
        /// </summary>
        public string License { get; set; } = string.Empty;

        /// <summary>
        /// Gets or sets the total number of records read from this source file.
        /// </summary>
        public int RecordCount { get; set; }
    }
}
