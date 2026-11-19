using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Sops.Drugs.Models;

namespace Sops.Drugs.SideName
{
    /// <summary>
    /// Formats deterministic SideName strings from drug strengths, dosage forms, and package counts.
    /// Ensures compliance with SOPs server SideName grammar rules.
    /// </summary>
    public static class SideNameFormatter
    {
        /// <summary>
        /// Formats a SideName string from ingredient strengths, dosage form, and pack unit count.
        /// </summary>
        /// <param name="ingredients">The list of active ingredients.</param>
        /// <param name="form">The dosage form.</param>
        /// <param name="units">The package unit count (e.g. 20 tablets, 1 bottle).</param>
        /// <returns>A validated, normalized SideName string.</returns>
        public static string Format(
            IEnumerable<DrugIngredient>? ingredients,
            DrugDosageForm form,
            int units = 1)
        {
            var segments = new List<string>();

            if (ingredients != null)
            {
                foreach (var ing in ingredients)
                {
                    if (ing.Concentration > 0)
                    {
                        string unitStr = FormatConcentrationUnit(ing.Unit);
                        string concStr = ing.Concentration.ToString("0.##", CultureInfo.InvariantCulture);
                        segments.Add($"{concStr} {unitStr}".Trim());
                    }
                }
            }

            int validUnits = units > 0 ? units : 1;
            string formStr = FormatDosageFormToken(form, validUnits);
            segments.Add($"{validUnits} {formStr}".Trim());

            string candidate = string.Join(" / ", segments);
            string normalized = SideNameValidator.Normalize(candidate);

            if (SideNameValidator.IsValid(normalized))
            {
                return normalized;
            }

            // Fallback: guaranteed valid single segment with number and form
            string fallback = $"{validUnits} {formStr}".Trim();
            if (SideNameValidator.IsValid(fallback))
            {
                return fallback;
            }

            return $"{validUnits} piece";
        }

        private static string FormatConcentrationUnit(ConcentrationUnit unit)
        {
            switch (unit)
            {
                case ConcentrationUnit.MG: return "mg";
                case ConcentrationUnit.ML: return "ml";
                case ConcentrationUnit.IU: return "iu";
                case ConcentrationUnit.MCG: return "mcg";
                case ConcentrationUnit.G: return "g";
                case ConcentrationUnit.PERCENT: return "%";
                case ConcentrationUnit.L: return "l";
                default: return "mg";
            }
        }

        private static string FormatDosageFormToken(DrugDosageForm form, int units)
        {
            switch (form)
            {
                case DrugDosageForm.TABLET:
                case DrugDosageForm.TABLET_FC:
                case DrugDosageForm.TABLET_EC:
                case DrugDosageForm.TABLET_EFF:
                    return units == 1 ? "tablet" : "tablets";

                case DrugDosageForm.CAPSULE:
                    return units == 1 ? "capsule" : "capsules";

                case DrugDosageForm.SYRUP:
                case DrugDosageForm.SUSPENSION:
                case DrugDosageForm.SOLUTION:
                case DrugDosageForm.DROPS:
                case DrugDosageForm.LOTION:
                    return units == 1 ? "bottle" : "bottles";

                case DrugDosageForm.OINTMENT:
                case DrugDosageForm.CREAM:
                case DrugDosageForm.GEL:
                case DrugDosageForm.EMULGEL:
                    return units == 1 ? "tube" : "tubes";

                case DrugDosageForm.SACHET:
                case DrugDosageForm.POWDER:
                case DrugDosageForm.GRANULES:
                    return units == 1 ? "sachet" : "sachets";

                case DrugDosageForm.SUPPOSITORY:
                    return units == 1 ? "suppository" : "suppositories";

                case DrugDosageForm.AMPOULE:
                    return units == 1 ? "ampoule" : "ampoules";

                case DrugDosageForm.VIAL:
                    return units == 1 ? "vial" : "vials";

                case DrugDosageForm.INHALER:
                case DrugDosageForm.SPRAY:
                    return units == 1 ? "bottle" : "bottles";

                case DrugDosageForm.PATCH:
                    return units == 1 ? "patch" : "patches";

                default:
                    return units == 1 ? "piece" : "pieces";
            }
        }
    }
}
