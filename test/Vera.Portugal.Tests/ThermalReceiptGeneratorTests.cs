using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Bogus;
using Vera.Documents.Visitors;
using Vera.Invoices;
using Vera.Models;
using Vera.Tests.Shared;
using Vera.Thermal;
using Xunit;
using Xunit.Abstractions;

namespace Vera.Portugal.Tests
{
    public class ThermalReceiptGeneratorTests
    {
        private readonly ITestOutputHelper _testOutputHelper;

        public ThermalReceiptGeneratorTests(ITestOutputHelper testOutputHelper)
        {
            _testOutputHelper = testOutputHelper;
        }

        [Fact]
        public void Should_generate_receipt()
        {
            // TODO(kevin): use invoice generator to generate test cases
            // TODO(kevin): test that receipt contains correct values

            var invoice = new Invoice
            {
                Supplier = new Supplier
                {
                    Name = "Lisboa store",
                    RegistrationNumber = "123.123.123",
                    Address = new Address
                    {
                        City = "Lisboa",
                        Number = "180",
                        PostalCode = "1500",
                        Street = "Lalala"
                    },
                },
                Employee = new Employee
                {
                    SystemId = "007",
                    FirstName = "Kevin"
                },
                TerminalId = "ST01.44",
                Date = DateTime.UtcNow,
                FiscalPeriod = 1,
                FiscalYear = 2020,
                Number = "123",
                OrderReferences = new List<string>{"3001"},
                Lines = new List<InvoiceLine>
                {
                    new()
                    {
                        Gross = 15m,
                        Description = "Shower foam dao",
                        Quantity = 2,
                        Taxes = new Taxes
                        {
                            Rate = 1.21m
                        }
                    }
                },
                Signature = new Signature
                {
                    Output = new byte[32]
                },
                Payments = new List<Payment>
                {
                    new()
                    {
                        Amount = 10m,
                        Description = "Cash"
                    },
                    new()
                    {
                        Amount = 5m,
                        Description = "Debit"
                    }
                },
                // TODO(kevin): pin receipt
                // Receipts = new List<PinReceipt>
                // {
                //     new PinReceipt
                //     {
                //         Lines = new[]
                //         {
                //             "PIN RECEIPT",
                //             "LINES",
                //             "GO",
                //             "HERE",
                //             "MASTERCARD ***123"
                //         }
                //     }
                // }
            };

            invoice.Totals = new InvoiceTotalsCalculator().Calculate(invoice);

            var account = new Account
            {
                Name = "Rituals Portugal",
                RegistrationNumber = "123.123.123",
                Address = new Address
                {
                    City = "Lisboa",
                    Number = "180",
                    PostalCode = "1500",
                    Street = "Lalala"
                }
            };

            var context = new ThermalReceiptContext
            {
                Account = account,
                Invoice =  invoice,
                Original = true
            };

            var generator = new ThermalReceiptGenerator(258501m, "PELICAN THEORY", "9999");
            var node = generator.Generate(context);

            var sb = new StringBuilder();

            var visitor = new StringThermalVisitor(new StringWriter(sb));
            node.Accept(visitor);

            var result = sb.ToString();

            var lines = result.Split(Environment.NewLine);
            foreach (var line in lines)
            {
                Assert.True(line.Length <= 48, $"{line} - exceeds length by {line.Length - 48} chars");
            }

            // Assert.Contains("FATURA SIMPLIFICADA", result);
            // Assert.DoesNotContain("NOTA DE CRÉDITO", result);

            _testOutputHelper.WriteLine(result);
        }
    }
}