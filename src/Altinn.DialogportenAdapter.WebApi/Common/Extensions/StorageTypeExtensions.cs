using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.DialogportenAdapter.WebApi.Common.Extensions;

internal static class StorageTypeExtensions
{
    public static bool ShouldBeHidden(this Application application)
        => application.MessageBoxConfig?.HideSettings?.HideAlways ?? false;

    public static SyncAdapterSettings GetSyncAdapterSettings(this Application? application)
    {
        return application?
           .MessageBoxConfig?
           .SyncAdapterSettings
            ?? new SyncAdapterSettings();
    }
}