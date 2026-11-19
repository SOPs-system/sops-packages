namespace Sops.Drugs.Models
{
    /// <summary>
    /// Pharmaceutical drug dosage forms recognized in the SOPs catalog.
    /// </summary>
    public enum DrugDosageForm
    {
        /// <summary>Standard tablet</summary>
        TABLET = 0,
        /// <summary>Film-coated tablet</summary>
        TABLET_FC = 1,
        /// <summary>Enteric-coated tablet</summary>
        TABLET_EC = 2,
        /// <summary>Effervescent tablet</summary>
        TABLET_EFF = 3,
        /// <summary>Hard or soft gelatin capsule</summary>
        CAPSULE = 4,
        /// <summary>Oral liquid syrup</summary>
        SYRUP = 5,
        /// <summary>Oral or topical suspension</summary>
        SUSPENSION = 6,
        /// <summary>Liquid solution</summary>
        SOLUTION = 7,
        /// <summary>Topical ointment</summary>
        OINTMENT = 8,
        /// <summary>Topical cream</summary>
        CREAM = 9,
        /// <summary>Topical lotion</summary>
        LOTION = 10,
        /// <summary>Eye, ear, or nasal drops</summary>
        DROPS = 11,
        /// <summary>Medicinal powder</summary>
        POWDER = 12,
        /// <summary>Powder or granule sachet</summary>
        SACHET = 13,
        /// <summary>Oral granules</summary>
        GRANULES = 14,
        /// <summary>Rectal or vaginal suppository</summary>
        SUPPOSITORY = 15,
        /// <summary>Transdermal patch</summary>
        PATCH = 16,
        /// <summary>Respiratory inhaler</summary>
        INHALER = 17,
        /// <summary>Topical or nasal spray</summary>
        SPRAY = 18,
        /// <summary>Topical gel</summary>
        GEL = 19,
        /// <summary>Topical emulgel</summary>
        EMULGEL = 20,
        /// <summary>Injection vial</summary>
        VIAL = 21,
        /// <summary>Injection ampoule</summary>
        AMPOULE = 22,
        /// <summary>Other or unclassified dosage form</summary>
        OTHER = 99
    }
}
