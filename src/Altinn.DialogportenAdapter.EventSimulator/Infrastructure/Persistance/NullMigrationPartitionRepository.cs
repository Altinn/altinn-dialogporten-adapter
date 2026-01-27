using System.Collections.ObjectModel;

namespace Altinn.DialogportenAdapter.EventSimulator.Infrastructure.Persistance;

internal sealed class NullMigrationPartitionRepository : IMigrationPartitionRepository
{
    public Task<ReadOnlyCollection<MigrationPartitionEntity>> GetExistingPartitions(List<MigrationPartitionEntity> partitions, CancellationToken cancellationToken)
    {
        return Task.FromResult(Array.Empty<MigrationPartitionEntity>().AsReadOnly());
    }

    public Task<MigrationPartitionEntity?> GetMigrationPartition(DateOnly partition, string organization, CancellationToken cancellationToken)
    {
        return Task.FromResult<MigrationPartitionEntity?>(null);
    }

    public Task Upsert(List<MigrationPartitionEntity> partitionEntities, CancellationToken cancellationToken)
    {
        return Task.CompletedTask;
    }

    public Task Truncate(CancellationToken cancellationToken = default)
    {
        return Task.CompletedTask;
    }
}