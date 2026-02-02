using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.DialogportenAdapter.Unit.Tests.Common.Builder;

public class AltinnDataTypeBuilder
{
    private readonly DataType _dataType;

    private AltinnDataTypeBuilder(DataType dataType)
    {
        _dataType = dataType;
    }

    public static AltinnDataTypeBuilder From(DataType appLogic)
    {
        return new AltinnDataTypeBuilder(appLogic.DeepClone());
    }

    public static AltinnDataTypeBuilder NewDefaultDataType() => new(
        new DataType
        {
            Id = "png-1",
            Description = new LanguageString
            {
                {
                    "nb", "I gui: ID er ref-data-as-pdf"
                }
            },
            AllowedContentTypes = [],
            AllowedContributors = [],
            AppLogic = null,
            TaskId = null,
            MaxSize = null,
            MaxCount = 0,
            MinCount = 0,
            Grouping = null,
            EnablePdfCreation = false,
            EnableFileScan = false,
            ValidationErrorOnPendingFileScan = false,
            EnabledFileAnalysers = null,
            EnabledFileValidators = null,
            AllowedKeysForUserDefinedMetadata = null
        });

    public AltinnDataTypeBuilder WithId(string id)
    {
        _dataType.Id = id;
        return this;
    }

    public AltinnDataTypeBuilder WithDescription(LanguageString description)
    {
        _dataType.Description = description;
        return this;
    }

    public AltinnDataTypeBuilder WithAllowedContentTypes(List<string> allowedContentTypes)
    {
        _dataType.AllowedContentTypes = allowedContentTypes;
        return this;
    }

    public AltinnDataTypeBuilder WithAllowedContributors(List<string> allowedContributors)
    {
        _dataType.AllowedContributors = allowedContributors;
        return this;
    }

    public AltinnDataTypeBuilder WithAppLogic(ApplicationLogic appLogic)
    {
        _dataType.AppLogic = appLogic;
        return this;
    }

    public AltinnDataTypeBuilder WithTaskId(string taskId)
    {
        _dataType.TaskId = taskId;
        return this;
    }

    public AltinnDataTypeBuilder WithMaxSize(int? maxSize)
    {
        _dataType.MaxSize = maxSize;
        return this;
    }

    public AltinnDataTypeBuilder WithMaxCount(int maxCount)
    {
        _dataType.MaxCount = maxCount;
        return this;
    }

    public AltinnDataTypeBuilder WithMinCount(int minCount)
    {
        _dataType.MinCount = minCount;
        return this;
    }

    public AltinnDataTypeBuilder WithGrouping(string grouping)
    {
        _dataType.Grouping = grouping;
        return this;
    }

    public AltinnDataTypeBuilder WithEnablePdfCreation(bool enablePdfCreation)
    {
        _dataType.EnablePdfCreation = enablePdfCreation;
        return this;
    }

    public AltinnDataTypeBuilder WithEnableFileScan(bool enableFileScan)
    {
        _dataType.EnableFileScan = enableFileScan;
        return this;
    }

    public AltinnDataTypeBuilder WithValidationErrorOnPendingFileScan(bool validationErrorOnPendingFileScan)
    {
        _dataType.ValidationErrorOnPendingFileScan = validationErrorOnPendingFileScan;
        return this;
    }

    public AltinnDataTypeBuilder WithEnabledFileAnalysers(List<string> enabledFileAnalysers)
    {
        _dataType.EnabledFileAnalysers = enabledFileAnalysers;
        return this;
    }

    public AltinnDataTypeBuilder WithEnabledFileValidators(List<string> enabledFileValidators)
    {
        _dataType.EnabledFileValidators = enabledFileValidators;
        return this;
    }

    public AltinnDataTypeBuilder WithAllowedKeysForUserDefinedMetadata(List<string> allowedKeys)
    {
        _dataType.AllowedKeysForUserDefinedMetadata = allowedKeys;
        return this;
    }

    public DataType Build()
    {
        return _dataType.DeepClone();
    }
}