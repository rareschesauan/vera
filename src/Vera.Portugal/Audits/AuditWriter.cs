using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Serialization;
using Vera.Audits;
using Vera.Portugal.Models;
using Vera.Portugal.Stores;

namespace Vera.Portugal.Audits
{
    public class AuditWriter : IAuditWriter
    {
        private readonly string _productCompanyTaxId;
        private readonly string _certificateName;
        private readonly string _certificateNumber;

        private readonly IWorkingDocumentStore _wdStore;

        public AuditWriter(string productCompanyTaxId, string certificateName, string certificateNumber,
            IWorkingDocumentStore wdStore)
        {
            _productCompanyTaxId = productCompanyTaxId;
            _certificateName = certificateName;
            _certificateNumber = certificateNumber;
            _wdStore = wdStore;
        }

        public Task<string> ResolveName(string supplierSystemId, int sequence, int total)
        {
            return Task.FromResult($"{supplierSystemId}-{DateTime.UtcNow:yyyyMMdd}-{sequence}_{total}.xml");
        }

        public async Task Write(AuditContext context, AuditCriteria criteria, Stream stream)
        {
            var creator = new AuditCreator(_productCompanyTaxId, _certificateName, _certificateNumber, _wdStore);
            var model = await creator.Create(context, criteria);

            var settings = new XmlWriterSettings
            {
                Indent = true,
                // Windows-1252
                Encoding = Encoding.GetEncoding(1252),
                CloseOutput = false
            };

            using var writer = XmlWriter.Create(stream, settings);
            var serializer = new XmlSerializer(typeof(AuditFile));
            serializer.Serialize(writer, model);
        }
    }
}