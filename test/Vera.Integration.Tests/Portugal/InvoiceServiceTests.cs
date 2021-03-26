using System;
using System.Threading.Tasks;
using Bogus;
using Vera.Grpc;
using Vera.Grpc.Models;
using Vera.Models;
using Vera.Tests.Shared;
using Xunit;

namespace Vera.Integration.Tests.Portugal
{
    public class InvoiceServiceTests : IClassFixture<ApiWebApplicationFactory>
    {
        private readonly Setup _setup;

        public InvoiceServiceTests(ApiWebApplicationFactory fixture)
        {
            _setup = fixture.CreateSetup();
        }

        [Fact]
        public async Task Should_be_able_to_create_an_invoice()
        {
            var client = await _setup.CreateClient(Constants.Account);

            var builder = new InvoiceBuilder();
            var director = new InvoiceDirector(builder, Guid.Parse(client.AccountId), "1");
            director.ConstructAnonymousWithSingleProductPaidWithCash();

            var invoice = builder.Result;
            
            var createInvoiceRequest = new CreateInvoiceRequest
            {
                Invoice = invoice.Pack()
            };

            var createInvoiceReply = await client.Invoice.CreateAsync(createInvoiceRequest, client.AuthorizedMetadata);

            Assert.Equal($"itFR {invoice.Supplier.SystemId}/{createInvoiceReply.Sequence}", createInvoiceReply.Number);
            Assert.True(createInvoiceReply.Sequence > 0);
        }

        [Fact]
        public async Task Should_have_an_ascending_sequence()
        {
            var client = await _setup.CreateClient(Constants.Account);
            
            var builder = new InvoiceBuilder();
            var director = new InvoiceDirector(builder, Guid.Parse(client.AccountId), "1");
            director.ConstructAnonymousWithSingleProductPaidWithCash();

            var invoice = builder.Result;

            // Create same transaction twice to verify sequence is incremented
            var first = await client.Invoice.CreateAsync(new CreateInvoiceRequest
            {
                Invoice = invoice.Pack()
            }, client.AuthorizedMetadata);

            var next = await client.Invoice.CreateAsync(new CreateInvoiceRequest
            {
                Invoice = invoice.Pack()
            }, client.AuthorizedMetadata);

            Assert.True(first.Sequence < next.Sequence, $"{first.Sequence} < {next.Sequence}");
        }

        [Fact]
        public async Task Should_be_able_to_run_validation()
        {
            var client = await _setup.CreateClient(Constants.Account);

            var builder = new InvoiceBuilder();
            var director = new InvoiceDirector(builder, Guid.Parse(client.AccountId), "1");
            director.ConstructAnonymousWithSingleProductPaidWithCash();
            
            var validationReply = await client.Invoice.ValidateAsync(new ValidateInvoiceRequest
            {
                AccountId = client.AccountId,
                Invoice = builder.Result.Pack()
            }, client.AuthorizedMetadata);
            
            Assert.Empty(validationReply.Results);
        }

        [Fact]
        public async Task Should_be_able_to_get_validation_results()
        {
            var client = await _setup.CreateClient(Constants.Account);
            
            var builder = new InvoiceBuilder();
            var director = new InvoiceDirector(builder, Guid.Parse(client.AccountId), "1");
            director.ConstructAnonymousWithSingleProductPaidWithCash();

            var invoice = builder.Result;
            
            invoice.Lines.Add(new Models.InvoiceLine
            {
                Description = "trigger mixed quantities",
                Product = new()
                {
                    Code = "1234",
                    Description = "trigger mixed quantities",
                    Type = ProductType.Goods
                },
                Quantity = -1,
                Taxes = new Taxes
                {
                    Category = TaxesCategory.High,
                    Rate = 1.23m,
                    Code = "IVA"
                },
            });

            var validationReply = await client.Invoice.ValidateAsync(new ValidateInvoiceRequest
            {
                AccountId = client.AccountId,
                Invoice = invoice.Pack()
            }, client.AuthorizedMetadata);

            Assert.Contains(validationReply.Results, x => x.Key == "Lines");
        }

        [Fact]
        public async Task Should_create_invoice_with_supplier()
        {
            var _faker = new Faker();
            var client = await _setup.CreateClient(Constants.Account);

            var supplier = new Grpc.Shared.Supplier
            {
                Name = _faker.Name.FullName(),
                RegistrationNumber = _faker.Random.AlphaNumeric(10),
                TaxRegistrationNumber = _faker.Random.AlphaNumeric(10),
                SystemId = _faker.Random.AlphaNumeric(10),
                Address = new Grpc.Shared.Address
                {
                    City = _faker.Address.City(),
                    Country = _faker.Address.Country(),
                    Number = _faker.Address.BuildingNumber(),
                    PostalCode = _faker.Address.ZipCode(),
                    Region = _faker.Address.County(),
                    Street = _faker.Address.StreetAddress()
                }
            };

            var reply = await client.Supplier.CreateAsync(new CreateSupplierRequest { Supplier = supplier });

            Assert.NotNull(reply);

            var getSupplierReply = await client.Supplier.GetAsync(new GetSupplierRequest { SystemId = supplier.SystemId });


            var builder = new InvoiceBuilder();
            var director = new InvoiceDirector(builder, Guid.Parse(client.AccountId), getSupplierReply.SystemId);
            director.ConstructAnonymousWithSingleProductPaidWithCash();
            var invoice = builder.Result;

            var createInvoiceRequest = new CreateInvoiceRequest
            {
                Invoice = invoice.Pack()
            };
            var createInvoiceReply = await client.Invoice.CreateAsync(createInvoiceRequest, client.AuthorizedMetadata);

            var getByNumberRequest = new GetInvoiceByNumberRequest 
            { 
                AccountId = client.AccountId, 
                Number = createInvoiceReply.Number 
            };

            var getInvoiceReply = client.Invoice.GetByNumber(getByNumberRequest, client.AuthorizedMetadata);

            Assert.Equal(getInvoiceReply.Supplier.Name, getSupplierReply.Name);
        }
    }
}