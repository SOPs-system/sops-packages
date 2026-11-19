namespace Sops.DataLayer.Models.Authentication
{
    /// <summary>
    /// Current client authentication and session state.
    /// </summary>
    public enum AuthenticationState
    {
        Unauthenticated = 0,
        Authenticating = 1,
        MfaRequired = 2,
        Authenticated = 3,
        Refreshing = 4,
        SessionExpired = 5,
        Revoked = 6,
        Locked = 7
    }
}
