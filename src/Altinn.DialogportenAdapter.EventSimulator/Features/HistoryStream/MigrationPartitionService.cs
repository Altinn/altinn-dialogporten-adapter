using Altinn.DialogportenAdapter.EventSimulator.Common.Channels;
using Altinn.DialogportenAdapter.EventSimulator.Common.StartupLoaders;
using Altinn.DialogportenAdapter.EventSimulator.Infrastructure;
using Altinn.DialogportenAdapter.EventSimulator.Infrastructure.Storage;

namespace Altinn.DialogportenAdapter.EventSimulator.Features.HistoryStream;

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
        if (command.Party is not null && string.IsNullOrWhiteSpace(command.Party))
        {
            throw new ArgumentException("Party cannot be empty when provided.", nameof(command));
        }

        if (OrganizationStartupLoader.LocalLoadDate < command.To)
        {
            throw new InvalidOperationException($"Cannot migrate instances after {OrganizationStartupLoader.LocalLoadDate}.");
        }

        var organizations = await GetOrganizations(command, cancellationToken);

        var partitionEntities = Enumerable
            .Range(0, command.To.DayNumber - command.From.DayNumber + 1)
            .Select(offset => command.To.AddDays(-offset))
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
            .Select(x => new MigrationPartitionCommand(x.Partition, x.Organization, command.Party))
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
    string? Party,
    bool Force = false)
{
    public bool IsTest => Party is not null;
}