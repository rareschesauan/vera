using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Vera.Dependencies;
using Vera.Invoices;
using Vera.Models;
using Vera.Portugal.Invoices;

namespace Vera.Bootstrap
{
    public interface IAccountComponentFactoryCollection
    {
        /// <summary>
        /// Attempts to retrieve the <c>IComponentFactory</c> for the given account based on the active certification.
        /// This method will throw if no factories can be found.
        /// </summary>
        /// <param name="account">account to resolve the factory for</param>
        /// <returns>factory based on the given account</returns>
        IComponentFactory GetComponentFactory(Account account);

        /// <summary>
        /// Get the invoice handler factory specific to the account
        /// </summary>
        /// <param name="account">account to resolve the factory for</param>
        /// <returns>factory based on the given account</returns>
        IInvoiceHandlerFactory GetInvoiceHandlerFactory(Account account);

        /// <summary>
        /// Returns the names of the supported certifications that accounts can make use of.
        /// </summary>
        ICollection<string> Names { get; }
    }

    public sealed class AccountComponentFactoryCollection : IAccountComponentFactoryCollection
    {
        private readonly IDictionary<string, IAccountComponentFactory> _factories;
        private readonly IDictionary<string, IInvoiceHandlerFactory> _invoiceHandlerFactories;

        public AccountComponentFactoryCollection(
            IEnumerable<IAccountComponentFactory> factories, 
            IEnumerable<IInvoiceHandlerFactory> invoiceHandlerFactories)
        {
            _factories = factories.ToImmutableDictionary(x => x.Name);
            _invoiceHandlerFactories = invoiceHandlerFactories.ToImmutableDictionary(x => x.Name);
        }

        public IComponentFactory GetComponentFactory(Account account)
        {
            return GetOrThrow(account).CreateComponentFactory(account);
        }

        public IInvoiceHandlerFactory GetInvoiceHandlerFactory(Account account)
        {
            var exists = _invoiceHandlerFactories.TryGetValue(account.Certification, out var factory);

            return exists ? factory : _invoiceHandlerFactories["default"];
        }

        public ICollection<string> Names => _factories.Keys;

        private IAccountComponentFactory GetOrThrow(Account account)
        {
            if (_factories.TryGetValue(account.Certification, out var factory))
            {
                return factory;
            }

            throw new InvalidOperationException($"No component factory available for certification {account.Certification}");
        }
    }
}