using Altinn.DialogportenAdapter.WebApi.Common.Extensions;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
using Altinn.Platform.Storage.Interface.Enums;
using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.DialogportenAdapter.WebApi.Features.Command.Sync;

internal sealed class ActivityDtoTransformer
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
                // When DataId is null the event refers to the instance itself
                InstanceEventType.Created when @event.DataId is null && !createdFound => DialogActivityType.DialogCreated,
                InstanceEventType.Submited => DialogActivityType.FormSubmitted,
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
        
        // TODO: Chunk within a time? What if the same user saves multiple times in a row over a long period? For example a user saves a form every day for a week.
        var savedEvents = events.InstanceEvents
            .OrderBy(x => x.Created)
            .Where(x => StringComparer.OrdinalIgnoreCase.Equals(x.EventType, "Saved"))
            .Aggregate((SavedActivities: new List<ActivityDto>(), Previous: (ActorDto?)null), (state, @event) =>
            {
                var current = GetPerformedBy(@event.User, nationalIdentityNumberByUserId);
                if (current.ActorId is null || current.ActorId == state.Previous?.ActorId)
                {
                    return state;
                }
                
                state.Previous = current;
                state.SavedActivities.Add(new ActivityDto
                {
                    Id = @event.Id.Value.ToVersion7(@event.Created.Value),
                    Type = DialogActivityType.FormSaved, 
                    CreatedAt = @event.Created,
                    PerformedBy = current
                });
                return state;
            }, tuple => tuple.SavedActivities);

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