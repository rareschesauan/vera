using System;
using System.Threading.Tasks;
using Vera.Grpc;
using Vera.Host.Mapping;
using Vera.Integration.Tests;
using Vera.Tests.Shared;
using Xunit;

namespace Vera.Portugal.Integration.Tests
{
    public class ReceiptServiceTests : IClassFixture<ApiWebApplicationFactory>
    {
        private readonly Setup _setup;

        public ReceiptServiceTests(ApiWebApplicationFactory fixture)
        {
            _setup = fixture.CreateSetup();
        }

        [Fact]
        public async Task Should_be_able_to_generate_a_receipt()
        {
            var client = await _setup.CreateClient(Constants.Account);

            var builder = new InvoiceBuilder();
            var director = new InvoiceDirector(builder, Guid.Parse(client.AccountId), client.SupplierSystemId);
            director.ConstructAnonymousWithSingleProductPaidWithCash();

            await client.OpenPeriod();

            var registerSystemId = await client.OpenRegister(100m);

            var invoice = builder.Result;
            invoice.RegisterSystemId = registerSystemId;
            var createInvoiceRequest = new CreateInvoiceRequest
            {
                Invoice = invoice.Pack()
            };

            var createInvoiceReply = await client.Invoice.CreateAsync(createInvoiceRequest, client.AuthorizedMetadata);

            var renderReceiptReply = await client.Receipt.RenderThermalAsync(new RenderThermalRequest
            {
                AccountId = client.AccountId,
                InvoiceNumber = createInvoiceReply.Number,
                Type = ReceiptOutputType.Json
            }, client.AuthorizedMetadata);

            Assert.Equal(ReceiptOutputType.Json, renderReceiptReply.Type);
            Assert.NotNull(renderReceiptReply.Content);

            // TODO(kevin): verify receipt content - important stuff that needs to be on there
            var receiptContent = renderReceiptReply.Content.ToStringUtf8();
        }

        [Fact]
        public async Task Should_be_able_to_mark_receipt_as_printed()
        {
            var client = await _setup.CreateClient(Constants.Account);

            var builder = new InvoiceBuilder();
            var director = new InvoiceDirector(builder, Guid.Parse(client.AccountId), client.SupplierSystemId);
            director.ConstructAnonymousWithSingleProductPaidWithCash();

            await client.OpenPeriod();
            var registerSystemId = await client.OpenRegister(100m);

            var invoice = builder.Result;
            invoice.RegisterSystemId = registerSystemId;

            var createInvoiceRequest = new CreateInvoiceRequest
            {
                Invoice = invoice.Pack()
            };

            var createInvoiceReply = await client.Invoice.CreateAsync(createInvoiceRequest, client.AuthorizedMetadata);

            var renderReceiptReply = await client.Receipt.RenderThermalAsync(new RenderThermalRequest
            {
                AccountId = client.AccountId,
                InvoiceNumber = createInvoiceReply.Number,
                Type = ReceiptOutputType.Json
            }, client.AuthorizedMetadata);

            await client.Receipt.UpdatePrintResultAsync(new UpdatePrintResultRequest
            {
                AccountId = client.AccountId,
                Token = renderReceiptReply.Token,
                Success = true,
            }, client.AuthorizedMetadata);
        }
    }
}
