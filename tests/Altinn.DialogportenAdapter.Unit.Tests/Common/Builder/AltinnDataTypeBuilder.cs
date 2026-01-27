using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.DialogportenAdapter.Unit.Tests.Common.Builder;

public class AltinnDataTypeBuilder
{
    private DataType _dataType;

    public AltinnDataTypeBuilder(DataType dataType)
    {
        _dataType = dataType;
    }

    public static AltinnDataTypeBuilder NewDefaultDataType()
    {
        return new AltinnDataTypeBuilder
        (
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
                AllowedContributers = [],
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
    }

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

    public AltinnDataTypeBuilder WithAllowedContributers(List<string> allowedContributers)
    {
        _dataType.AllowedContributers = allowedContributers;
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
        return new DataType
        {
            Id = _dataType.Id,
            Description = _dataType.Description?.Aggregate(
                new LanguageString(),
                (dict, kvp) =>
                {
                    dict[kvp.Key] = kvp.Value;
                    return dict;
                }
            ),
            AllowedContentTypes = _dataType.AllowedContentTypes?.ToList(),
            AllowedContributers = _dataType.AllowedContributers?.ToList(),
            AllowedContributors = _dataType.AllowedContributors?.ToList(),
            AppLogic = _dataType.AppLogic != null
                ? new ApplicationLogic
                {
                    AutoCreate = _dataType.AppLogic.AutoCreate,
                    ClassRef = _dataType.AppLogic.ClassRef,
                    SchemaRef = _dataType.AppLogic.SchemaRef,
                    AllowAnonymousOnStateless = _dataType.AppLogic.AllowAnonymousOnStateless,
                    AutoDeleteOnProcessEnd = _dataType.AppLogic.AutoDeleteOnProcessEnd,
                    DisallowUserCreate = _dataType.AppLogic.DisallowUserCreate,
                    DisallowUserDelete = _dataType.AppLogic.DisallowUserDelete,
                    ShadowFields = _dataType.AppLogic.ShadowFields != null
                        ? new ShadowFields
                        {
                            Prefix = _dataType.AppLogic.ShadowFields.Prefix,
                            SaveToDataType = _dataType.AppLogic.ShadowFields.SaveToDataType
                        }
                        : null
                }
                : null,
            TaskId = _dataType.TaskId,
            MaxSize = _dataType.MaxSize,
            MaxCount = _dataType.MaxCount,
            MinCount = _dataType.MinCount,
            Grouping = _dataType.Grouping,
            EnablePdfCreation = _dataType.EnablePdfCreation,
            EnableFileScan = _dataType.EnableFileScan,
            ValidationErrorOnPendingFileScan = _dataType.ValidationErrorOnPendingFileScan,
            EnabledFileAnalysers = _dataType.EnabledFileAnalysers?.ToList(),
            EnabledFileValidators = _dataType.EnabledFileValidators?.ToList(),
            AllowedKeysForUserDefinedMetadata = _dataType.AllowedKeysForUserDefinedMetadata?.ToList(),
        };
    }
}