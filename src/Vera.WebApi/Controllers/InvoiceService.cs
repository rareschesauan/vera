using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Vera.Bootstrap;
using Vera.Grpc;
using Vera.Invoices;
using Vera.Models;
using Vera.Stores;
using Address = Vera.Models.Address;
using Invoice = Vera.Grpc.Invoice;
using InvoiceLine = Vera.Models.InvoiceLine;
using Payment = Vera.Models.Payment;
using Product = Vera.Grpc.Product;
using Settlement = Vera.Models.Settlement;

namespace Vera.WebApi.Controllers
{
    public class InvoiceService : Grpc.InvoiceService.InvoiceServiceBase
    {
        private readonly ICompanyStore _companyStore;
        private readonly IInvoiceStore _invoiceStore;
        private readonly IComponentFactoryCollection _componentFactoryCollection;

        public InvoiceService(
            ICompanyStore companyStore,
            IInvoiceStore invoiceStore,
            IComponentFactoryCollection componentFactoryCollection
        )
        {
            _companyStore = companyStore;
            _invoiceStore = invoiceStore;
            _componentFactoryCollection = componentFactoryCollection;
        }

        public override async Task<CreateInvoiceReply> Create(CreateInvoiceRequest request, ServerCallContext context)
        {
            var accountId = Guid.Parse(request.Invoice.Account);

            var principal = context.GetHttpContext().User;
            var company = await _companyStore.GetByName(principal.FindFirstValue(Security.ClaimTypes.CompanyName));
            var account = company.Accounts.FirstOrDefault(a => a.Id == accountId);

            if (account == null)
            {
                // Not allowed to create an invoice for this account because it does not belong to the company
                // to which the user has rights
                throw new RpcException(new Status(StatusCode.Unauthenticated, "Unauthorized"));
            }

            // TODO: validate invoice, very, very, very strict

            var factory = _componentFactoryCollection.Get(account);

            var facade = new InvoiceFacade(_invoiceStore, factory);

            var result = await facade.Process(Map(request.Invoice));

            return new CreateInvoiceReply
            {
                Number = result.Number,
                Sequence = result.Sequence,
                Signature = new Signature
                {
                    Input = ByteString.CopyFromUtf8(result.RawSignature),
                    Output = ByteString.CopyFrom(result.Signature)
                }
            };
        }

        private static Vera.Models.Invoice Map(Invoice invoice)
        {
            var result = new Vera.Models.Invoice
            {
                SystemId = invoice.SystemId,
                Date = invoice.Timestamp.ToDateTime(),
                Manual = invoice.Manual,
                Remark = invoice.Remark,
                AccountId = Guid.Parse(invoice.Account),
                TerminalId = invoice.TerminalId,
            };

            result.Supplier = new Billable
            {
                SystemId = invoice.Supplier.SystemId,
                Name = invoice.Supplier.Name,
                RegistrationNumber = invoice.Supplier.RegistrationNumber,
                TaxRegistrationNumber = invoice.Supplier.TaxRegistrationNumber
            };

            if (invoice.Customer != null)
            {
                result.Customer = new Vera.Models.Customer
                {
                    SystemID = invoice.Customer.SystemId,
                    Email = invoice.Customer.Email,
                    FirstName = invoice.Customer.FirstName,
                    LastName = invoice.Customer.LastName,
                    CompanyName = invoice.Customer.CompanyName,
                    RegistrationNumber = invoice.Customer.RegistrationNumber,
                    TaxRegistrationNumber = invoice.Customer.TaxRegistrationNumber,
                    ShippingAddress = Map(invoice.Customer.ShippingAddress),
                    BillingAddress = Map(invoice.Customer.BillingAddress)
                };
            }

            if (invoice.Employee != null)
            {
                // TODO: map some more fields and billable may not be the optimal type here
                result.Employee = new Billable
                {
                    SystemId = invoice.Employee.SystemId,
                };
            }

            result.Payments = invoice.Payments.Select(Map).ToList();

            result.Lines = invoice.Lines.Select(Map).ToList();

            return result;
        }

        private static Address Map(Vera.Grpc.Address a)
        {
            if (a == null) return null;

            return new()
            {
                City = a.City,
                Country = a.Country,
                Number = a.Number,
                Region = a.Region,
                Street = a.Street,
                PostalCode = a.PostalCode
            };
        }

        private static Payment Map(Vera.Grpc.Payment p)
        {
            var category = p.Category switch
            {
                Vera.Grpc.Payment.Types.Category.Other => PaymentCategory.Other,
                Vera.Grpc.Payment.Types.Category.Debit => PaymentCategory.Debit,
                Vera.Grpc.Payment.Types.Category.Credit => PaymentCategory.Credit,
                Vera.Grpc.Payment.Types.Category.Cash => PaymentCategory.Cash,
                Vera.Grpc.Payment.Types.Category.Voucher => PaymentCategory.Voucher,
                Vera.Grpc.Payment.Types.Category.Online => PaymentCategory.Online,
                _ => throw new ArgumentOutOfRangeException(nameof(p.Category), p.Category, null)
            };

            return new()
            {
                Amount = p.Amount,
                Category =  category,
                Description = p.Description
            };
        }

        private static InvoiceLine Map(Vera.Grpc.InvoiceLine line)
        {
            var result = new InvoiceLine
            {
                Description = line.Description,
                Gross = line.Gross,
                Net = line.Net,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice,
                UnitOfMeasure = line.Unit,
                Taxes = new Taxes
                {
                    Code = line.Tax.Code,
                    Rate = line.Tax.Rate
                }
            };

            if (line.Product != null)
            {
                var productType = line.Product.Group switch
                {
                    Product.Types.Group.Other => ProductTypes.Goods,
                    _ => throw new ArgumentOutOfRangeException(nameof(line.Product.Group))
                };

                result.Product = new Vera.Models.Product
                {
                    Code = line.Product.Code,
                    Description = line.Product.Description,
                    Type = productType,
                };
            }

            if (line.Settlements != null)
            {
                result.Settlements = line.Settlements.Select(s => new Settlement
                {
                    Amount = s.Amount,
                    Description = s.Description,
                    SystemId = s.SystemId
                }).ToList();
            }

            return result;
        }
    }
}