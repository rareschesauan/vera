using System.Linq;
using System.Threading.Tasks;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;
using Vera.Bootstrap;
using Vera.Grpc;
using Vera.Grpc.Shared;
using Vera.Host.Mapping;
using Vera.Host.Security;
using Vera.Stores;

namespace Vera.Host.Services
{
    [Authorize]
    public class InvoiceService : Grpc.InvoiceService.InvoiceServiceBase
    {
        private readonly IAccountStore _accountStore;
        private readonly IInvoiceStore _invoiceStore;
        private readonly IAccountComponentFactoryCollection _accountComponentFactoryCollection;

        public InvoiceService(
            IAccountStore accountStore,
            IInvoiceStore invoiceStore,
            IAccountComponentFactoryCollection accountComponentFactoryCollection)
        {
            _accountStore = accountStore;
            _invoiceStore = invoiceStore;
            _accountComponentFactoryCollection = accountComponentFactoryCollection;
        }

        public override async Task<CreateInvoiceReply> Create(CreateInvoiceRequest request, ServerCallContext context)
        {
            var invoice = request.Invoice.Unpack();
            var account = await context.ResolveAccount(_accountStore);

            // TODO: validate invoice, very, very, very strict
            // TODO(kevin): NF525 - requires signature of original invoice on the returned line
            
            var componentFactory = _accountComponentFactoryCollection.GetComponentFactory(account);
            var invoiceHandlerFactory = _accountComponentFactoryCollection.GetInvoiceHandlerFactory(account);

            var handler = invoiceHandlerFactory.Create(componentFactory);
            await handler.Handle(invoice);

            return new CreateInvoiceReply
            {
                Number = invoice.Number,
                Sequence = invoice.Sequence,
                Signature = new Signature
                {
                    Input = ByteString.CopyFromUtf8(invoice.Signature.Input),
                    Output = ByteString.CopyFrom(invoice.Signature.Output)
                }
            };
        }

        public override async Task<GetInvoiceReply> GetByNumber(GetInvoiceByNumberRequest request, ServerCallContext context)
        {
            var account = await context.ResolveAccount(_accountStore);
            var invoice = await _invoiceStore.GetByNumber(account.Id, request.Number);

            return new GetInvoiceReply
            {
                Number = invoice.Number,
                Supplier = invoice.Supplier.Pack(),
                PeriodId = invoice.PeriodId.ToString(),
                Remark = invoice.Remark
            };
        }

        public override async Task<ValidateInvoiceReply> Validate(ValidateInvoiceRequest request, ServerCallContext context)
        {
            var account = await context.ResolveAccount(_accountStore);
            var factory = _accountComponentFactoryCollection.GetComponentFactory(account);
            var validators = factory.CreateInvoiceValidators();
            var invoice = request.Invoice.Unpack();

            var reply = new ValidateInvoiceReply();
            foreach (var val in validators)
            {
                var results = val.Validate(invoice);
                foreach (var result in results)
                {
                    reply.Results[result.MemberNames.First()] = result.ErrorMessage;
                }
            }

            return reply;
        }
    }
}