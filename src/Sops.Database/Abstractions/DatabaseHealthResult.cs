using System;
using System.Collections.Generic;

namespace Sops.Database.Abstractions
{
    /// <summary>
    /// Represents the outcome of a database provider health check probe.
    /// </summary>
    public sealed class DatabaseHealthResult
    {
        /// <summary>
        /// Gets a value indicating whether the database provider is healthy and responsive.
        /// </summary>
        public bool IsHealthy { get; }

        /// <summary>
        /// Gets an optional descriptive message detailing the health status.
        /// </summary>
        public string? Description { get; }

        /// <summary>
        /// Gets the measured round-trip latency of the health probe.
        /// </summary>
        public TimeSpan Latency { get; }

        /// <summary>
        /// Gets the exception encountered during the health check, if any.
        /// </summary>
        public Exception? Exception { get; }

        /// <summary>
        /// Gets additional diagnostic key-value pairs associated with the check.
        /// </summary>
        public IReadOnlyDictionary<string, object> Data { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="DatabaseHealthResult"/> class.
        /// </summary>
        /// <param name="isHealthy">Whether the provider is healthy.</param>
        /// <param name="latency">Probe latency duration.</param>
        /// <param name="description">Optional description message.</param>
        /// <param name="exception">Optional exception encountered.</param>
        /// <param name="data">Optional diagnostic metadata.</param>
        public DatabaseHealthResult(
            bool isHealthy,
            TimeSpan latency,
            string? description = null,
            Exception? exception = null,
            IReadOnlyDictionary<string, object>? data = null)
        {
            IsHealthy = isHealthy;
            Latency = latency;
            Description = description;
            Exception = exception;
            Data = data ?? new Dictionary<string, object>();
        }

        /// <summary>
        /// Creates a successful, healthy status result.
        /// </summary>
        /// <param name="latency">Probe latency.</param>
        /// <param name="description">Optional success message.</param>
        /// <param name="data">Optional diagnostic metadata.</param>
        /// <returns>A healthy <see cref="DatabaseHealthResult"/>.</returns>
        public static DatabaseHealthResult Healthy(
            TimeSpan latency,
            string? description = "Database probe succeeded.",
            IReadOnlyDictionary<string, object>? data = null) =>
            new DatabaseHealthResult(true, latency, description, null, data);

        /// <summary>
        /// Creates a failed, unhealthy status result.
        /// </summary>
        /// <param name="latency">Probe latency before failure.</param>
        /// <param name="description">Error description.</param>
        /// <param name="exception">Exception that caused the failure.</param>
        /// <param name="data">Optional diagnostic metadata.</param>
        /// <returns>An unhealthy <see cref="DatabaseHealthResult"/>.</returns>
        public static DatabaseHealthResult Unhealthy(
            TimeSpan latency,
            string? description,
            Exception? exception = null,
            IReadOnlyDictionary<string, object>? data = null) =>
            new DatabaseHealthResult(false, latency, description, exception, data);
    }
}
