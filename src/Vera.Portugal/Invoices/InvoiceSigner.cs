using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using Vera.Models;
using Vera.Signing;

namespace Vera.Portugal.Invoices
{
    /// <summary>
    /// Creates a signature for an invoice based on the document "Order No. 8632/2014 of the 3 of July".
    /// </summary>
    public sealed class InvoiceSigner : IInvoiceSigner
    {
        private readonly RSA _rsa;
        private readonly int _privateKeyVersion;

        public InvoiceSigner(RSA rsa, int privateKeyVersion)
        {
            _rsa = rsa ?? throw new NullReferenceException(nameof(rsa));
            _privateKeyVersion = privateKeyVersion;
        }

        public Task<Signature> Sign(Invoice invoice, Signature previousSignature)
        {
            const char separator = ';';

            var formattedDate = invoice.Date.ToString("yyyy-MM-dd");
            var systemEntryDate = invoice.Date.ToString("yyyy-MM-ddTHH:mm:ss");
            var grossTotal = Math.Abs(invoice.Totals.Gross).ToString("0.00", CultureInfo.InvariantCulture);

            var signatureBuilder = new StringBuilder()
                .Append(formattedDate)
                .Append(separator)
                .Append(systemEntryDate)
                .Append(separator)
                .Append(invoice.Number)
                .Append(separator)
                .Append(grossTotal)
                .Append(separator);

            if (previousSignature != null)
            {
                signatureBuilder.Append(Convert.ToBase64String(previousSignature.Output));
            }

            var signature = signatureBuilder.ToString();

            var result = _rsa.SignData(
                Encoding.UTF8.GetBytes(signature),
                HashAlgorithmName.SHA1,
                RSASignaturePadding.Pkcs1
            );

            return Task.FromResult(new Signature
            {
                Input = signature,
                Output = result,
                Version = _privateKeyVersion
            });
        }
    }
}