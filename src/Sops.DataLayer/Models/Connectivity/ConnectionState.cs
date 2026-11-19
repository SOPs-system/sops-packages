namespace Sops.DataLayer.Models.Connectivity
{
    /// <summary>
    /// Independent connectivity state tracking local pharmacy server reachability and global cloud API reachability separately.
    /// </summary>
    public enum ConnectionState
    {
        Unknown = 0,
        LocalAndCloudAvailable = 1,
        LocalOnly = 2,
        CloudOnly = 3,
        FullyOffline = 4,
        LocalDegraded = 5,
        CloudDegraded = 6
    }
}
