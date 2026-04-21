using WireMock.RequestBuilders;

namespace Altinn.DialogportenAdapter.Integration.Tests.Common.Extensions;

public static class WireMockRequestExtensions
{

    extension(IRequestBuilder requestBuilder)
    {
        public IRequestBuilder DpPostDialog()
        {
            return requestBuilder.UsingPost().WithPath("/api/v1/serviceowner/dialogs");
        }

        public IRequestBuilder DpGetDialog(Guid dialogId)
        {
            return requestBuilder.UsingGet().WithPath($"/api/v1/serviceowner/dialogs/{dialogId}");
        }

        public IRequestBuilder StorageGetApplication(string appId)
        {
            return requestBuilder.UsingGet().WithPath($"/storage/api/v1/applications/{appId}");
        }

        public IRequestBuilder StorageGetApplicationTexts(string appId, string language)
        {
            return requestBuilder.UsingGet().WithPath($"/storage/api/v1/applications/{appId}/texts/{language}");
        }

        public IRequestBuilder StorageGetInstance(int partyId, Guid instanceId)
        {
            return requestBuilder.UsingGet().WithPath($"/storage/api/v1/instances/{partyId}/{instanceId}");
        }

        public IRequestBuilder StorageGetInstanceEvents(int partyId, Guid instanceId)
        {
            return requestBuilder.UsingGet().WithPath($"/storage/api/v1/instances/{partyId}/{instanceId}/events");
        }

        public IRequestBuilder StoragePutInstanceDatavalues(int partyId, Guid instanceId)
        {
            return requestBuilder.UsingPut().WithPath($"/storage/api/v1/instances/{partyId}/{instanceId}/datavalues");
        }

        public IRequestBuilder RegisterPostPartySearch()
        {
            return requestBuilder.UsingPost().WithPath("/register/api/v1/dialogporten/parties/query");
        }
    }
}
