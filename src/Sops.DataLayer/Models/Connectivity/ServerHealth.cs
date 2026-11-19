using System;
using Sops.DataLayer.Endpoints;

namespace Sops.DataLayer.Models.Connectivity
{
    /// <summary>
    /// Health check outcome and latency measurement for a specific target server.
    /// </summary>
    public sealed class ServerHealth
    {
        public BackendTarget Target { get; }
        public bool IsReachable { get; }
        public TimeSpan Latency { get; }
        public string? StatusText { get; }
        public DateTimeOffset CheckedAtUtc { get; }
        public string? ErrorMessage { get; }

        public ServerHealth(
            BackendTarget target,
            bool isReachable,
            TimeSpan latency,
            string? statusText = null,
            string? errorMessage = null)
        {
            Target = target;
            IsReachable = isReachable;
            Latency = latency;
            StatusText = statusText ?? (isReachable ? "Healthy" : "Unreachable");
            CheckedAtUtc = DateTimeOffset.UtcNow;
            ErrorMessage = errorMessage;
        }

        public static ServerHealth Healthy(BackendTarget target, TimeSpan latency, string? status = null) =>
            new ServerHealth(target, true, latency, status);

        public static ServerHealth Unreachable(BackendTarget target, string errorMessage, TimeSpan latency = default) =>
            new ServerHealth(target, false, latency, "Unreachable", errorMessage);
    }
}
