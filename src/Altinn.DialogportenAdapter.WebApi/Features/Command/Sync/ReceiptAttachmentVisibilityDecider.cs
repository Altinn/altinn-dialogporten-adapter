using Altinn.Platform.Storage.Interface.Models;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;

namespace Altinn.DialogportenAdapter.WebApi.Features.Command.Sync;

internal sealed class ReceiptAttachmentVisibilityDecider
{
    private const string RefDataAsPdfDataTypeId = "ref-data-as-pdf";
    private const string AppOwnedContributor = "app:owned";
    private static readonly HashSet<string> HiddenGroupingResourceKeys =
        // Sourced from https://raw.githubusercontent.com/Altinn/altinn-receipt/main/src/backend/Altinn.Receipt/appsettings.json
        new(["group.formdatahtml", "group.formdatasource", "group.signaturesource", "group.paymentsource", "group.activities"], StringComparer.Ordinal);

    private readonly Dictionary<string, DataType> _dataTypesById;
    private readonly HashSet<string> _dataTypeIdsExcludedFromGui;

    private ReceiptAttachmentVisibilityDecider(
        Dictionary<string, DataType> dataTypesById,
        HashSet<string> dataTypeIdsExcludedFromGui)
    {
        _dataTypesById = dataTypesById;
        _dataTypeIdsExcludedFromGui = dataTypeIdsExcludedFromGui;
    }

    public static ReceiptAttachmentVisibilityDecider Create(Application application)
    {
        var dataTypes = application?.DataTypes ?? [];

        var dataTypesById = new Dictionary<string, DataType>(StringComparer.OrdinalIgnoreCase);
        foreach (var dataType in dataTypes)
        {
            if (!string.IsNullOrWhiteSpace(dataType.Id))
            {
                dataTypesById[dataType.Id] = dataType;
            }
        }

        var excludedDataTypes = dataTypes
            .Where(ShouldExcludeFromGui)
            .Select(dt => dt.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new ReceiptAttachmentVisibilityDecider(dataTypesById, excludedDataTypes);
    }

    public AttachmentUrlConsumerType GetConsumerType(DataElement dataElement)
    {
        return ShouldBeVisible(dataElement)
            ? AttachmentUrlConsumerType.Gui
            : AttachmentUrlConsumerType.Api;
    }

    private bool ShouldBeVisible(DataElement? dataElement)
    {
        if (dataElement is null)
        {
            return false;
        }

        var dataTypeId = dataElement.DataType;
        if (string.IsNullOrWhiteSpace(dataTypeId))
        {
            // No data type metadata means we fall back to making the attachment visible
            return true;
        }

        if (string.Equals(dataTypeId, RefDataAsPdfDataTypeId, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (_dataTypeIdsExcludedFromGui.Contains(dataTypeId))
        {
            return false;
        }

        if (_dataTypesById.TryGetValue(dataTypeId, out var dataType) &&
            !string.IsNullOrWhiteSpace(dataType.Grouping) &&
            HiddenGroupingResourceKeys.Contains(dataType.Grouping))
        {
            return false;
        }

        return true;
    }

    private static bool ShouldExcludeFromGui(DataType dataType)
    {
        if (string.IsNullOrWhiteSpace(dataType.Id))
        {
            return true;
        }

        if (string.Equals(dataType.Id, RefDataAsPdfDataTypeId, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (dataType.AppLogic is not null)
        {
            return true;
        }

        if (ContainsAppOwned(dataType.AllowedContributors))
        {
            return true;
        }

#pragma warning disable CS0618 // AllowedContributers is kept for backwards compatibility
        if (ContainsAppOwned(dataType.AllowedContributers))
        {
#pragma warning restore CS0618
            return true;
        }

        return false;
    }

    private static bool ContainsAppOwned(IEnumerable<string>? values) =>
        values is not null && values.Any(value => string.Equals(value, AppOwnedContributor, StringComparison.Ordinal));
}
