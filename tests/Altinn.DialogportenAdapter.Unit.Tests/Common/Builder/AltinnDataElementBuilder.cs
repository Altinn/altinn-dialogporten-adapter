using Altinn.Platform.Storage.Interface.Enums;
using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.DialogportenAdapter.Unit.Tests.Common.Builder;

public class AltinnDataElementBuilder(DataElement dataElement)
{
    public static AltinnDataElementBuilder NewDefaultDataElementBuilder()
    {
        return new AltinnDataElementBuilder
        (
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
    }

    public AltinnDataElementBuilder WithCreated(DateTime created)
    {
        dataElement.Created = created;
        return this;
    }

    public AltinnDataElementBuilder WithCreatedBy(string createdBy)
    {
        dataElement.CreatedBy = createdBy;
        return this;
    }

    public AltinnDataElementBuilder WithLastChanged(DateTime lastChanged)
    {
        dataElement.LastChanged = lastChanged;
        return this;
    }

    public AltinnDataElementBuilder WithLastChangedBy(string lastChangedBy)
    {
        dataElement.LastChangedBy = lastChangedBy;
        return this;
    }

    public AltinnDataElementBuilder WithId(string id)
    {
        dataElement.Id = id;
        return this;
    }

    public AltinnDataElementBuilder WithInstanceGuid(string instanceGuid)
    {
        dataElement.InstanceGuid = instanceGuid;
        return this;
    }

    public AltinnDataElementBuilder WithDataType(string dataType)
    {
        dataElement.DataType = dataType;
        return this;
    }

    public AltinnDataElementBuilder WithFilename(string filename)
    {
        dataElement.Filename = filename;
        return this;
    }

    public AltinnDataElementBuilder WithContentType(string contentType)
    {
        dataElement.ContentType = contentType;
        return this;
    }

    public AltinnDataElementBuilder WithBlobStoragePath(string blobStoragePath)
    {
        dataElement.BlobStoragePath = blobStoragePath;
        return this;
    }

    public AltinnDataElementBuilder WithSelfLinks(ResourceLinks selfLinks)
    {
        dataElement.SelfLinks = selfLinks;
        return this;
    }

    public AltinnDataElementBuilder WithSize(long size)
    {
        dataElement.Size = size;
        return this;
    }

    public AltinnDataElementBuilder WithContentHash(string contentHash)
    {
        dataElement.ContentHash = contentHash;
        return this;
    }

    public AltinnDataElementBuilder WithLocked(bool locked)
    {
        dataElement.Locked = locked;
        return this;
    }

    public AltinnDataElementBuilder WithRefs(List<Guid> refs)
    {
        dataElement.Refs = refs;
        return this;
    }

    public AltinnDataElementBuilder WithIsRead(bool isRead)
    {
        dataElement.IsRead = isRead;
        return this;
    }

    public AltinnDataElementBuilder WithTags(List<string> tags)
    {
        dataElement.Tags = tags;
        return this;
    }

    public AltinnDataElementBuilder WithUserDefinedMetadata(List<KeyValueEntry> userDefinedMetadata)
    {
        dataElement.UserDefinedMetadata = userDefinedMetadata;
        return this;
    }

    public AltinnDataElementBuilder WithMetadata(List<KeyValueEntry> metadata)
    {
        dataElement.Metadata = metadata;
        return this;
    }

    public AltinnDataElementBuilder WithDeleteStatus(DeleteStatus deleteStatus)
    {
        dataElement.DeleteStatus = deleteStatus;
        return this;
    }

    public AltinnDataElementBuilder WithFileScanResult(FileScanResult fileScanResult)
    {
        dataElement.FileScanResult = fileScanResult;
        return this;
    }

    public AltinnDataElementBuilder WithReferences(List<Reference> references)
    {
        dataElement.References = references;
        return this;
    }

    public DataElement Build()
    {
        return new DataElement
        {
            Created = dataElement.Created,
            CreatedBy = dataElement.CreatedBy,
            LastChanged = dataElement.LastChanged,
            LastChangedBy = dataElement.LastChangedBy,
            Id = dataElement.Id,
            InstanceGuid = dataElement.InstanceGuid,
            DataType = dataElement.DataType,
            Filename = dataElement.Filename,
            ContentType = dataElement.ContentType,
            BlobStoragePath = dataElement.BlobStoragePath,
            SelfLinks = dataElement.SelfLinks != null
                ? new ResourceLinks
                {
                    Apps = dataElement.SelfLinks.Apps,
                    Platform = dataElement.SelfLinks.Platform,
                }
                : null,
            Size = dataElement.Size,
            ContentHash = dataElement.ContentHash,
            Locked = dataElement.Locked,
            Refs = dataElement.Refs?.ToList(),
            IsRead = dataElement.IsRead,
            Tags = dataElement.Tags?.Select(r => r).ToList(),
            UserDefinedMetadata = dataElement.UserDefinedMetadata?.Select(md => new KeyValueEntry
            {
                Key = md.Key,
                Value = md.Value
            }).ToList(),
            Metadata = dataElement.Metadata?.Select(md => new KeyValueEntry
            {
                Key = md.Key,
                Value = md.Value
            }).ToList(),
            DeleteStatus = dataElement.DeleteStatus != null
                ? new DeleteStatus
                {
                    IsHardDeleted = dataElement.DeleteStatus.IsHardDeleted,
                    HardDeleted = dataElement.DeleteStatus.HardDeleted,
                }
                : null,
            FileScanResult = dataElement.FileScanResult,
            References = dataElement.References?.Select(r => new Reference
            {
                Value = r.Value,
                Relation = r.Relation,
                ValueType = r.ValueType
            }).ToList()
        };
    }
}