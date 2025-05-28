using Altinn.DialogportenAdapter.WebApi.Common;
using System.Text.Json;
using Altinn.DialogportenAdapter.WebApi.Common.Extensions;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Register;
using Altinn.Platform.Storage.Interface.Enums;
using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.DialogportenAdapter.WebApi.Features.Command.Sync;

internal sealed class ActivityDtoTransformer
{
    private readonly IRegisterRepository _registerRepository;

    public ActivityDtoTransformer(IRegisterRepository registerRepository)
    {
        _registerRepository = registerRepository ?? throw new ArgumentNullException(nameof(registerRepository));
    }

    public async Task<List<ActivityDto>> GetActivities(InstanceEventList events, CancellationToken cancellationToken)
    {
        var activities = new List<ActivityDto>();
        var createdFound = false;
        var actorUrnByUserId = await LookupUsers(events.InstanceEvents, cancellationToken);

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
                InstanceEventType.Deleted when @event.DataId is null => DialogActivityType.DialogDeleted,
                InstanceEventType.Undeleted when @event.DataId is null => DialogActivityType.DialogRestored,
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
                PerformedBy = GetPerformedBy(@event.User, actorUrnByUserId),
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
                var current = GetPerformedBy(@event.User, actorUrnByUserId);
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

    private async Task<Dictionary<int, string>> LookupUsers(List<InstanceEvent> events, CancellationToken cancellationToken)
    {
        var actorUrnByUserUrn = await _registerRepository.GetActorUrnByUserId(
            events.Where(x => x.User.UserId.HasValue)
                .Select(x => x.User.UserId!.Value.ToString())
                .Distinct(),
            cancellationToken
        );

        return actorUrnByUserUrn.ToDictionary(x => int.Parse(x.Key), x => x.Value);
    }

    private static ActorDto GetPerformedBy(PlatformUser user, Dictionary<int, string> actorUrnByUserId)
    {
        if (user.UserId.HasValue && actorUrnByUserId.TryGetValue(user.UserId.Value, out var actorUrn))
        {
            return actorUrn.StartsWith(Constants.DisplayNameUrnPrefix)
                ? new ActorDto { ActorType = ActorType.PartyRepresentative, ActorName = actorUrn[Constants.DisplayNameUrnPrefix.Length..] }
                : new ActorDto { ActorType = ActorType.PartyRepresentative, ActorId = actorUrn };
        }

        if (!string.IsNullOrWhiteSpace(user.SystemUserOwnerOrgNo))
        {
            return new ActorDto { ActorType = ActorType.PartyRepresentative, ActorId = $"{Constants.OrganizationUrnPrefix}{user.SystemUserOwnerOrgNo}" };
        }

        if (!string.IsNullOrWhiteSpace(user.OrgId))
        {
            return new ActorDto { ActorType = ActorType.ServiceOwner };
        }

        throw new InvalidOperationException($"{nameof(PlatformUser)} could not be converted to {nameof(ActorDto)}: {JsonSerializer.Serialize(user)}.");
    }
}