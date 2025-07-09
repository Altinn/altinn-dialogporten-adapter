using System.Collections.ObjectModel;
using System.Runtime.Serialization;
using Azure;
using Azure.Data.Tables;

namespace Altinn.DialogportenAdapter.EventSimulator.Infrastructure.Persistance;

public interface IMigrationPartitionRepository
{
    Task<ReadOnlyCollection<MigrationPartitionEntity>> GetExistingPartitions(
        List<MigrationPartitionEntity> partitions,
        CancellationToken cancellationToken);

    Task<MigrationPartitionEntity?> Get(DateOnly partition, string organization, CancellationToken cancellationToken);
    Task Upsert(List<MigrationPartitionEntity> partitionEntities, CancellationToken cancellationToken);
    Task Truncate(CancellationToken cancellationToken = default);
}

public sealed class MigrationPartitionEntity : ITableEntity
{
    [Obsolete("Used by Table Storage SDK, do not use directly.", error: true)]
    public MigrationPartitionEntity() { }

    public MigrationPartitionEntity(DateOnly partition, string organization)
    {
        Partition = partition;
        Organization = organization ?? throw new ArgumentNullException(nameof(organization));
    }

    [IgnoreDataMember]
    public DateOnly Partition { get; private set; }

    [IgnoreDataMember]
    public string Organization { get; private set; }

    public DateTimeOffset? Checkpoint { get; set; }

    public int? TotalAmount { get; set; }

    public bool Complete { get; set; }


    public string PartitionKey
    {
        get => Partition.ToString();
        set => Partition = DateOnly.Parse(value);
    }
    public string RowKey
    {
        get => Organization;
        set => Organization = value;
    }
    public DateTimeOffset? Timestamp { get; set; }
    public ETag ETag { get; set; }

    public void InstanceHandled(DateTimeOffset lastChanged)
    {
        Checkpoint = lastChanged < Checkpoint ? lastChanged : Checkpoint;
        TotalAmount = TotalAmount is null ? 1 : TotalAmount + 1;
    }
}