using Xunit;

namespace Altinn.DialogportenAdapter.Integration.Tests.Common;

[CollectionDefinition(nameof(AdapterCollectionFixture))]
public class AdapterCollectionFixture : ICollectionFixture<DialogportenAdapterApplication>
{
}
