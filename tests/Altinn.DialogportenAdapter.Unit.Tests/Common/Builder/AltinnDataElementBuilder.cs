using Altinn.Platform.Storage.Interface.Enums;
using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.DialogportenAdapter.Unit.Tests.Common.Builder;

public class AltinnDataElementBuilder
{
    private DateTime _created;
    private string _createdBy;
    private DateTime _lastChanged;
    private string _lastChangedBy;
    private string _id;
    private string _instanceGuid;
    private string _dataType;
    private string _filename;
    private string _contentType;
    private string _blobStoragePath;
    private ResourceLinks _selfLinks;
    private long _size;
    private string _contentHash;
    private bool _locked;
    private List<Guid> _refs;
    private bool _isRead = true;
    private List<string> _tags;
    private List<KeyValueEntry> _userDefinedMetadata;
    private List<KeyValueEntry> _metadata;
    private DeleteStatus _deleteStatus;
    private FileScanResult _fileScanResult;
    private List<Reference> _references;

    private AltinnDataElementBuilder()
    {
    }

    public static AltinnDataElementBuilder NewDefaultDataElementBuilder()
    {
        return new AltinnDataElementBuilder
        {
            _created = new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
            _createdBy = "me",
            _lastChanged = new DateTime(2000, 2, 1, 1, 1, 1, DateTimeKind.Utc),
            _lastChangedBy = "123456789",
            _id = "019bd57e-ce5e-74ed-8130-3a1ac8af3d91",
            _instanceGuid = "019bd57f-7146-73fa-9292-e6401d8ef5e8",
            _dataType = null,
            _filename = "filename",
            _contentType = "image/jpg",
            _blobStoragePath = "/images",
            _selfLinks = new ResourceLinks
            {
                Apps = null,
                Platform = "http://platform.localhost"
            },
            _size = 1024,
            _contentHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            _locked = false,
            _refs = [Guid.Parse("019bd582-ef52-7850-a483-94ba72e9bba5")],
            _isRead = false,
            _tags = ["Viktig", "Konfidensiell"],
            _userDefinedMetadata = [new KeyValueEntry { Key = "eier", Value = "Trond" }],
            _metadata = [new KeyValueEntry { Key = "versjon", Value = "2" }],
            _deleteStatus = null,
            _fileScanResult = FileScanResult.NotApplicable,
            _references =
            [
                new Reference
                {
                    Value = "https://localhost",
                    Relation = RelationType.GeneratedFrom,
                    ValueType = ReferenceType.DataElement
                }
            ],
        };
    }

    public AltinnDataElementBuilder WithCreated(DateTime created)
    {
        _created = created;
        return this;
    }

    public AltinnDataElementBuilder WithCreatedBy(string createdBy)
    {
        _createdBy = createdBy;
        return this;
    }

    public AltinnDataElementBuilder WithLastChanged(DateTime lastChanged)
    {
        _lastChanged = lastChanged;
        return this;
    }

    public AltinnDataElementBuilder WithLastChangedBy(string lastChangedBy)
    {
        _lastChangedBy = lastChangedBy;
        return this;
    }

    public AltinnDataElementBuilder WithId(string id)
    {
        _id = id;
        return this;
    }

    public AltinnDataElementBuilder WithInstanceGuid(string instanceGuid)
    {
        _instanceGuid = instanceGuid;
        return this;
    }

    public AltinnDataElementBuilder WithDataType(string dataType)
    {
        _dataType = dataType;
        return this;
    }

    public AltinnDataElementBuilder WithFilename(string filename)
    {
        _filename = filename;
        return this;
    }

    public AltinnDataElementBuilder WithContentType(string contentType)
    {
        _contentType = contentType;
        return this;
    }

    public AltinnDataElementBuilder WithBlobStoragePath(string blobStoragePath)
    {
        _blobStoragePath = blobStoragePath;
        return this;
    }

    public AltinnDataElementBuilder WithSelfLinks(ResourceLinks selfLinks)
    {
        _selfLinks = selfLinks;
        return this;
    }

    public AltinnDataElementBuilder WithSize(long size)
    {
        _size = size;
        return this;
    }

    public AltinnDataElementBuilder WithContentHash(string contentHash)
    {
        _contentHash = contentHash;
        return this;
    }

    public AltinnDataElementBuilder WithLocked(bool locked)
    {
        _locked = locked;
        return this;
    }

    public AltinnDataElementBuilder WithRefs(List<Guid> refs)
    {
        _refs = refs;
        return this;
    }

    public AltinnDataElementBuilder WithIsRead(bool isRead)
    {
        _isRead = isRead;
        return this;
    }

    public AltinnDataElementBuilder WithTags(List<string> tags)
    {
        _tags = tags;
        return this;
    }

    public AltinnDataElementBuilder WithUserDefinedMetadata(List<KeyValueEntry> userDefinedMetadata)
    {
        _userDefinedMetadata = userDefinedMetadata;
        return this;
    }

    public AltinnDataElementBuilder WithMetadata(List<KeyValueEntry> metadata)
    {
        _metadata = metadata;
        return this;
    }

    public AltinnDataElementBuilder WithDeleteStatus(DeleteStatus deleteStatus)
    {
        _deleteStatus = deleteStatus;
        return this;
    }

    public AltinnDataElementBuilder WithFileScanResult(FileScanResult fileScanResult)
    {
        _fileScanResult = fileScanResult;
        return this;
    }

    public AltinnDataElementBuilder WithReferences(List<Reference> references)
    {
        _references = references;
        return this;
    }

    public DataElement Build()
    {
        return new DataElement
        {
            Created = _created,
            CreatedBy = _createdBy,
            LastChanged = _lastChanged,
            LastChangedBy = _lastChangedBy,
            Id = _id,
            InstanceGuid = _instanceGuid,
            DataType = _dataType,
            Filename = _filename,
            ContentType = _contentType,
            BlobStoragePath = _blobStoragePath,
            SelfLinks = _selfLinks,
            Size = _size,
            ContentHash = _contentHash,
            Locked = _locked,
            Refs = _refs,
            IsRead = _isRead,
            Tags = _tags,
            UserDefinedMetadata = _userDefinedMetadata,
            Metadata = _metadata,
            DeleteStatus = _deleteStatus,
            FileScanResult = _fileScanResult,
        };
    }
}