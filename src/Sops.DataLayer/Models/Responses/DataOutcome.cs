namespace Sops.DataLayer.Models.Responses
{
    /// <summary>
    /// Categorized outcome of a DataLayer operation, separating business state from transport errors.
    /// </summary>
    public enum DataOutcome
    {
        Success = 0,
        Failure = 1,
        Cancelled = 2,
        Timeout = 3,
        Unauthorized = 4,
        Forbidden = 5,
        NotFound = 6,
        Conflict = 7,
        ValidationFailed = 8,
        RateLimited = 9,
        ServerUnavailable = 10,
        UnknownOutcome = 11,
        PendingSynchronization = 12,
        NetworkUnavailable = 13
    }
}
