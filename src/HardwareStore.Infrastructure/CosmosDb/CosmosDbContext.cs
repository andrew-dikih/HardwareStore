namespace HardwareStore.Infrastructure.CosmosDb;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;

[ExcludeFromCodeCoverage]
public class CosmosDbContext
{
    private readonly CosmosClient _client;
    private readonly string _databaseName;
    private readonly string _containerName;

    public CosmosDbContext(IOptions<CosmosDbSettings> settings)
    {
        var opts = settings.Value;
        var clientOptions = new CosmosClientOptions
        {
            SerializerOptions = new CosmosSerializationOptions
            {
                PropertyNamingPolicy = CosmosPropertyNamingPolicy.CamelCase
            }
        };
        if (opts.AllowInsecure)
        {
            clientOptions.HttpClientFactory = () => new System.Net.Http.HttpClient(
                new System.Net.Http.HttpClientHandler
                {
                    ServerCertificateCustomValidationCallback =
                        System.Net.Http.HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
                }, disposeHandler: true);
            clientOptions.ConnectionMode = ConnectionMode.Gateway;
        }
        _client = new CosmosClient(opts.ConnectionString, clientOptions);
        _databaseName = opts.DatabaseName;
        _containerName = opts.ContainerName;
    }

    public Container GetContainer() =>
        _client.GetContainer(_databaseName, _containerName);

    public async Task InitializeAsync()
    {
        var db = await _client.CreateDatabaseIfNotExistsAsync(_databaseName);
        await db.Database.CreateContainerIfNotExistsAsync(
            new ContainerProperties(_containerName, "/documentType"));
    }
}
