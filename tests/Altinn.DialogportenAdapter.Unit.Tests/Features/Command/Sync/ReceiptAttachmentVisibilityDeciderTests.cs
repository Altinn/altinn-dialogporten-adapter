using Altinn.DialogportenAdapter.WebApi.Features.Command.Sync;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.DialogportenAdapter.Unit.Tests.Features.Command.Sync;

public class ReceiptAttachmentVisibilityDeciderTests
{
    [Fact]
    public void GetConsumerType_DataTypeWithAppLogic_ReturnsApi()
    {
        var application = new Application
        {
            DataTypes =
            [
                new() { Id = "main", AppLogic = new ApplicationLogic() }
            ]
        };

        var decider = ReceiptAttachmentVisibilityDecider.Create(application);
        var result = decider.GetConsumerType(CreateDataElement("main"));

        Assert.Equal(AttachmentUrlConsumerType.Api, result);
    }

    [Fact]
    public void GetConsumerType_DataTypeWithAllowedContributorsAppOwned_ReturnsApi()
    {
        var application = new Application
        {
            DataTypes =
            [
                new() { Id = "attachment", AllowedContributors = ["endUser", "app:owned"] }
            ]
        };

        var decider = ReceiptAttachmentVisibilityDecider.Create(application);
        var result = decider.GetConsumerType(CreateDataElement("attachment"));

        Assert.Equal(AttachmentUrlConsumerType.Api, result);
    }

    [Fact]
    public void GetConsumerType_DataTypeWithLegacyAppOwnedContributers_ReturnsApi()
    {
        var legacyDataType = new DataType { Id = "legacy" };
#pragma warning disable CS0618 // AllowedContributers is kept for backwards compatibility
        legacyDataType.AllowedContributers = ["app:owned"];
#pragma warning restore CS0618

        var application = new Application
        {
            DataTypes = [legacyDataType]
        };

        var decider = ReceiptAttachmentVisibilityDecider.Create(application);
        var result = decider.GetConsumerType(CreateDataElement("legacy"));

        Assert.Equal(AttachmentUrlConsumerType.Api, result);
    }

    [Fact]
    public void GetConsumerType_RefDataAsPdf_ReturnsGui()
    {
        var application = new Application
        {
            DataTypes =
            [
                new() { Id = "ref-data-as-pdf" }
            ]
        };

        var decider = ReceiptAttachmentVisibilityDecider.Create(application);
        var result = decider.GetConsumerType(CreateDataElement("ref-data-as-pdf"));

        Assert.Equal(AttachmentUrlConsumerType.Gui, result);
    }

    [Fact]
    public void GetConsumerType_DataTypeWithHiddenGrouping_ReturnsApi()
    {
        var application = new Application
        {
            DataTypes =
            [
                new() { Id = "formsource", Grouping = "group.formdatasource" }
            ]
        };

        var decider = ReceiptAttachmentVisibilityDecider.Create(application);
        var result = decider.GetConsumerType(CreateDataElement("formsource"));

        Assert.Equal(AttachmentUrlConsumerType.Api, result);
    }

    [Fact]
    public void GetConsumerType_DataTypeNotConfigured_UsesGui()
    {
        var application = new Application { DataTypes = [] };

        var decider = ReceiptAttachmentVisibilityDecider.Create(application);
        var result = decider.GetConsumerType(CreateDataElement("unknown"));

        Assert.Equal(AttachmentUrlConsumerType.Gui, result);
    }

    [Fact]
    public void GetConsumerType_DataElementWithoutDataType_UsesGui()
    {
        var application = new Application { DataTypes = [] };

        var decider = ReceiptAttachmentVisibilityDecider.Create(application);
        var result = decider.GetConsumerType(CreateDataElement(null));

        Assert.Equal(AttachmentUrlConsumerType.Gui, result);
    }

    private static DataElement CreateDataElement(string? dataType) =>
        new()
        {
            Id = Guid.NewGuid().ToString(),
            DataType = dataType
        };
}
