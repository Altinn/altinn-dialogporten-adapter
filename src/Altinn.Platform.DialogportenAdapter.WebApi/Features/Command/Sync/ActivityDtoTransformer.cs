using Altinn.Platform.DialogportenAdapter.WebApi.Common;
using Altinn.Platform.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
using Altinn.Platform.Storage.Interface.Enums;
using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.Platform.DialogportenAdapter.WebApi.Features.Command.Sync;

internal class ActivityDtoTransformer
{
    public List<ActivityDto> GetActivities(InstanceEventList events)
    {
        var nationalIdentityNumberByUserId = events.InstanceEvents
            .Where(x => !string.IsNullOrWhiteSpace(x.User.NationalIdentityNumber))
            .GroupBy(x => x.User.UserId)
            .ToDictionary(x => x.Key!.Value, x => x.Select(xx => xx.User.NationalIdentityNumber).First());

        var activities = new List<ActivityDto>();
        var createdFound = false;

        foreach (var @event in events.InstanceEvents.OrderBy(x => x.Created))
        {
            if (!Enum.TryParse<InstanceEventType>(@event.EventType, ignoreCase: true, out var eventType))
            {
                continue;
            }

            var activityType = eventType switch
            {
                InstanceEventType.Created when @event.DataId is null && !createdFound => DialogActivityType.DialogCreated,
                // InstanceEventType.Saved => DialogActivityType.Information, // TODO: Ta eldste - her må mer massering til
                InstanceEventType.Submited => DialogActivityType.Information,
                InstanceEventType.Deleted => DialogActivityType.DialogDeleted,
                InstanceEventType.Undeleted => DialogActivityType.DialogRestored,
                InstanceEventType.Signed => DialogActivityType.SignatureProvided,
                InstanceEventType.MessageArchived => DialogActivityType.DialogClosed,
                InstanceEventType.MessageRead => DialogActivityType.DialogOpened,
                InstanceEventType.SentToSign => DialogActivityType.SentToSigning,
                InstanceEventType.SentToPayment => DialogActivityType.SentToPayment,
                InstanceEventType.SentToSendIn => DialogActivityType.SentToSendIn,
                InstanceEventType.SentToFormFill => DialogActivityType.SentToFormFill,
                _ => (DialogActivityType?)null
            };

            if (!activityType.HasValue)
            {
                continue;
            }
            
            createdFound = createdFound || activityType == DialogActivityType.DialogCreated;
            
            activities.Add(new ActivityDto
            {
                Id = @event.Id.Value.ToVersion7(@event.Created.Value),
                Type = activityType.Value,
                CreatedAt = @event.Created,
                PerformedBy = GetPerformedBy(@event.User, nationalIdentityNumberByUserId),
                Description = activityType == DialogActivityType.Information
                    ? [ new LocalizationDto { LanguageCode = "nb", Value = eventType.ToString() } ]
                    : [ ]
            });
        }
        
        var savedEvents = events.InstanceEvents
            .OrderBy(x => x.Created)
            .Where(x => StringComparer.OrdinalIgnoreCase.Equals(x.EventType, "Saved"))
            .Aggregate((new List<ActivityDto>(), (ActorDto?)null), (tuple, @event) =>
            {
                var current = GetPerformedBy(@event.User, nationalIdentityNumberByUserId);
                if (current.ActorId is null || current.ActorId == tuple.Item2?.ActorId)
                {
                    return tuple;
                }
                
                tuple.Item2 = current;
                tuple.Item1.Add(new ActivityDto
                {
                    Id = @event.Id.Value.ToVersion7(@event.Created.Value),
                    Type = DialogActivityType.Information,
                    CreatedAt = @event.Created,
                    PerformedBy = GetPerformedBy(@event.User, nationalIdentityNumberByUserId),
                    Description = { new LocalizationDto { LanguageCode = "nb", Value = "Lagret"} }
                });
                return tuple;
            }, tuple => tuple.Item1);

        activities.AddRange(savedEvents);
        return activities;
    }

    private static ActorDto GetPerformedBy(PlatformUser user, Dictionary<int, string> nationalIdentityNumberByUserId)
    {
        if (!string.IsNullOrWhiteSpace(user.OrgId))
        {
            return new ActorDto { ActorType = ActorType.ServiceOwner };
        }
        
        // TODO: GetPerformedBy logic needs to be improved.
        // We need to handle the case where the user is not found in the dictionary
        if (!nationalIdentityNumberByUserId.TryGetValue(user.UserId.Value, out var nationalId))
        {
            return new ActorDto
            {
                ActorType = ActorType.PartyRepresentative,
                ActorName = "Unknown user"
            };
        }

        return new ActorDto
        {
            ActorType = ActorType.PartyRepresentative,
            ActorId = ToPersonIdentifier(nationalId)
        };
    }
    
    private static string? ToPersonIdentifier(string? personNumber)
    {
        const string personPrefix = "urn:altinn:person:identifier-no:";
        return string.IsNullOrWhiteSpace(personNumber) ? null : $"{personPrefix}{personNumber}";
    }
}