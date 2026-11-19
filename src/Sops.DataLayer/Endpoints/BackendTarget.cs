namespace Sops.DataLayer.Endpoints
{
    /// <summary>
    /// Target backend destination for a logical operation.
    /// </summary>
    public enum BackendTarget
    {
        /// <summary>Local pharmacy server (authoritative for POS sales, inventory FEFO, shifts, cash drawer).</summary>
        LocalServer = 0,

        /// <summary>Global SOPs cloud backend (authoritative for licensing, global catalogs, cross-branch sync).</summary>
        GlobalApi = 1,

        /// <summary>Explicitly configured remote service.</summary>
        ExplicitService = 2
    }
}
