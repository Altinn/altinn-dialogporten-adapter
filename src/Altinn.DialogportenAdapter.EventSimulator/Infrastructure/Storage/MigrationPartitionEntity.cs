using System.Runtime.Serialization;
using Azure;
using Azure.Data.Tables;

namespace Altinn.DialogportenAdapter.EventSimulator.Infrastructure.Storage;

internal sealed class MigrationPartitionEntity(DateOnly partition, string organization) : ITableEntity
{
    [IgnoreDataMember]
    public DateOnly Partition { get; private set; } = partition;

    [IgnoreDataMember]
    public string Organization { get; private set; } = organization ?? throw new ArgumentNullException(nameof(organization));
    // public TimeOnly? CheckpointTime { get; set; }
    // public Guid? CheckpointId { get; set; }
    public uint? TotalAmount { get; set; }


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
}