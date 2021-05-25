﻿using System;
using System.Collections.Generic;
using Vera.Audits;
using Vera.Configuration;
using Vera.Dependencies;
using Vera.Invoices;
using Vera.Models;
using Vera.Registers;
using Vera.Reports;
using Vera.Signing;
using Vera.Thermal;

namespace Vera.Poland
{
    public sealed class ComponentFactory : IComponentFactory
    {
        public IConfigurationValidator CreateConfigurationValidator()
        {
            return new ConfigurationValidator();
        }

        public IEnumerable<IInvoiceValidator> CreateInvoiceValidators()
        {
            return new List<IInvoiceValidator> { new NullInvoiceValidator() };
        }

        public IBucketGenerator<Invoice> CreateInvoiceBucketGenerator()
        {
            throw new NotImplementedException();
        }

        public IInvoiceNumberGenerator CreateInvoiceNumberGenerator()
        {
            throw new NotImplementedException();
        }

        public IPackageSigner CreatePackageSigner()
        {
            throw new NotImplementedException();
        }

        public IThermalReceiptGenerator CreateThermalReceiptGenerator()
        {
            throw new NotImplementedException();
        }

        public IReportReceiptGenerator CreateThermalReportGenerator()
        {
            throw new NotImplementedException();
        }

        public IAuditWriter CreateAuditWriter()
        {
            throw new NotImplementedException();
        }

        public IRegisterInitializer CreateRegisterInitializer()
        {
            return new OpenRegisterInitializer();
        }
    }
}