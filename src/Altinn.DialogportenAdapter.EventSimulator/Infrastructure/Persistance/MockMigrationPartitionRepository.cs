namespace Altinn.DialogportenAdapter.EventSimulator.Infrastructure.Persistance;

internal sealed class MockMigrationPartitionRepository : IMigrationPartitionRepository
{
    public Task<List<MigrationPartitionEntity>> GetExistingPartitions(List<MigrationPartitionEntity> partitions, CancellationToken cancellationToken)
    {
        return  Task.FromResult(new List<MigrationPartitionEntity>());
    }

    public Task<MigrationPartitionEntity?> Get(DateOnly partition, string organization, CancellationToken cancellationToken)
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