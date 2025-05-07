using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.DialogportenAdapter.WebApi.Common.Extensions;

internal static class StorageTypeExtensions
{
    public static bool ShouldBeHidden(this Application application, Instance instance)
    {
        var hideSettings = application.MessageBoxConfig?.HideSettings;
        if (hideSettings is null)
        {
            return false;
        }

        var currentTask = instance.Process?.CurrentTask?.ElementId;
        return hideSettings.HideAlways ||
               (hideSettings.HideOnTask?.Any(x => x == currentTask) ?? false);
    }
}