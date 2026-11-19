using System;
using System.Text.RegularExpressions;
using Sops.Drugs.Models;

namespace Sops.Drugs.Normalization
{
    /// <summary>
    /// Parses pharmaceutical dosage forms and standardizes abbreviations into <see cref="DrugDosageForm"/>.
    /// </summary>
    public static class DosageFormParser
    {
        /// <summary>
        /// Parses a dosage form string or trade name text into a strongly typed <see cref="DrugDosageForm"/>.
        /// </summary>
        /// <param name="input">The raw text containing dosage form indicators.</param>
        /// <returns>The identified <see cref="DrugDosageForm"/>.</returns>
        public static DrugDosageForm Parse(string? input)
        {
            if (string.IsNullOrWhiteSpace(input))
                return DrugDosageForm.TABLET;

            string s = input!.Trim().ToUpperInvariant();

            // Tablet sub-types
            if (s.Contains("F.C. TAB") || s.Contains("F.C.TAB") || s.Contains("F.C.TABLET") ||
                s.Contains("FILM COATED") || s.Contains("FILM-COATED") || s.Contains("FCT") ||
                s.Contains("F.C.T") || s.Contains("COATED TAB"))
            {
                return DrugDosageForm.TABLET_FC;
            }

            if (s.Contains("E.C. TAB") || s.Contains("E.C.TAB") || s.Contains("ENTERIC COATED") ||
                s.Contains("ENTERIC-COATED") || s.Contains("E.C.T"))
            {
                return DrugDosageForm.TABLET_EC;
            }

            if (s.Contains("EFF. TAB") || s.Contains("EFF.TAB") || s.Contains("EFFERVESCENT") ||
                s.Contains("EFF.TABLET"))
            {
                return DrugDosageForm.TABLET_EFF;
            }

            // General tablet
            if (s.Contains("TABLET") || s.Contains("TABS") || s.Contains(" TAB ") || s.EndsWith(" TAB") ||
                s.Contains("TAB.") || s.Contains("LOZENGE") || s.Contains("CHEWABLE"))
            {
                return DrugDosageForm.TABLET;
            }

            // Capsule
            if (s.Contains("CAPSULE") || s.Contains("CAPS") || s.Contains(" CAP ") || s.EndsWith(" CAP") ||
                s.Contains("CAP.") || s.Contains("SOFTGEL") || s.Contains("SOFT GEL"))
            {
                return DrugDosageForm.CAPSULE;
            }

            // Liquids
            if (s.Contains("SUSPENSION") || s.Contains("SUSP") || s.Contains("SUSP."))
            {
                return DrugDosageForm.SUSPENSION;
            }

            if (s.Contains("SYRUP") || s.Contains("SYR") || s.Contains("SYR."))
            {
                return DrugDosageForm.SYRUP;
            }

            if (s.Contains("SOLUTION") || s.Contains(" SOL ") || s.EndsWith(" SOL") || s.Contains("SOL."))
            {
                return DrugDosageForm.SOLUTION;
            }

            // Topicals
            if (s.Contains("EMULGEL"))
            {
                return DrugDosageForm.EMULGEL;
            }

            if (s.Contains("GEL"))
            {
                return DrugDosageForm.GEL;
            }

            if (s.Contains("CREAM") || s.Contains("CRM") || s.Contains("CRM."))
            {
                return DrugDosageForm.CREAM;
            }

            if (s.Contains("OINTMENT") || s.Contains("OINT") || s.Contains("OINT."))
            {
                return DrugDosageForm.OINTMENT;
            }

            if (s.Contains("LOTION"))
            {
                return DrugDosageForm.LOTION;
            }

            // Drops
            if (s.Contains("DROP") || s.Contains("DROPS") || s.Contains("EYE DROP") || s.Contains("EAR DROP"))
            {
                return DrugDosageForm.DROPS;
            }

            // Sachets, powders, granules
            if (s.Contains("SACHET") || s.Contains("SACH") || s.Contains("SACH."))
            {
                return DrugDosageForm.SACHET;
            }

            if (s.Contains("GRANULE") || s.Contains("GRANULES"))
            {
                return DrugDosageForm.GRANULES;
            }

            if (s.Contains("POWDER") || s.Contains("PWD"))
            {
                return DrugDosageForm.POWDER;
            }

            // Suppository
            if (s.Contains("SUPPOSITORY") || s.Contains("SUPPOSITORIES") || s.Contains("SUPP") || s.Contains("SUPP.") ||
                s.Contains("PESSARY") || s.Contains("OVULE"))
            {
                return DrugDosageForm.SUPPOSITORY;
            }

            // Injectables
            if (s.Contains("VIAL") || s.Contains("VIALS"))
            {
                return DrugDosageForm.VIAL;
            }

            if (s.Contains("AMPOULE") || s.Contains("AMPOULES") || s.Contains("AMP") || s.Contains("AMP.") ||
                s.Contains("INJECTION") || s.Contains("INJ") || s.Contains("INJ."))
            {
                return DrugDosageForm.AMPOULE;
            }

            // Inhalers & Sprays
            if (s.Contains("SPRAY") || s.Contains("AEROSOL"))
            {
                return DrugDosageForm.SPRAY;
            }

            if (s.Contains("INHALER") || s.Contains("ROTACAP") || s.Contains("RESPICAP"))
            {
                return DrugDosageForm.INHALER;
            }

            if (s.Contains("PATCH"))
            {
                return DrugDosageForm.PATCH;
            }

            return DrugDosageForm.OTHER;
        }
    }
}
