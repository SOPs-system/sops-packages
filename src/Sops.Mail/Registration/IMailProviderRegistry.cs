using System;
using System.Collections.Generic;
using Sops.Mail.Abstractions;

namespace Sops.Mail.Registration
{
    /// <summary>
    /// Registry for registering and creating email providers by name.
    /// </summary>
    public interface IMailProviderRegistry
    {
        /// <summary>
        /// Gets the names of all registered mail providers.
        /// </summary>
        IReadOnlyCollection<string> RegisteredProviders { get; }

        /// <summary>
        /// Registers a mail provider factory under the specified provider name.
        /// </summary>
        /// <param name="providerName">The provider name (e.g. "Smtp", "SendGrid").</param>
        /// <param name="factory">The factory function to create the provider instance.</param>
        void Register(string providerName, Func<IServiceProvider?, IMailSender> factory);

        /// <summary>
        /// Checks whether a provider is registered with the given name.
        /// </summary>
        /// <param name="providerName">The provider name.</param>
        /// <returns>True if registered; otherwise false.</returns>
        bool IsRegistered(string providerName);

        /// <summary>
        /// Creates an instance of the specified mail provider.
        /// </summary>
        /// <param name="providerName">The provider name.</param>
        /// <param name="serviceProvider">Optional service provider for dependency resolution.</param>
        /// <returns>An <see cref="IMailSender"/> instance.</returns>
        IMailSender CreateProvider(string providerName, IServiceProvider? serviceProvider = null);
    }
}
