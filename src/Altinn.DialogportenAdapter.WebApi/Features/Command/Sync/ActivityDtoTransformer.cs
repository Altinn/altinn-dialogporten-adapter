using System.Diagnostics.CodeAnalysis;
using System.Globalization;
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

    public async Task<List<ActivityDto>> GetActivities(InstanceEventList events, InstanceOwner instanceOwner, CancellationToken cancellationToken)
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
                Id = @event.Id!.Value.ToVersion7(@event.Created!.Value),
                Type = activityType.Value,
                CreatedAt = @event.Created,
                PerformedBy = GetPerformedBy(@event.User, instanceOwner, actorUrnByUserId),
                Description = activityType == DialogActivityType.Information // Todo: This never happens. The Information type is never handled
                    ? [ new LocalizationDto { LanguageCode = "nb", Value = eventType.ToString() } ]
                    : [ ]
            });
        }

        var savedEvents = events.InstanceEvents
            .OrderBy(x => x.Created)
            .Where(x => StringComparer.OrdinalIgnoreCase.Equals(x.EventType, "Saved"))
            // We ignore "Saved" events from the service owner, as they are typically related to transformations performed by the app
            // as a consequence of the instance being saved by the end user, and do not represent an explicit action performed by the service owner.
            // These events are usually interleaved, breaking the collapsing of "Saved" events performed by the end user.
            .Where(x => string.IsNullOrWhiteSpace(x.User.OrgId))
            .Aggregate((SavedActivities: new List<ActivityDto>(), PreviousActivity: (ActivityDto?)null), (state, @event) =>
            {
                var currentActor = GetPerformedBy(@event.User, instanceOwner, actorUrnByUserId);
                if (IsPerformedBy(state.PreviousActivity, currentActor))
                {
                    state.PreviousActivity.CreatedAt = @event.Created;
                    return state;
                }

                state.SavedActivities.Add(state.PreviousActivity = new ActivityDto
                {
                    Id = @event.Id!.Value.ToVersion7(@event.Created!.Value),
                    Type = DialogActivityType.FormSaved,
                    CreatedAt = @event.Created,
                    PerformedBy = currentActor
                });
                return state;
            }, state => state.SavedActivities);

        activities.AddRange(savedEvents);
        return activities;
    }

    private static bool IsPerformedBy(
        [NotNullWhen(true)] ActivityDto? activity,
        [NotNullWhen(true)] ActorDto? actor) =>
        activity?.PerformedBy is not null
        && actor is not null
        && (
            // Fall back to comparing on actorName in case actorId is null (legacy users)
            activity.PerformedBy.ActorId is not null || actor.ActorId is not null
                ? activity.PerformedBy.ActorId == actor.ActorId
                : !string.IsNullOrWhiteSpace(activity.PerformedBy.ActorName)
                      && activity.PerformedBy.ActorName == actor.ActorName
        );

    private async Task<Dictionary<int, string>> LookupUsers(List<InstanceEvent> events, CancellationToken cancellationToken)
    {
        var actorUrnByUserUrn = await _registerRepository.GetActorUrnByUserId(
            events.Where(x => x.User?.UserId != null)
                .Select(x => x.User.UserId!.Value.ToString(CultureInfo.InvariantCulture))
                .Distinct(),
            cancellationToken
        );

        return actorUrnByUserUrn.ToDictionary(x => int.Parse(x.Key, CultureInfo.InvariantCulture), x => x.Value);
    }

    private static ActorDto GetPerformedBy(PlatformUser user, InstanceOwner instanceOwner, Dictionary<int, string> actorUrnByUserId)
    {
        if (user.UserId.HasValue && actorUrnByUserId.TryGetValue(user.UserId.Value, out var actorUrn))
        {
            // Legacy system ids and enterprise users does not have a standard urn format in register, so just return the name
            return actorUrn.StartsWith(Constants.DisplayNameUrnPrefix, StringComparison.InvariantCulture)
                ? new ActorDto { ActorType = ActorType.PartyRepresentative, ActorName = actorUrn[Constants.DisplayNameUrnPrefix.Length..] }
                : new ActorDto { ActorType = ActorType.PartyRepresentative, ActorId = actorUrn };
        }

        // Altinn 2 end user system id
        if (user.EndUserSystemId.HasValue)
        {
            return new ActorDto { ActorType = ActorType.PartyRepresentative, ActorName = $"EUS #{user.EndUserSystemId.Value}" };
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