﻿using Microsoft.Extensions.Logging;
using Vera.Concurrency;
using Vera.Dependencies;
using Vera.Dependencies.Handlers;
using Vera.Invoices;
using Vera.Models;
using Vera.Portugal.WorkingDocuments;
using Vera.Stores;

namespace Vera.Portugal.Invoices
{
    public sealed class PortugalInvoiceHandlerFactory : IInvoiceHandlerFactory
    {
        private readonly ILoggerFactory _loggerFactory;
        private readonly IInvoiceStore _invoiceStore;
        private readonly IChainStore _chainStore;
        private readonly ILocker _locker;
        private readonly ISupplierStore _supplierStore;
        private readonly IPeriodStore _periodStore;
        private readonly IWorkingDocumentStore _wdStore;

        public PortugalInvoiceHandlerFactory(
            ILoggerFactory loggerFactory,
            IInvoiceStore invoiceStore,
            IChainStore chainStore,
            ILocker locker,
            ISupplierStore supplierStore,
            IPeriodStore periodStore,
            IWorkingDocumentStore wdStore)
        {
            _loggerFactory = loggerFactory;
            _invoiceStore = invoiceStore;
            _chainStore = chainStore;
            _locker = locker;
            _supplierStore = supplierStore;
            _periodStore = periodStore;
            _wdStore = wdStore;
        }

        public IHandlerChain<Invoice> Create(IInvoiceComponentFactory factory)
        {
            var signer = factory.CreatePackageSigner();
            var bucketGenerator = factory.CreateInvoiceBucketGenerator();
            var head = new InvoiceSupplierHandler(_supplierStore);

            var wdHandler = new WorkingDocumentsHandler(_wdStore, _chainStore,
                    signer, _loggerFactory.CreateLogger<WorkingDocumentsHandler>());

            var persistenceHandler = new InvoicePersistenceHandler(
                _loggerFactory.CreateLogger<InvoicePersistenceHandler>(),
                _chainStore,
                _invoiceStore,
                signer,
                factory.CreateInvoiceNumberGenerator(),
                bucketGenerator
            );

            wdHandler.WithNext(persistenceHandler);

            head.WithNext(new InvoiceOpenPeriodHandler(_periodStore))
                .WithNext(new InvoiceTotalsHandler())
                .WithNext(new InvoiceValidationHandler(factory.CreateInvoiceValidator()))
                .WithNext(new LockingHandler<Invoice>(wdHandler, _locker, bucketGenerator));

            return head;
        }

        public string Name => "PT";
    }
}
