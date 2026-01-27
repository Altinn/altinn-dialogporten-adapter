using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.DialogportenAdapter.Unit.Tests.Common.Builder;

public class AltinnDataTypeBuilder
{
    private string _id;
    private LanguageString _description;
    private List<string> _allowedContentTypes;
    private List<string> _allowedContributers;
    private List<string> _allowedContributors;
    private ApplicationLogic? _appLogic;
    private string? _taskId;
    private int? _maxSize;
    private int _maxCount = 1;
    private int _minCount = 1;
    private string? _grouping;
    private bool _enablePdfCreation = true;
    private bool _enableFileScan;
    private bool _validationErrorOnPendingFileScan;
    private List<string>? _enabledFileAnalysers = [];
    private List<string>? _enabledFileValidators = [];
    private List<string>? _allowedKeysForUserDefinedMetadata;

    private AltinnDataTypeBuilder() {}

    public static AltinnDataTypeBuilder NewDefaultDataType()
    {
        return new AltinnDataTypeBuilder
        {
            _id = "png-1",
            _description = new LanguageString
            {
                {
                    "nb", "I gui: ID er ref-data-as-pdf"
                }
            },
            _allowedContentTypes = [],
            _allowedContributers = [],
            _allowedContributors = [],
            _appLogic = null,
            _taskId = null,
            _maxSize = null,
            _maxCount = 0,
            _minCount = 0,
            _grouping = null,
            _enablePdfCreation = false,
            _enableFileScan = false,
            _validationErrorOnPendingFileScan = false,
            _enabledFileAnalysers = null,
            _enabledFileValidators = null,
            _allowedKeysForUserDefinedMetadata = null
        };
    }

    public AltinnDataTypeBuilder WithId(string id)
    {
        _id = id;
        return this;
    }

    public AltinnDataTypeBuilder WithDescription(LanguageString description)
    {
        _description = description;
        return this;
    }

    public AltinnDataTypeBuilder WithAllowedContentTypes(List<string> allowedContentTypes)
    {
        _allowedContentTypes = allowedContentTypes;
        return this;
    }

    public AltinnDataTypeBuilder WithAllowedContributers(List<string> allowedContributers)
    {
        _allowedContributers = allowedContributers;
        return this;
    }

    public AltinnDataTypeBuilder WithAllowedContributors(List<string> allowedContributors)
    {
        _allowedContributors = allowedContributors;
        return this;
    }

    public AltinnDataTypeBuilder WithAppLogic(ApplicationLogic appLogic)
    {
        _appLogic = appLogic;
        return this;
    }

    public AltinnDataTypeBuilder WithTaskId(string taskId)
    {
        _taskId = taskId;
        return this;
    }

    public AltinnDataTypeBuilder WithMaxSize(int? maxSize)
    {
        _maxSize = maxSize;
        return this;
    }

    public AltinnDataTypeBuilder WithMaxCount(int maxCount)
    {
        _maxCount = maxCount;
        return this;
    }

    public AltinnDataTypeBuilder WithMinCount(int minCount)
    {
        _minCount = minCount;
        return this;
    }

    public AltinnDataTypeBuilder WithGrouping(string grouping)
    {
        _grouping = grouping;
        return this;
    }

    public AltinnDataTypeBuilder WithEnablePdfCreation(bool enablePdfCreation)
    {
        _enablePdfCreation = enablePdfCreation;
        return this;
    }

    public AltinnDataTypeBuilder WithEnableFileScan(bool enableFileScan)
    {
        _enableFileScan = enableFileScan;
        return this;
    }

    public AltinnDataTypeBuilder WithValidationErrorOnPendingFileScan(bool validationErrorOnPendingFileScan)
    {
        _validationErrorOnPendingFileScan = validationErrorOnPendingFileScan;
        return this;
    }

    public AltinnDataTypeBuilder WithEnabledFileAnalysers(List<string> enabledFileAnalysers)
    {
        _enabledFileAnalysers = enabledFileAnalysers;
        return this;
    }

    public AltinnDataTypeBuilder WithEnabledFileValidators(List<string> enabledFileValidators)
    {
        _enabledFileValidators = enabledFileValidators;
        return this;
    }

    public AltinnDataTypeBuilder WithAllowedKeysForUserDefinedMetadata(List<string> allowedKeys)
    {
        _allowedKeysForUserDefinedMetadata = allowedKeys;
        return this;
    }

    public DataType Build()
    {
        return new DataType
        {
            Id = _id,
            Description = _description,
            AllowedContentTypes = _allowedContentTypes,
            AllowedContributers = _allowedContributers,
            AllowedContributors = _allowedContributors,
            AppLogic = _appLogic,
            TaskId = _taskId,
            MaxSize = _maxSize,
            MaxCount = _maxCount,
            MinCount = _minCount,
            Grouping = _grouping,
            EnablePdfCreation = _enablePdfCreation,
            EnableFileScan = _enableFileScan,
            ValidationErrorOnPendingFileScan = _validationErrorOnPendingFileScan,
            EnabledFileAnalysers = _enabledFileAnalysers,
            EnabledFileValidators = _enabledFileValidators,
            AllowedKeysForUserDefinedMetadata = _allowedKeysForUserDefinedMetadata
        };
    }
}