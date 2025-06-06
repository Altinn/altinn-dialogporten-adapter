using Altinn.DialogportenAdapter.EventSimulator.Infrastructure;

namespace Altinn.DialogportenAdapter.EventSimulator.Common.StartupLoaders;

internal sealed class OrganizationStartupLoader : IStartupLoader
{
    private readonly IOrganizationRepository _organizationRepository;

    public OrganizationStartupLoader(IOrganizationRepository organizationRepository)
    {
        _organizationRepository = organizationRepository ?? throw new ArgumentNullException(nameof(organizationRepository));
    }

    public async Task Load(CancellationToken cancellationToken)
    {
        await _organizationRepository.GetOrganizations(cancellationToken);
    }
}