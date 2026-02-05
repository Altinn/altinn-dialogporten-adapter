using Altinn.Platform.Storage.Interface.Enums;
using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.DialogportenAdapter.Unit.Tests.Common.Builder;

public class AltinnDataElementBuilder
{
    private readonly DataElement _dataElement;

    private AltinnDataElementBuilder(DataElement dataElement)
    {
        _dataElement = dataElement;
    }

    public static AltinnDataElementBuilder From(DataElement dataElement)
    {
        return new AltinnDataElementBuilder(dataElement.DeepClone());
    }

    public static AltinnDataElementBuilder NewDefaultDataElementBuilder() => new(
        new DataElement
        {
            Created = new DateTime(2000, 1, 1, 1, 1, 1, DateTimeKind.Utc),
            CreatedBy = "me",
            LastChanged = new DateTime(2000, 2, 1, 1, 1, 1, DateTimeKind.Utc),
            LastChangedBy = "123456789",
            Id = "019bd57e-ce5e-74ed-8130-3a1ac8af3d91",
            InstanceGuid = "019bd57f-7146-73fa-9292-e6401d8ef5e8",
            DataType = null,
            Filename = "filename",
            ContentType = "image/jpg",
            BlobStoragePath = "/images",
            SelfLinks = new ResourceLinks
            {
                Apps = null,
                Platform = "http://platform.localhost"
            },
            Size = 1024,
            ContentHash = "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            Locked = false,
            Refs = [Guid.Parse("019bd582-ef52-7850-a483-94ba72e9bba5")],
            IsRead = false,
            Tags = ["Viktig", "Konfidensiell"],
            UserDefinedMetadata = [new KeyValueEntry { Key = "eier", Value = "Trond" }],
            Metadata = [new KeyValueEntry { Key = "versjon", Value = "2" }],
            DeleteStatus = null,
            FileScanResult = FileScanResult.NotApplicable,
            References =
            [
                new Reference
                {
                    Value = "https://localhost",
                    Relation = RelationType.GeneratedFrom,
                    ValueType = ReferenceType.DataElement
                }
            ],
        });

    public AltinnDataElementBuilder WithCreated(DateTime created)
    {
        _dataElement.Created = created;
        return this;
    }

    public AltinnDataElementBuilder WithCreatedBy(string createdBy)
    {
        _dataElement.CreatedBy = createdBy;
        return this;
    }

    public AltinnDataElementBuilder WithLastChanged(DateTime lastChanged)
    {
        _dataElement.LastChanged = lastChanged;
        return this;
    }

    public AltinnDataElementBuilder WithLastChangedBy(string lastChangedBy)
    {
        _dataElement.LastChangedBy = lastChangedBy;
        return this;
    }

    public AltinnDataElementBuilder WithId(string id)
    {
        _dataElement.Id = id;
        return this;
    }

    public AltinnDataElementBuilder WithInstanceGuid(string instanceGuid)
    {
        _dataElement.InstanceGuid = instanceGuid;
        return this;
    }

    public AltinnDataElementBuilder WithDataType(string dataType)
    {
        _dataElement.DataType = dataType;
        return this;
    }

    public AltinnDataElementBuilder WithFilename(string filename)
    {
        _dataElement.Filename = filename;
        return this;
    }

    public AltinnDataElementBuilder WithContentType(string contentType)
    {
        _dataElement.ContentType = contentType;
        return this;
    }

    public AltinnDataElementBuilder WithBlobStoragePath(string blobStoragePath)
    {
        _dataElement.BlobStoragePath = blobStoragePath;
        return this;
    }

    public AltinnDataElementBuilder WithSelfLinks(ResourceLinks selfLinks)
    {
        _dataElement.SelfLinks = selfLinks;
        return this;
    }

    public AltinnDataElementBuilder WithSize(long size)
    {
        _dataElement.Size = size;
        return this;
    }

    public AltinnDataElementBuilder WithContentHash(string contentHash)
    {
        _dataElement.ContentHash = contentHash;
        return this;
    }

    public AltinnDataElementBuilder WithLocked(bool locked)
    {
        _dataElement.Locked = locked;
        return this;
    }

    public AltinnDataElementBuilder WithRefs(List<Guid> refs)
    {
        _dataElement.Refs = refs;
        return this;
    }

    public AltinnDataElementBuilder WithIsRead(bool isRead)
    {
        _dataElement.IsRead = isRead;
        return this;
    }

    public AltinnDataElementBuilder WithTags(List<string> tags)
    {
        _dataElement.Tags = tags;
        return this;
    }

    public AltinnDataElementBuilder WithUserDefinedMetadata(List<KeyValueEntry> userDefinedMetadata)
    {
        _dataElement.UserDefinedMetadata = userDefinedMetadata;
        return this;
    }

    public AltinnDataElementBuilder WithMetadata(List<KeyValueEntry> metadata)
    {
        _dataElement.Metadata = metadata;
        return this;
    }

    public AltinnDataElementBuilder WithDeleteStatus(DeleteStatus deleteStatus)
    {
        _dataElement.DeleteStatus = deleteStatus;
        return this;
    }

    public AltinnDataElementBuilder WithFileScanResult(FileScanResult fileScanResult)
    {
        _dataElement.FileScanResult = fileScanResult;
        return this;
    }

    public AltinnDataElementBuilder WithReferences(List<Reference> references)
    {
        _dataElement.References = references;
        return this;
    }

    public DataElement Build()
    {
        return _dataElement.DeepClone();
    }
}
