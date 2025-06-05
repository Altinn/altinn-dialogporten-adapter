using Azure.Data.Tables;

namespace Altinn.DialogportenAdapter.EventSimulator.Features.Migration;

internal sealed class AzureTableMigrator : IHostedService
{
    private readonly IHostEnvironment _env;
    private readonly Settings _settings;

    public AzureTableMigrator(IHostEnvironment env, Settings settings)
    {
        _env = env ?? throw new ArgumentNullException(nameof(env));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        var serviceClient = new TableServiceClient(_settings.DialogportenAdapter.AzureStorage.ConnectionString);
        await serviceClient.CreateTableIfNotExistsAsync(AzureStorageSettings.GetTableName(_env), cancellationToken);
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

