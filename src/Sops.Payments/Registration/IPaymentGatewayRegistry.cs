using System;
using System.Collections.Generic;
using Sops.Payments.Abstractions;

namespace Sops.Payments.Registration
{
    /// <summary>
    /// Registry for registering and dynamically resolving payment gateway providers.
    /// </summary>
    public interface IPaymentGatewayRegistry
    {
        /// <summary>
        /// Gets the names of all registered payment gateway providers.
        /// </summary>
        IReadOnlyCollection<string> RegisteredProviders { get; }

        /// <summary>
        /// Registers a payment gateway factory function under the specified provider name.
        /// </summary>
        /// <param name="providerName">The provider name (e.g. "Paymob", "Stripe").</param>
        /// <param name="factory">The factory to construct the gateway.</param>
        void Register(string providerName, Func<IServiceProvider?, IPaymentGateway> factory);

        /// <summary>
        /// Determines whether a provider is registered with the given name.
        /// </summary>
        /// <param name="providerName">The provider name.</param>
        /// <returns>True if registered; otherwise, false.</returns>
        bool IsRegistered(string providerName);

        /// <summary>
        /// Resolves and instantiates the payment gateway associated with the specified provider name.
        /// </summary>
        /// <param name="providerName">The provider name.</param>
        /// <param name="serviceProvider">Optional service provider for resolving dependencies.</param>
        /// <returns>An <see cref="IPaymentGateway"/> instance.</returns>
        IPaymentGateway CreateGateway(string providerName, IServiceProvider? serviceProvider = null);
    }
}
