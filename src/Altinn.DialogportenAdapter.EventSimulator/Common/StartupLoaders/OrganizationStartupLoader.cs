using Altinn.DialogportenAdapter.EventSimulator.Infrastructure;

namespace Altinn.DialogportenAdapter.EventSimulator.Common.StartupLoaders;

internal sealed class OrganizationStartupLoader : IStartupLoader
{
    private readonly IOrganizationRepository _organizationRepository;

    public OrganizationStartupLoader(IOrganizationRepository organizationRepository)
    {
        _organizationRepository = organizationRepository ?? throw new ArgumentNullException(nameof(organizationRepository));
    }

    public static DateOnly LocalLoadDate { get; private set; } = DateOnly.MinValue;

    public async Task Load(CancellationToken cancellationToken)
    {
        await _organizationRepository.GetOrganizations(cancellationToken);

        // Set the load date to yesterday, as today's data is not yet
        // available and new organizations might be added today.
        LocalLoadDate = DateOnly.FromDateTime(DateTime.Now.AddDays(-1));
    }
}
