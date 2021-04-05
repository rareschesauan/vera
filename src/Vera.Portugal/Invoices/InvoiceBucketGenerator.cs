using System.Text;
using Vera.Invoices;
using Vera.Models;

namespace Vera.Portugal.Invoices
{
    public sealed class InvoiceBucketGenerator : IInvoiceBucketGenerator
    {
        public string Generate(Invoice invoice)
        {
            var sb = new StringBuilder(invoice.Supplier.SystemId)
                .Append('-')
                .Append(InvoiceTypeHelper.DetermineType(invoice));

            if (invoice.Manual)
            {
                sb.Append("-m");
            }

            // format: {StoreNumber}-{Type}[-m]
            return sb.ToString();
        }
    }
}