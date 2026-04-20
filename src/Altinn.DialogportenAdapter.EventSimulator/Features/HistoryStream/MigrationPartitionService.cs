using System.Globalization;
using Altinn.DialogportenAdapter.EventSimulator.Common.StartupLoaders;
using Altinn.DialogportenAdapter.EventSimulator.Infrastructure.Persistance;
using Altinn.DialogportenAdapter.EventSimulator.Infrastructure.Storage;
using Wolverine;

namespace Altinn.DialogportenAdapter.EventSimulator.Features.HistoryStream;

internal sealed class MigrationPartitionService
{
    private readonly IOrganizationRepository _organizationRepository;
    private readonly IMigrationPartitionRepository _migrationPartitionRepository;
    private readonly IMessageBus _messageBus;

    public MigrationPartitionService(
        IOrganizationRepository organizationRepository,
        IMigrationPartitionRepository migrationPartitionRepository,
        IMessageBus messageBus)
    {
        _organizationRepository = organizationRepository ?? throw new ArgumentNullException(nameof(organizationRepository));
        _migrationPartitionRepository = migrationPartitionRepository ?? throw new ArgumentNullException(nameof(migrationPartitionRepository));
        _messageBus = messageBus ?? throw new ArgumentNullException(nameof(messageBus));
    }

    public async Task Handle(MigrationCommand command, CancellationToken cancellationToken)
    {
        if (command.Instances?.Count > 0)
        {
            await HandleInstances(command, cancellationToken);
            return;
        }

        await HandlePartitions(command, cancellationToken);
    }

    private async Task HandleInstances(MigrationCommand command, CancellationToken cancellationToken)
    {
        ValidateInstanceCommand(command);

        await Task.WhenAll(command.Instances!
            .Select(ParseInstanceCommand)
            .Select(x => _messageBus
                .SendAsync(x)
                .AsTask()));
    }

    private async Task HandlePartitions(MigrationCommand command, CancellationToken cancellationToken)
    {
        if (command.From is null)
        {
            throw new ArgumentException("From is required when migrating partitions.", nameof(command));
        }

        if (command.To is null)
        {
            throw new ArgumentException("To is required when migrating partitions.", nameof(command));
        }

        var from = command.From.Value;
        var to = command.To.Value;

        if (from > to)
        {
            throw new ArgumentException("From cannot be after To.", nameof(command));
        }

        if (command.Party is not null && string.IsNullOrWhiteSpace(command.Party))
        {
            throw new ArgumentException("Party cannot be empty when provided.", nameof(command));
        }

        if (OrganizationStartupLoader.LocalLoadDate < to && !command.Force)
        {
            throw new InvalidOperationException($"Cannot migrate instances after {OrganizationStartupLoader.LocalLoadDate} (use force:true to override)");
        }

        var organizations = await GetOrganizations(command, cancellationToken);

        var partitionEntities = Enumerable
            .Range(0, to.DayNumber - from.DayNumber + 1)
            .Select(offset => to.AddDays(-offset))
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
            .Select(x => new MigratePartitionCommand(x.Partition, x.Organization, command.Party))
            .Select(x => _messageBus
                .SendAsync(x)
                .AsTask()));
    }

    private static void ValidateInstanceCommand(MigrationCommand command)
    {
        if (command.From is not null || command.To is not null)
        {
            throw new ArgumentException("From and To cannot be provided when migrating explicit instances.", nameof(command));
        }

        if (command.Organizations?.Count > 0)
        {
            throw new ArgumentException("Organizations cannot be provided when migrating explicit instances.", nameof(command));
        }

        if (command.Party is not null)
        {
            throw new ArgumentException("Party cannot be provided when migrating explicit instances. Include the party id in each instance id instead.", nameof(command));
        }

        if (command.Force)
        {
            throw new ArgumentException("Force cannot be provided when migrating explicit instances.", nameof(command));
        }
    }

    private static MigrateInstanceCommand ParseInstanceCommand(string instance)
    {
        var instanceId = instance.AsSpan().Trim();
        var partsEnumerator = instanceId.Split("/");
        if (!partsEnumerator.MoveNext() || !int.TryParse(instanceId[partsEnumerator.Current], out var partyId))
        {
            throw new ArgumentException($"Invalid instance id '{instance}'. Expected format is '{{partyId}}/{{instanceGuid}}'.", nameof(instance));
        }

        if (!partsEnumerator.MoveNext() || !Guid.TryParse(instanceId[partsEnumerator.Current], out var instanceGuid))
        {
            throw new ArgumentException($"Invalid instance id '{instance}'. Expected format is '{{partyId}}/{{instanceGuid}}'.", nameof(instance));
        }

        if (partsEnumerator.MoveNext())
        {
            throw new ArgumentException($"Invalid instance id '{instance}'. Expected format is '{{partyId}}/{{instanceGuid}}'.", nameof(instance));
        }

        return new MigrateInstanceCommand(partyId.ToString(CultureInfo.InvariantCulture), instanceGuid);
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
    DateOnly? From,
    DateOnly? To,
    List<string>? Organizations,
    string? Party,
    List<string>? Instances,
    bool Force = false)
{
    public bool IsTest => Party is not null;
}
