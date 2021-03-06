using Microsoft.Azure.Cosmos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Vera.Azure.Extensions;
using Vera.Models;
using Vera.Stores;

namespace Vera.Azure.Stores
{
    public class CosmosAccountStore : IAccountStore
    {
        private const string DocumentType = "account";

        private readonly Container _container;

        public CosmosAccountStore(Container container)
        {
            _container = container;
        }

        public Task Store(Account account)
        {
            var document = ToDocument(account);

            return _container.CreateItemAsync(document, new PartitionKey(document.PartitionKey));
        }

        public Task Update(Account account)
        {
            var document = ToDocument(account);

            return _container.ReplaceItemAsync(
                document,
                document.Id.ToString(),
                new PartitionKey(document.PartitionKey)
            );
        }

        public async Task<Account?> Get(Guid companyId, Guid accountId)
        {
            try
            {
                var document = await _container.ReadItemAsync<TypedDocument<Account>>(
                    accountId.ToString(),
                    new PartitionKey(companyId.ToString())
                );

                return document.Resource.Value;
            }
            catch (CosmosException e) when (e.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }
        }

        public Task<ICollection<Account>> GetByCompany(Guid companyId)
        {
            var queryable = _container.GetItemLinqQueryable<TypedDocument<Account>>(requestOptions: new QueryRequestOptions
                {
                    PartitionKey = new PartitionKey(companyId.ToString())
                })
                .Where(x => x.Type == DocumentType);

            return queryable.ToListAsync();
        }

        private static TypedDocument<Account> ToDocument(Account account)
        {
            return new(
                a => a.Id,
                a => a.CompanyId.ToString(),
                account,
                DocumentType
            );
        }
    }
}