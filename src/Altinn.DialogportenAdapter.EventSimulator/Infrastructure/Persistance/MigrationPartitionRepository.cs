using System.Collections.ObjectModel;
using System.Globalization;
using System.Reflection;
using Altinn.DialogportenAdapter.EventSimulator.Common.Extensions;
using Azure.Data.Tables;

namespace Altinn.DialogportenAdapter.EventSimulator.Infrastructure.Persistance;

internal sealed class MigrationPartitionRepository : IMigrationPartitionRepository
{
    private readonly TableClient _tableClient;
    private readonly ILogger<MigrationPartitionRepository> _logger;

    public MigrationPartitionRepository(TableClient tableClient, ILogger<MigrationPartitionRepository> logger)
    {
        _tableClient = tableClient ?? throw new ArgumentNullException(nameof(tableClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ReadOnlyCollection<MigrationPartitionEntity>> GetExistingPartitions(
        List<MigrationPartitionEntity> partitions,
        CancellationToken cancellationToken)
    {
        if (partitions.Count == 0)
        {
            return Array.Empty<MigrationPartitionEntity>().AsReadOnly();
        }

        var partitionKeyStrings = partitions
            .GroupBy(x => x.Partition)
            .Select(x => x.Key.ToPartitionKey())
            .ToList();

        var filter = string.Join(" or ", partitionKeyStrings.Select(pk => $"PartitionKey eq '{pk}'"));

        var queryResult = _tableClient.QueryAsync<TableEntity>(
            filter: filter,
            maxPerPage: 1000,
            select: [nameof(TableEntity.PartitionKey), nameof(TableEntity.RowKey)],
            cancellationToken: cancellationToken);

        var partitionsByKey = partitions
            .GroupBy(x => (x.Partition, x.Organization))
            .ToDictionary(x => x.Key, x => x.Single());

        var result = new List<MigrationPartitionEntity>();
        await foreach (var entity in queryResult)
        {
            var key = (entity.PartitionKey.ToDateOnly(), entity.RowKey);
            if (partitionsByKey.TryGetValue(key, out var partition))
            {
                result.Add(partition);
            }
        }

        return result.AsReadOnly();
    }

    public async Task<MigrationPartitionEntity?> Get(DateOnly partition, string organization, CancellationToken cancellationToken)
    {
        var entity = await _tableClient.GetEntityIfExistsAsync<MigrationPartitionEntity>(
            partitionKey: partition.ToPartitionKey(),
            rowKey: organization.ToRowKey(),
            cancellationToken: cancellationToken);

        return entity.Value;
    }

    public async Task Upsert(List<MigrationPartitionEntity> partitionEntities, CancellationToken cancellationToken)
    {
        if (partitionEntities.Count == 0)
            return;

        var batchTasks = partitionEntities
            .GroupBy(e => e.PartitionKey)
            .SelectMany(group => group.Chunk(100))
            .Select(async (chunk, batchIndex) =>
            {
                var actions = chunk
                    .Select(entity => new TableTransactionAction(TableTransactionActionType.UpsertReplace, entity))
                    .ToList();

                var pk = chunk.First().PartitionKey;

                try
                {
                    await _tableClient.SubmitTransactionAsync(actions, cancellationToken);
                }
                catch (TableTransactionFailedException ex)
                {
                    var failedIndex = ex.FailedTransactionActionIndex ?? -1;

                    // Build a compact, single log entry with all meaningful details
                    string actionType = "<n/a>";
                    string failedPk = pk;
                    string failedRk = "<n/a>";
                    string properties = "<n/a>";

                    if (failedIndex >= 0 && failedIndex < actions.Count)
                    {
                        var failedAction = actions[failedIndex];
                        actionType = failedAction.ActionType.ToString();

                        if (failedAction.Entity is MigrationPartitionEntity mpe)
                        {
                            failedPk = mpe.PartitionKey;
                            failedRk = mpe.RowKey;
                            properties = FormatEntityProperties(mpe);
                        }
                        else if (failedAction.Entity is ITableEntity te)
                        {
                            failedPk = te.PartitionKey;
                            failedRk = te.RowKey;
                            properties = FormatITableEntityProperties(te);
                        }
                    }

                    _logger.LogError(ex,
                        "Batch {BatchIndex} FAILED. Status: {Status}, ErrorCode: {ErrorCode}, FailedIndex: {FailedIndex}, Action: {Action}, PK: {FailedPK}, RK: {FailedRK}, Properties: {Properties}",
                        batchIndex, ex.Status, ex.ErrorCode, failedIndex, actionType, failedPk, failedRk, properties);

                    if (failedIndex < 0 || failedIndex >= actions.Count)
                    {
                        _logger.LogWarning("No valid failed index provided — failure may be at the changeset level.");
                    }

                    throw; // preserve existing behavior
                }
            });

        await Task.WhenAll(batchTasks);
    }

    public async Task Truncate(CancellationToken cancellationToken = default)
    {
        const int batchSize = 100; // Max allowed per batch
        var deleteTasks = new List<Task>();

        await foreach (var page in _tableClient
                           .QueryAsync<TableEntity>(maxPerPage: batchSize, cancellationToken: cancellationToken)
                           .AsPages()
                           .WithCancellation(cancellationToken))
        {
            var groups = page.Values.GroupBy(e => e.PartitionKey);

            foreach (var group in groups)
            {
                var batch = new List<TableTransactionAction>();

                foreach (var entity in group)
                {
                    batch.Add(new TableTransactionAction(TableTransactionActionType.Delete, entity));

                    if (batch.Count == batchSize)
                    {
                        deleteTasks.Add(_tableClient.SubmitTransactionAsync(batch, cancellationToken));
                        batch = new List<TableTransactionAction>();
                    }
                }

                if (batch.Count > 0)
                {
                    deleteTasks.Add(_tableClient.SubmitTransactionAsync(batch, cancellationToken));
                }
            }
        }

        await Task.WhenAll(deleteTasks);
        _logger.LogInformation("Truncate completed.");
    }

    // ------- Helpers (ILogger-based) -------

    private static string FormatEntityProperties(object entity)
    {
        var pairs = new List<string>();

        foreach (var prop in entity.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
        {
            if (prop.Name is nameof(ITableEntity.PartitionKey) or nameof(ITableEntity.RowKey) or nameof(ITableEntity.Timestamp) or nameof(ITableEntity.ETag))
                continue;

            try
            {
                var value = prop.GetValue(entity);
                pairs.Add($"{prop.Name}={FormatValue(value)}");
            }
            catch (Exception ex)
            {
                pairs.Add($"{prop.Name}=<error reading: {ex.Message}>");
            }
        }

        return string.Join(", ", pairs);
    }

    private static string FormatITableEntityProperties(ITableEntity e)
    {
        if (e is TableEntity te)
        {
            var pairs = new List<string>();

            foreach (var kv in te)
            {
                if (kv.Key is nameof(ITableEntity.PartitionKey) or nameof(ITableEntity.RowKey) or nameof(ITableEntity.Timestamp) or nameof(ITableEntity.ETag))
                    continue;

                pairs.Add($"{kv.Key}={FormatValue(kv.Value)}");
            }

            return string.Join(", ", pairs);
        }

        return FormatEntityProperties(e);
    }

    private static string FormatValue(object? v) =>
        v switch
        {
            null => "<null>",
            byte[] bytes => $"[byte[{bytes.Length}]]",
            DateTime dt => $"{dt:o} (DateTime) # Prefer DateTimeOffset",
            DateTimeOffset dto => dto.ToString("o"),
            _ => v.ToString() ?? "<null>"
        };
}
