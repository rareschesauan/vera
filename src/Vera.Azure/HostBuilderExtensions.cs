using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Azure.Cosmos;
using Microsoft.Azure.Cosmos.Fluent;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Serilog;
using Vera.Azure.Concurrency;
using Vera.Azure.Stores;
using Vera.Concurrency;
using Vera.Stores;

namespace Vera.Azure
{
    public static class HostBuilderExtensions
    {
        private static readonly SemaphoreSlim Lock = new(1, 1);
        private static volatile bool _created;

        public static IHost ConfigureCosmos(this IHost host)
        {
            if (_created) return host;

            // Running on the background to prevent deadlocks that occur
            // when running the async code below synchronously and I did
            // not find a way to run this before startup with the IHostBuilder
            var task = Task.Run(async () =>
            {
                if (_created) return;

                if (!await Lock.WaitAsync(TimeSpan.FromSeconds(30)))
                {
                    throw new TimeoutException("failed to acquire lock for configuring cosmos in time");
                }

                if (_created)
                {
                    Lock.Release();
                    return;
                }

                using var scope = host.Services.CreateScope();

                var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

                var cosmosOptions = config
                    .GetSection(CosmosOptions.Section)
                    .Get<CosmosOptions>();

                var cosmosContainerOptions = config
                    .GetSection(CosmosContainerOptions.Section)
                    .Get<CosmosContainerOptions>() ?? new CosmosContainerOptions();

                var client = scope.ServiceProvider.GetRequiredService<CosmosClient>();

                var throughput = ThroughputProperties.CreateManualThroughput(400);

                var response = await client.CreateDatabaseIfNotExistsAsync(cosmosOptions.Database, throughput);

                const string partitionKeyPath = "/PartitionKey";

                var containers = new[]
                {
                    cosmosContainerOptions.Companies,
                    cosmosContainerOptions.Invoices,
                    cosmosContainerOptions.Audits,
                    cosmosContainerOptions.Trails,
                    cosmosContainerOptions.Chains,
                    cosmosContainerOptions.Periods,
                    cosmosContainerOptions.Documents,
                    cosmosContainerOptions.EventLogs,
                    cosmosContainerOptions.Registers,
                };

                var db = response.Database;

                foreach (var container in containers)
                {
                    await db
                        .DefineContainer(container, partitionKeyPath)
                        .CreateIfNotExistsAsync();
                }

                _created = true;

                Lock.Release();
            });

            var start = DateTime.Now;

            while (!task.IsCompleted && (DateTime.Now - start).TotalSeconds < 15) { }

            if (!_created)
            {
                var additional = "";

                if (task.Exception != null)
                {
                    foreach (var e in task.Exception.InnerExceptions)
                    {
                        additional += e.Message + Environment.NewLine;
                    }
                }

                throw new Exception($"task is completed, but cosmos containers were not created: {task.Status}. {additional}");
            }

            return host;
        }

        public static IHostBuilder UseAzure(this IHostBuilder builder)
        {
            return builder
                .UseCosmosStores()
                .UseAzureBlobs();
        }

        /// <summary>
        /// Use Cosmos as the main backing store for the entities.
        /// </summary>
        /// <param name="builder"></param>
        /// <returns></returns>
        /// <seealso cref="CosmosOptions"/>
        /// <seealso cref="CosmosContainerOptions"/>
        public static IHostBuilder UseCosmosStores(this IHostBuilder builder)
        {
            return builder.ConfigureServices((context, collection) =>
            {
                var cosmosOptions = context.Configuration
                    .GetSection(CosmosOptions.Section)
                    .Get<CosmosOptions>();

                var cosmosContainerOptions = context.Configuration
                    .GetSection(CosmosContainerOptions.Section)
                    .Get<CosmosContainerOptions>() ?? new CosmosContainerOptions();

                if (string.IsNullOrEmpty(cosmosOptions.ConnectionString))
                {
                    Log.Error("cannot register cosmos stores because connection string is missing");
                    return;
                }

                if (string.IsNullOrEmpty(cosmosOptions.Database))
                {
                    Log.Error("cannot register cosmos stores because database is missing");
                    return;
                }

                var cosmosClient = new CosmosClientBuilder(cosmosOptions.ConnectionString)
                    .WithContentResponseOnWrite(false)
                    .WithConnectionModeDirect()
                    .WithRequestTimeout(TimeSpan.FromSeconds(5))
                    .WithThrottlingRetryOptions(TimeSpan.FromSeconds(6), 10)
                    .WithApplicationName("vera")
                    .Build();

                collection.AddSingleton(cosmosClient);

                collection.AddSingleton<IInvoiceStore>(_ => new CosmosInvoiceStore(
                    cosmosClient.GetContainer(cosmosOptions.Database, cosmosContainerOptions.Invoices)));

                collection.AddSingleton<ICompanyStore>(_ => new CosmosCompanyStore(
                    cosmosClient.GetContainer(cosmosOptions.Database, cosmosContainerOptions.Companies)));

                collection.AddSingleton<IAccountStore>(_ => new CosmosAccountStore(
                    cosmosClient.GetContainer(cosmosOptions.Database, cosmosContainerOptions.Companies)));

                collection.AddSingleton<IUserStore>(_ => new CosmosUserStore(
                    cosmosClient.GetContainer(cosmosOptions.Database, cosmosContainerOptions.Companies)));

                collection.AddSingleton<ISupplierStore>(_ => new CosmosSupplierStore(
                    cosmosClient.GetContainer(cosmosOptions.Database, cosmosContainerOptions.Companies)));

                collection.AddSingleton<IAuditStore>(_ => new CosmosAuditStore(
                    cosmosClient.GetContainer(cosmosOptions.Database, cosmosContainerOptions.Audits)));

                collection.AddSingleton<IPrintAuditTrailStore>(_ => new CosmosPrintAuditTrailStore(
                    cosmosClient.GetContainer(cosmosOptions.Database, cosmosContainerOptions.Trails)));

                collection.AddSingleton<IChainStore>(_ => new CosmosChainStore(
                    cosmosClient.GetContainer(cosmosOptions.Database, cosmosContainerOptions.Chains)));

                collection.AddSingleton<IPeriodStore>(_ => new CosmosPeriodStore(
                    cosmosClient.GetContainer(cosmosOptions.Database, cosmosContainerOptions.Periods)));

                collection.AddSingleton<IReportStore>(_ => new CosmosReportStore(
                    cosmosClient.GetContainer(cosmosOptions.Database, cosmosContainerOptions.Documents)));

                collection.AddSingleton<IEventLogStore>(_ => new CosmosEventLogStore(
                    cosmosClient.GetContainer(cosmosOptions.Database, cosmosContainerOptions.EventLogs)));

                collection.AddSingleton<IRegisterStore>(_ => new CosmosRegisterStore(
                    cosmosClient.GetContainer(cosmosOptions.Database, cosmosContainerOptions.Registers)));

                collection.AddSingleton<IMessageTemplateStore>(_ => new CosmosMessageTemplateStore(
                    cosmosClient.GetContainer(cosmosOptions.Database, cosmosContainerOptions.Companies)));
            });
        }

        public static IHostBuilder UseAzureBlobs(this IHostBuilder builder)
        {
            return builder.ConfigureServices((context, collection) =>
            {
                // Do not configure Azure dependency when running in development mode
                if (context.HostingEnvironment.IsDevelopment())
                {
                    Log.Warning("not using Azure blob storage because we're running in development mode");
                    return;
                }

                var blobConnectionString = context.Configuration["VERA:BLOB:CONNECTIONSTRING"];

                if (string.IsNullOrEmpty(blobConnectionString))
                {
                    Log.Warning("not using Azure blob storage because the connection string is empty");
                    return;
                }

                collection.Replace(ServiceDescriptor.Singleton<ILocker>(new AzureBlobLocker(blobConnectionString)));
                collection.Replace(ServiceDescriptor.Singleton<IBlobStore>(new AzureBlobStore(blobConnectionString)));
            });
        }
    }
}
