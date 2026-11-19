namespace Sops.DataLayer.Models.Events
{
    /// <summary>
    /// Standard topics published over the local server live feed (/api/live).
    /// </summary>
    public static class LiveTopics
    {
        public const string Kpis = "kpis";
        public const string Notifications = "notifications";
        public const string Cart = "cart";
        public const string Inventory = "inventory";
        public const string Shift = "shift";
        public const string Sync = "sync";
    }
}
