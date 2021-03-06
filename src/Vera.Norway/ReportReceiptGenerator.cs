using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Vera.Documents.Nodes;
using Vera.Extensions;
using Vera.Models;
using Vera.Reports;

namespace Vera.Norway
{
    public class ReportReceiptGenerator : IReportReceiptGenerator
    {
        private static readonly CultureInfo Culture = CultureInfo.CreateSpecificCulture("no-NO");

        public IThermalNode Generate(ReceiptReportContext context)
        {
            var nodes = new List<IThermalNode>();

            nodes.AddRange(GenerateHeader(context));
            nodes.Add(new SpacingThermalNode(1));

            nodes.AddRange(GenerateLines(context));
            nodes.Add(new SpacingThermalNode(1));

            nodes.AddRange(GenerateFooter(context));

            return new DocumentThermalNode(nodes);
        }

        private IEnumerable<IThermalNode> GenerateHeader(ReceiptReportContext context)
        {
            var account = context.RegisterReport.Account;

            yield return new TextThermalNode(context.Header)
            {
                Bold = true,
                FontSize = FontSize.Large
            };

            var reportType = context.RegisterReport.Type switch
            {
                RegisterReportType.Current => "X",
                RegisterReportType.EndOfDay => "Z",
                _ => throw new ArgumentOutOfRangeException()
            };

            yield return new TextThermalNode($"{reportType} Report #{context.RegisterReport.Number}")
            {
                Bold = true,
                FontSize = FontSize.Large
            };

            yield return new TextThermalNode(account.Name);
            yield return new TextThermalNode($"ORG NR: {account.TaxRegistrationNumber}");
            yield return new TextThermalNode($"Foretaksregisteret");

            yield return new SpacingThermalNode(1);

            var date = context.RegisterReport.Date;
            yield return new TextThermalNode($"Dato: {date:yyyy-MM-dd}");
            yield return new TextThermalNode($"Tid: {date:HH:mm:ss}");
        }

        private IEnumerable<IThermalNode> GenerateLines(ReceiptReportContext context)
        {
            const string format = "{0,-30}{1,-10}";
            var report = context.RegisterReport;
            var sb = new StringBuilder();
            var totals = report.Totals;

            sb.AppendFormat(
                format,
                $"Total cash sales",
                totals.Cash.FormatCurrency(Culture)
            );
            yield return new TextThermalNode(sb.ToString());

            sb.Clear();
            sb.AppendFormat(
                format,
                "Change",
                context.RegisterReport.RegisterOpeningAmount.FormatCurrency(Culture)
            );
            yield return new TextThermalNode(sb.ToString());

            sb.Clear();
            sb.AppendFormat(
                format,
                "Cash drawer openings",
                context.RegisterReport.CashDrawerOpenings
            );
            yield return new TextThermalNode(sb.ToString());

            sb.Clear();
            sb.AppendFormat(
                format,
                "Payments",
                report.Payments.Count
            );
            yield return new TextThermalNode(sb.ToString());

            foreach (var payment in report.Payments)
            {
                sb.Clear();
                sb.AppendFormat(
                    format,
                    $"{payment.PaymentCategory}({payment.Count})",
                    payment.Amount.FormatCurrency(Culture)
                );
                yield return new TextThermalNode(sb.ToString());
            }

            sb.Clear();
            sb.AppendFormat(
                format,
                "Taxes",
                report.Taxes.Count
            );
            yield return new TextThermalNode(sb.ToString());

            foreach (var tax in report.Taxes)
            {
                sb.Clear();
                sb.AppendFormat(
                    format,
                    $"{tax.TaxesCategory}({tax.TaxRate.FormatTaxRate(Culture)})",
                    tax.Amount.FormatCurrency(Culture)
                );
                yield return new TextThermalNode(sb.ToString());
            }

            sb.Clear();
            sb.AppendFormat(
                format,
                "Payments per employee",
                report.PaymentsPerEmployee.Count
            );
            yield return new TextThermalNode(sb.ToString());

            foreach (var employeePayments in report.PaymentsPerEmployee)
            {
                var payment = employeePayments.Payment;

                sb.Clear();
                sb.AppendFormat(
                    format,
                    $"{employeePayments.Employee.FullName}({payment.Count})",
                    $"{payment.Amount.FormatCurrency(Culture)} ({payment.PaymentCategory})"
                );
                yield return new TextThermalNode(sb.ToString());
            }

            sb.Clear();
            sb.AppendFormat(
                format,
                "Product groups",
                report.Products.Count
            );
            yield return new TextThermalNode(sb.ToString());

            foreach (var product in report.Products)
            {
                sb.Clear();
                sb.AppendFormat(
                    format,
                    $"{product.Type}({product.Count})",
                    product.Amount.FormatCurrency(Culture)
                );
                yield return new TextThermalNode(sb.ToString());
            }

            sb.Clear();
            sb.AppendFormat(
                format,
                "Number of discounts",
                report.Discount.Count
            );
            yield return new TextThermalNode(sb.ToString());

            sb.Clear();
            sb.AppendFormat(
                format,
                "Total amount of discounts",
                report.Discount.Amount.FormatCurrency(Culture)
            );
            yield return new TextThermalNode(sb.ToString());

            sb.Clear();
            sb.AppendFormat(
                format,
                "Number of returns",
                report.Return.Count
            );
            yield return new TextThermalNode(sb.ToString());

            sb.Clear();
            sb.AppendFormat(
                format,
                "Total amount of returns",
                report.Return.Amount.FormatCurrency(Culture)
            );
            yield return new TextThermalNode(sb.ToString());

            sb.Clear();
            sb.AppendFormat(
                format,
                "Number of copies",
                context.Prints.Count
            );
            yield return new TextThermalNode(sb.ToString());

            sb.Clear();
            sb.AppendFormat(
                format,
                $"Grand total (cash)",
                totals.Cash.FormatCurrency(Culture)
            );
            yield return new TextThermalNode(sb.ToString());

            sb.Clear();
            sb.AppendFormat(
                format,
                $"Grand total",
                totals.Gross.FormatCurrency(Culture)
            );
            yield return new TextThermalNode(sb.ToString());

            sb.Clear();
            sb.AppendFormat(
                format,
                $"Grand total (returns)",
                totals.Return.FormatCurrency(Culture)
            );
            yield return new TextThermalNode(sb.ToString());

            sb.Clear();
            sb.AppendFormat(
                format,
                $"Grand total (net)",
                totals.Net.FormatCurrency(Culture)
            );
            yield return new TextThermalNode(sb.ToString());
        }


        private IEnumerable<IThermalNode> GenerateFooter(ReceiptReportContext context)
        {
            yield return new TextThermalNode($"Kassapunkt ID: {context.RegisterReport.RegisterSystemId}");

            yield return new SpacingThermalNode(1);

            var signature = Convert.ToBase64String(context.Signature);
            yield return new QRCodeThermalNode(signature);
        }
    }
}
