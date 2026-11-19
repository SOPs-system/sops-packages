using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Sops.Drugs.Models;
using Sops.Drugs.Normalization;
using Sops.Drugs.Serialization;
using Sops.Drugs.SideName;

namespace Sops.Drugs.Runtime
{
    /// <summary>
    /// Fast, in-memory indexed drug catalog supporting instant bilingual search, barcode lookup,
    /// ingredient search, and consistency validation.
    /// </summary>
    public class DrugCatalogIndex
    {
        private readonly DrugCatalog _catalog;
        private readonly Dictionary<string, Drug> _byId = new Dictionary<string, Drug>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Drug> _bySku = new Dictionary<string, Drug>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Drug> _byBarcode = new Dictionary<string, Drug>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<Drug>> _byCategory = new Dictionary<string, List<Drug>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, List<Drug>> _byIngredient = new Dictionary<string, List<Drug>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, DrugCategory> _categoriesById = new Dictionary<string, DrugCategory>(StringComparer.OrdinalIgnoreCase);

        // Search token inverted index
        private readonly Dictionary<string, List<Drug>> _tokenIndex = new Dictionary<string, List<Drug>>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// Gets the underlying <see cref="DrugCatalog"/>.
        /// </summary>
        public DrugCatalog Catalog => _catalog;

        /// <summary>
        /// Gets all drugs in the catalog.
        /// </summary>
        public IReadOnlyList<Drug> Drugs => _catalog.Drugs;

        /// <summary>
        /// Gets all categories in the catalog.
        /// </summary>
        public IReadOnlyList<DrugCategory> Categories => _catalog.Categories;

        /// <summary>
        /// Gets the total number of drugs in the catalog.
        /// </summary>
        public int Count => _catalog.Drugs.Count;

        /// <summary>
        /// Initializes a new instance of the <see cref="DrugCatalogIndex"/> class from an existing catalog.
        /// </summary>
        /// <param name="catalog">The catalog instance.</param>
        public DrugCatalogIndex(DrugCatalog catalog)
        {
            _catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
            BuildIndexes();
        }

        /// <summary>
        /// Loads and indexes a catalog from a JSON file path.
        /// </summary>
        public static DrugCatalogIndex LoadFromFile(string filePath)
        {
            var catalog = DrugCatalogSerializer.DeserializeCatalogFromFile(filePath);
            return new DrugCatalogIndex(catalog);
        }

        /// <summary>
        /// Loads and indexes a catalog from a JSON stream.
        /// </summary>
        public static DrugCatalogIndex LoadFromStream(Stream stream)
        {
            var catalog = DrugCatalogSerializer.DeserializeCatalog(stream);
            return new DrugCatalogIndex(catalog);
        }

        private void BuildIndexes()
        {
            foreach (var cat in _catalog.Categories)
            {
                if (!string.IsNullOrWhiteSpace(cat.Id))
                {
                    _categoriesById[cat.Id] = cat;
                }
            }

            foreach (var drug in _catalog.Drugs)
            {
                if (!string.IsNullOrWhiteSpace(drug.Id))
                {
                    _byId[drug.Id] = drug;
                }

                if (!string.IsNullOrWhiteSpace(drug.Sku))
                {
                    _bySku[drug.Sku] = drug;
                }

                if (!string.IsNullOrWhiteSpace(drug.Barcode))
                {
                    _byBarcode[drug.Barcode!] = drug;
                }

                if (!string.IsNullOrWhiteSpace(drug.CategoryId))
                {
                    if (!_byCategory.TryGetValue(drug.CategoryId!, out var list))
                    {
                        list = new List<Drug>();
                        _byCategory[drug.CategoryId!] = list;
                    }
                    list.Add(drug);
                }

                foreach (var ing in drug.GenericNames)
                {
                    string normIng = ing.Trim().ToUpperInvariant();
                    if (!string.IsNullOrWhiteSpace(normIng))
                    {
                        if (!_byIngredient.TryGetValue(normIng, out var list))
                        {
                            list = new List<Drug>();
                            _byIngredient[normIng] = list;
                        }
                        list.Add(drug);
                    }
                }

                // Index search tokens for English name and Arabic name
                IndexTextTokens(drug.NameEn, drug);
                IndexTextTokens(drug.NameAr, drug);
                IndexTextTokens(drug.Company, drug);
            }
        }

        private void IndexTextTokens(string? text, Drug drug)
        {
            if (string.IsNullOrWhiteSpace(text)) return;

            string normalized = text!.Contains('\u0600') // Check if Arabic Unicode block
                ? DrugNameNormalizer.NormalizeArabic(text)
                : DrugNameNormalizer.NormalizeEnglish(text);

            string[] tokens = normalized.Split(new[] { ' ', '-', '/', '(', ')', '+', '.', ',' }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string token in tokens)
            {
                string cleanToken = token.Trim();
                if (cleanToken.Length < 2) continue;

                if (!_tokenIndex.TryGetValue(cleanToken, out var list))
                {
                    list = new List<Drug>();
                    _tokenIndex[cleanToken] = list;
                }

                if (!list.Contains(drug))
                {
                    list.Add(drug);
                }
            }
        }

        /// <summary>
        /// Retrieves a drug by its unique stable ID.
        /// </summary>
        public Drug? GetById(string id)
        {
            if (string.IsNullOrWhiteSpace(id)) return null;
            return _byId.TryGetValue(id.Trim(), out var drug) ? drug : null;
        }

        /// <summary>
        /// Retrieves a drug by its business SKU (e.g. "DRG-000001").
        /// </summary>
        public Drug? GetBySku(string sku)
        {
            if (string.IsNullOrWhiteSpace(sku)) return null;
            return _bySku.TryGetValue(sku.Trim(), out var drug) ? drug : null;
        }

        /// <summary>
        /// Retrieves a drug by its barcode.
        /// </summary>
        public Drug? FindByBarcode(string barcode)
        {
            if (string.IsNullOrWhiteSpace(barcode)) return null;
            string? normalized = BarcodeValidator.Normalize(barcode);
            if (normalized != null && _byBarcode.TryGetValue(normalized, out var drug))
            {
                return drug;
            }

            return null;
        }

        /// <summary>
        /// Finds all drugs belonging to a category ID.
        /// </summary>
        public IReadOnlyList<Drug> FindByCategory(string categoryId)
        {
            if (string.IsNullOrWhiteSpace(categoryId)) return Array.Empty<Drug>();
            return _byCategory.TryGetValue(categoryId.Trim(), out var list) ? list : Array.Empty<Drug>();
        }

        /// <summary>
        /// Finds all drugs containing a specific active ingredient.
        /// </summary>
        public IReadOnlyList<Drug> FindByIngredient(string ingredientName)
        {
            if (string.IsNullOrWhiteSpace(ingredientName)) return Array.Empty<Drug>();
            string norm = ingredientName.Trim().ToUpperInvariant();

            var results = new List<Drug>();
            foreach (var pair in _byIngredient)
            {
                if (pair.Key.IndexOf(norm, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    foreach (var drug in pair.Value)
                    {
                        if (!results.Contains(drug)) results.Add(drug);
                    }
                }
            }

            return results;
        }

        /// <summary>
        /// Searches drugs by English or Arabic name query with token matching.
        /// </summary>
        /// <param name="query">The search term.</param>
        /// <param name="limit">Maximum number of results to return (default 50).</param>
        /// <returns>A list of matching drugs.</returns>
        public IReadOnlyList<Drug> Search(string query, int limit = 50)
        {
            if (string.IsNullOrWhiteSpace(query))
                return Array.Empty<Drug>();

            string trimmedQuery = query.Trim();
            bool isArabic = false;
            foreach (char c in trimmedQuery)
            {
                if (c >= '\u0600' && c <= '\u06FF')
                {
                    isArabic = true;
                    break;
                }
            }

            string normalized = isArabic
                ? DrugNameNormalizer.NormalizeArabic(trimmedQuery)
                : DrugNameNormalizer.NormalizeEnglish(trimmedQuery);

            string[] tokens = normalized.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
            if (tokens.Length == 0) return Array.Empty<Drug>();

            // Find matching candidate sets
            List<Drug>? candidateMatches = null;

            foreach (string token in tokens)
            {
                var tokenHits = new HashSet<Drug>();

                foreach (var pair in _tokenIndex)
                {
                    if (pair.Key.StartsWith(token, StringComparison.OrdinalIgnoreCase))
                    {
                        foreach (var d in pair.Value)
                        {
                            tokenHits.Add(d);
                        }
                    }
                }

                if (candidateMatches == null)
                {
                    candidateMatches = tokenHits.ToList();
                }
                else
                {
                    candidateMatches = candidateMatches.Where(tokenHits.Contains).ToList();
                }

                if (candidateMatches.Count == 0) break;
            }

            return (candidateMatches ?? new List<Drug>()).Take(limit).ToList();
        }

        /// <summary>
        /// Validates the referential integrity and consistency of the indexed catalog.
        /// </summary>
        /// <returns>A list of validation issues found.</returns>
        public List<CatalogValidationIssue> ValidateCatalog()
        {
            var issues = new List<CatalogValidationIssue>();

            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenSkus = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var drug in _catalog.Drugs)
            {
                if (string.IsNullOrWhiteSpace(drug.Id))
                {
                    issues.Add(new CatalogValidationIssue("ERROR", "Drug missing ID", drug.NameEn));
                }
                else if (!seenIds.Add(drug.Id))
                {
                    issues.Add(new CatalogValidationIssue("ERROR", $"Duplicate drug ID '{drug.Id}'", drug.NameEn));
                }

                if (string.IsNullOrWhiteSpace(drug.Sku))
                {
                    issues.Add(new CatalogValidationIssue("ERROR", "Drug missing SKU", drug.NameEn));
                }
                else if (!seenSkus.Add(drug.Sku))
                {
                    issues.Add(new CatalogValidationIssue("ERROR", $"Duplicate drug SKU '{drug.Sku}'", drug.NameEn));
                }

                if (string.IsNullOrWhiteSpace(drug.NameEn))
                {
                    issues.Add(new CatalogValidationIssue("ERROR", "Drug missing English commercial name", drug.Id));
                }

                if (!string.IsNullOrWhiteSpace(drug.CategoryId) && !_categoriesById.ContainsKey(drug.CategoryId!))
                {
                    issues.Add(new CatalogValidationIssue("WARNING", $"Dangling CategoryId '{drug.CategoryId}'", drug.NameEn));
                }

                if (!string.IsNullOrWhiteSpace(drug.SideName) && !SideNameValidator.IsValid(drug.SideName))
                {
                    issues.Add(new CatalogValidationIssue("WARNING", $"Invalid SideName grammar: '{drug.SideName}'", drug.NameEn));
                }

                if (!string.IsNullOrWhiteSpace(drug.Barcode) && !BarcodeValidator.ValidateCheckDigit(drug.Barcode))
                {
                    issues.Add(new CatalogValidationIssue("INFO", $"Invalid barcode check digit: '{drug.Barcode}'", drug.NameEn));
                }
            }

            return issues;
        }
    }

    /// <summary>
    /// Represents a catalog validation error, warning, or informational notice.
    /// </summary>
    public class CatalogValidationIssue
    {
        /// <summary>Severity level ("ERROR", "WARNING", "INFO")</summary>
        public string Severity { get; set; } = "ERROR";
        /// <summary>Description of the issue</summary>
        public string Description { get; set; } = string.Empty;
        /// <summary>Context or affected entity</summary>
        public string Context { get; set; } = string.Empty;

        /// <summary>
        /// Initializes a new instance of the <see cref="CatalogValidationIssue"/> class.
        /// </summary>
        public CatalogValidationIssue()
        {
        }

        /// <summary>
        /// Initializes a new instance with the specified severity, description, and context.
        /// </summary>
        public CatalogValidationIssue(string severity, string description, string context)
        {
            Severity = severity;
            Description = description;
            Context = context;
        }

        /// <inheritdoc />
        public override string ToString() => $"[{Severity}] {Description} ({Context})";
    }
}
