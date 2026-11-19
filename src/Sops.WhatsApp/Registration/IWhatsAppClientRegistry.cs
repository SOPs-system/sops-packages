using System;
using System.Collections.Generic;
using Sops.WhatsApp.Abstractions;

namespace Sops.WhatsApp.Registration
{
    /// <summary>
    /// Registry of WhatsApp clients supporting custom client factories and multi-provider/multi-account extensibility.
    /// </summary>
    public interface IWhatsAppClientRegistry
    {
        /// <summary>
        /// Gets the list of registered provider names.
        /// </summary>
        IReadOnlyCollection<string> RegisteredProviders { get; }

        /// <summary>
        /// Registers a factory for a given provider name.
        /// </summary>
        /// <param name="providerName">The provider name (case-insensitive).</param>
        /// <param name="factory">The factory to create the client.</param>
        void Register(string providerName, Func<IServiceProvider?, IWhatsAppClient> factory);

        /// <summary>
        /// Checks whether a provider is registered.
        /// </summary>
        /// <param name="providerName">The provider name.</param>
        /// <returns><c>true</c> if registered; otherwise, <c>false</c>.</returns>
        bool IsRegistered(string providerName);

        /// <summary>
        /// Creates a client instance for the specified provider name.
        /// </summary>
        /// <param name="providerName">The provider name.</param>
        /// <param name="serviceProvider">Optional service provider for resolving dependencies.</param>
        /// <returns>A new <see cref="IWhatsAppClient"/> instance.</returns>
        IWhatsAppClient CreateClient(string providerName, IServiceProvider? serviceProvider = null);
    }
}
