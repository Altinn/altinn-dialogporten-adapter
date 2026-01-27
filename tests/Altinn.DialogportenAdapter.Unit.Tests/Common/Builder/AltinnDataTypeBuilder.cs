using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.DialogportenAdapter.Unit.Tests.Common.Builder;

public class AltinnDataTypeBuilder(DataType dataType)
{
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
        dataType.Id = id;
        return this;
    }

    public AltinnDataTypeBuilder WithDescription(LanguageString description)
    {
        dataType.Description = description;
        return this;
    }

    public AltinnDataTypeBuilder WithAllowedContentTypes(List<string> allowedContentTypes)
    {
        dataType.AllowedContentTypes = allowedContentTypes;
        return this;
    }

    public AltinnDataTypeBuilder WithAllowedContributors(List<string> allowedContributors)
    {
        dataType.AllowedContributors = allowedContributors;
        return this;
    }

    public AltinnDataTypeBuilder WithAppLogic(ApplicationLogic appLogic)
    {
        dataType.AppLogic = appLogic;
        return this;
    }

    public AltinnDataTypeBuilder WithTaskId(string taskId)
    {
        dataType.TaskId = taskId;
        return this;
    }

    public AltinnDataTypeBuilder WithMaxSize(int? maxSize)
    {
        dataType.MaxSize = maxSize;
        return this;
    }

    public AltinnDataTypeBuilder WithMaxCount(int maxCount)
    {
        dataType.MaxCount = maxCount;
        return this;
    }

    public AltinnDataTypeBuilder WithMinCount(int minCount)
    {
        dataType.MinCount = minCount;
        return this;
    }

    public AltinnDataTypeBuilder WithGrouping(string grouping)
    {
        dataType.Grouping = grouping;
        return this;
    }

    public AltinnDataTypeBuilder WithEnablePdfCreation(bool enablePdfCreation)
    {
        dataType.EnablePdfCreation = enablePdfCreation;
        return this;
    }

    public AltinnDataTypeBuilder WithEnableFileScan(bool enableFileScan)
    {
        dataType.EnableFileScan = enableFileScan;
        return this;
    }

    public AltinnDataTypeBuilder WithValidationErrorOnPendingFileScan(bool validationErrorOnPendingFileScan)
    {
        dataType.ValidationErrorOnPendingFileScan = validationErrorOnPendingFileScan;
        return this;
    }

    public AltinnDataTypeBuilder WithEnabledFileAnalysers(List<string> enabledFileAnalysers)
    {
        dataType.EnabledFileAnalysers = enabledFileAnalysers;
        return this;
    }

    public AltinnDataTypeBuilder WithEnabledFileValidators(List<string> enabledFileValidators)
    {
        dataType.EnabledFileValidators = enabledFileValidators;
        return this;
    }

    public AltinnDataTypeBuilder WithAllowedKeysForUserDefinedMetadata(List<string> allowedKeys)
    {
        dataType.AllowedKeysForUserDefinedMetadata = allowedKeys;
        return this;
    }

    public DataType Build()
    {
        return new DataType
        {
            Id = dataType.Id,
            Description = dataType.Description?.Aggregate(
                new LanguageString(),
                (dict, kvp) =>
                {
                    dict[kvp.Key] = kvp.Value;
                    return dict;
                }
            ),
            AllowedContentTypes = dataType.AllowedContentTypes?.ToList(),
            AllowedContributors = dataType.AllowedContributors?.ToList(),
            AppLogic = dataType.AppLogic != null
                ? new ApplicationLogic
                {
                    AutoCreate = dataType.AppLogic.AutoCreate,
                    ClassRef = dataType.AppLogic.ClassRef,
                    SchemaRef = dataType.AppLogic.SchemaRef,
                    AllowAnonymousOnStateless = dataType.AppLogic.AllowAnonymousOnStateless,
                    AutoDeleteOnProcessEnd = dataType.AppLogic.AutoDeleteOnProcessEnd,
                    DisallowUserCreate = dataType.AppLogic.DisallowUserCreate,
                    DisallowUserDelete = dataType.AppLogic.DisallowUserDelete,
                    ShadowFields = dataType.AppLogic.ShadowFields != null
                        ? new ShadowFields
                        {
                            Prefix = dataType.AppLogic.ShadowFields.Prefix,
                            SaveToDataType = dataType.AppLogic.ShadowFields.SaveToDataType
                        }
                        : null
                }
                : null,
            TaskId = dataType.TaskId,
            MaxSize = dataType.MaxSize,
            MaxCount = dataType.MaxCount,
            MinCount = dataType.MinCount,
            Grouping = dataType.Grouping,
            EnablePdfCreation = dataType.EnablePdfCreation,
            EnableFileScan = dataType.EnableFileScan,
            ValidationErrorOnPendingFileScan = dataType.ValidationErrorOnPendingFileScan,
            EnabledFileAnalysers = dataType.EnabledFileAnalysers?.ToList(),
            EnabledFileValidators = dataType.EnabledFileValidators?.ToList(),
            AllowedKeysForUserDefinedMetadata = dataType.AllowedKeysForUserDefinedMetadata?.ToList(),
        };
    }
}