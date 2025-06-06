using Azure.Data.Tables;

namespace Altinn.DialogportenAdapter.EventSimulator.Common.StartupLoaders;

internal sealed class AzureTableStartupLoader : IStartupLoader
{
    private readonly IHostEnvironment _env;
    private readonly Settings _settings;

    public AzureTableStartupLoader(IHostEnvironment env, Settings settings)
    {
        _env = env ?? throw new ArgumentNullException(nameof(env));
        _settings = settings ?? throw new ArgumentNullException(nameof(settings));
    }

    public Task Load(CancellationToken cancellationToken)
    {
        var serviceClient = new TableServiceClient(_settings.DialogportenAdapter.AzureStorage.ConnectionString);
        return serviceClient.CreateTableIfNotExistsAsync(AzureStorageSettings.GetTableName(_env), cancellationToken);
    }
}