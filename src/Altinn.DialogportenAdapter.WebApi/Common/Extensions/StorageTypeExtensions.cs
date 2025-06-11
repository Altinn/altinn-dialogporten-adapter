using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.DialogportenAdapter.WebApi.Common.Extensions;

internal static class StorageTypeExtensions
{
    public static bool ShouldBeHidden(this Application application, Instance instance)
    {
        var hideSettings = application.MessageBoxConfig?.HideSettings;
        if (hideSettings == null)
        {
            return false;
        }

        if (hideSettings.HideAlways)
        {
            return true;
        }

        var processId = instance.Process?.CurrentTask?.ElementId;
        return hideSettings.HideOnTask is not null
               && processId is not null
               && hideSettings.HideOnTask.Contains(processId);
    }

    public static SyncAdapterSettings GetSyncAdapterSettings(this Application? application)
    {
        return application?
           .MessageBoxConfig?
           .SyncAdapterSettings
            ?? new SyncAdapterSettings();
    }
}