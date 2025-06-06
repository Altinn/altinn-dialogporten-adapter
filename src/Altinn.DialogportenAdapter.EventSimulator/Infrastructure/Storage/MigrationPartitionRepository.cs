using Azure.Data.Tables;

namespace Altinn.DialogportenAdapter.EventSimulator.Infrastructure.Storage;

internal sealed class MigrationPartitionRepository
{
    private readonly TableClient _tableClient;

    public MigrationPartitionRepository(TableClient tableClient)
    {
        _tableClient = tableClient ?? throw new ArgumentNullException(nameof(tableClient));
    }

    public async Task<List<MigrationPartitionEntity>> GetExistingPartitions(
        List<MigrationPartitionEntity> partitions,
        CancellationToken cancellationToken)
    {
        if (partitions.Count == 0)
        {
            return [];
        }

        var partitionKeyStrings = partitions
            .GroupBy(x => x.Partition)
            .Select(x => x.Key.ToString())
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
            var key = (DateOnly.Parse(entity.PartitionKey), entity.RowKey);
            if (partitionsByKey.TryGetValue(key, out var partition))
            {
                result.Add(partition);
            }
        }

        return result;
    }

    public async Task<MigrationPartitionEntity?> Get(DateOnly partition, string organization, CancellationToken cancellationToken)
    {
        var entity = await _tableClient.GetEntityIfExistsAsync<MigrationPartitionEntity>(
            partitionKey: partition.ToString(),
            rowKey: organization,
            cancellationToken: cancellationToken);
        return entity.Value;
    }

    public async Task Upsert(List<MigrationPartitionEntity> partitionEntities, CancellationToken cancellationToken)
    {
        if (partitionEntities.Count == 0)
        {
            return;
        }

        var groupsByPartition = partitionEntities
            .GroupBy(e => e.PartitionKey)
            .SelectMany(x => x.Chunk(100))
            .Select(chunkedPartitionBatch => chunkedPartitionBatch
                .Select(entity => new TableTransactionAction(TableTransactionActionType.UpsertMerge, entity)))
            .Select(batch => _tableClient.SubmitTransactionAsync(batch, cancellationToken));

        await Task.WhenAll(groupsByPartition);
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
            // Group entities by PartitionKey for batch deletes
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
    }
}