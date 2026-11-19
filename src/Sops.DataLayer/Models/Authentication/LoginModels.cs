using System;
using System.Collections.Generic;

namespace Sops.DataLayer.Models.Authentication
{
    /// <summary>
    /// User credentials supplied during POS login.
    /// </summary>
    public sealed class LoginRequest
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string? TerminalId { get; set; }
        public long? ShiftFloatPiastres { get; set; }
    }

    /// <summary>
    /// Successful authentication response from the local pharmacy server.
    /// </summary>
    public sealed class LoginResponse
    {
        public string AccessToken { get; set; } = string.Empty;
        public string? RefreshToken { get; set; }
        public DateTimeOffset ExpiresAtUtc { get; set; }
        public SessionInfo Session { get; set; } = new SessionInfo();
        public IReadOnlyList<string> Permissions { get; set; } = Array.Empty<string>();
        public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    }

    /// <summary>
    /// Detailed session metadata identifying active staff, terminal, and cash shift.
    /// </summary>
    public sealed class SessionInfo
    {
        public string SessionId { get; set; } = string.Empty;
        public string StaffId { get; set; } = string.Empty;
        public string Username { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public string TerminalId { get; set; } = string.Empty;
        public string? ShiftId { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
        public DateTimeOffset ExpiresAtUtc { get; set; }
        public bool IsExpired => DateTimeOffset.UtcNow >= ExpiresAtUtc;
    }

    /// <summary>
    /// Multi-factor authentication challenge returned when secondary verification is required.
    /// </summary>
    public sealed class MfaChallenge
    {
        public string ChallengeId { get; set; } = string.Empty;
        public string Method { get; set; } = "otp";
        public string TargetHint { get; set; } = string.Empty;
        public DateTimeOffset ExpiresAtUtc { get; set; }
    }
}
