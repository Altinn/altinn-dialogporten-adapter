using Altinn.DialogportenAdapter.EventSimulator.Common.Channels;
using Altinn.DialogportenAdapter.EventSimulator.Infrastructure;
using Altinn.DialogportenAdapter.EventSimulator.Infrastructure.Storage;

namespace Altinn.DialogportenAdapter.EventSimulator.Features.Migration;

internal sealed class MigrationPartitionService
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly MigrationPartitionRepository _migrationPartitionRepository;
    private readonly IChannelPublisher<MigrationPartitionCommand> _channelPublisher;

    public MigrationPartitionService(
        IChannelPublisher<MigrationPartitionCommand> channelPublisher,
        IOrganizationRepository organizationRepository,
        MigrationPartitionRepository migrationPartitionRepository)
    {
        _channelPublisher = channelPublisher ?? throw new ArgumentNullException(nameof(channelPublisher));
        _organizationRepository = organizationRepository ?? throw new ArgumentNullException(nameof(organizationRepository));
        _migrationPartitionRepository = migrationPartitionRepository ?? throw new ArgumentNullException(nameof(migrationPartitionRepository));
    }

    public async Task Handle(MigrationCommand command, CancellationToken cancellationToken)
    {
        var organizations = await GetOrganizations(command, cancellationToken);

        var partitionEntities = Enumerable
            .Range(0, command.To.DayNumber - command.From.DayNumber + 1)
            .Select(offset => command.From.AddDays(offset))
            .SelectMany(_ => organizations, (day, org) => new MigrationPartitionEntity(day, org))
            .ToList();

        if (ShouldSkipExistingPartitions(command))
        {
            partitionEntities = partitionEntities
                .Except(await _migrationPartitionRepository.GetExistingPartitions(partitionEntities, cancellationToken))
                .ToList();
        }

        if (!command.IsTest)
        {
            await _migrationPartitionRepository.Upsert(partitionEntities, cancellationToken);
        }

        await Task.WhenAll(partitionEntities
            .Select(x => new MigrationPartitionCommand(x.Partition, x.Organization, command.Parties))
            .Select(x => _channelPublisher
                .Publish(x, cancellationToken)
                .AsTask()));
    }

    private static bool ShouldSkipExistingPartitions(MigrationCommand command) =>
        !command.Force && !command.IsTest;

    private async Task<IEnumerable<string>> GetOrganizations(MigrationCommand command, CancellationToken cancellationToken)
    {
        var validOrganizations = await _organizationRepository.GetOrganizations(cancellationToken);
        if (!(command.Organizations?.Count > 0))
        {
            return validOrganizations;
        }

        var invalid = command.Organizations
            .Except(validOrganizations)
            .ToList();

        if (invalid.Count > 0)
        {
            throw new InvalidOperationException(
                $"Invalid organizations: {string.Join(", ", invalid)}. Valid " +
                $"organizations are: {string.Join(", ", validOrganizations)}");
        }
        return command.Organizations;

    }
}

internal sealed record MigrationCommand(
    DateOnly From,
    DateOnly To,
    List<string>? Organizations,
    List<string>? Parties,
    bool Force = false)
{
    internal bool IsTest => Parties?.Count > 0;
}

internal sealed record MigrationPartitionCommand(DateOnly Partition, string Organization, List<string>? Parties)
{
    public bool IsTest => Parties?.Count > 0;
}