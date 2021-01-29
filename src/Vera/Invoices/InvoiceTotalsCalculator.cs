using System;
using Vera.Models;

namespace Vera.Invoices
{
    public class InvoiceTotalsCalculator
    {
        // TODO(kevin): do the rounding to 2 decimals here already?
        public Totals Calculate(Invoice invoice)
        {
            var totals = new Totals();
            var table = totals.Taxes;

            foreach (var line in invoice.Lines)
            {
                var entry = line.Taxes.Category switch
                {
                    TaxesCategory.High => table.High ??= new TaxTable.Entry(line.Taxes.Rate),
                    TaxesCategory.Low => table.Low ??= new TaxTable.Entry(line.Taxes.Rate),
                    TaxesCategory.Zero => table.Zero ??= new TaxTable.Entry(line.Taxes.Rate),
                    TaxesCategory.Exempt => table.Exempt ??= new TaxTable.Entry(line.Taxes.Rate),
                    TaxesCategory.Intermediate => table.Intermediate ??= new TaxTable.Entry(line.Taxes.Rate),
                    _ => throw new ArgumentOutOfRangeException(nameof(line.Taxes.Category), "Unknown tax category")
                };

                entry.Base += line.Net;
                entry.Value += line.Gross - line.Net;

                totals.Net += line.Net;
                totals.Gross += line.Gross;
            }

            return totals;
        }
    }
}