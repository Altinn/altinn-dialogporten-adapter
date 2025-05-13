using Altinn.DialogportenAdapter.WebApi.Common;
using System.Diagnostics;
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

        var actorByUserId = await LookupUsers(events.InstanceEvents, cancellationToken);

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
                PerformedBy = await GetPerformedBy(@event.User, cancellationToken),
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

                var current = await GetPerformedBy(@event.User, cancellationToken);
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

    private async Task<Dictionary<string, ActorDto>> LookupUsers(List<InstanceEvent> events, CancellationToken cancellationToken)
    {
        var invalidUserIds = events
            .Select(x => x.User.UserId)
            .Where(x => x is null)
            .ToList();

        if (invalidUserIds.Count > 0)
        {
            throw new UnreachableException("Assumption failed: UserId should not be null.");
        }

        var userUrns = await Task.WhenAll(events
            .Select(x => x.User.UserId.ToString())
            .Distinct()
            .Select(async x => (UserId: x!, Actor: await GetPerformedBy(x!, cancellationToken))));
        return userUrns.ToDictionary(x => x.UserId, x => x.Actor);
    }

    private async Task<ActorDto> GetPerformedBy(string? userId, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(userId))
        {
            throw new UnreachableException("Should not get here...");
        }

        var userUrn = await _registerRepository.GetUserUrn(userId, cancellationToken);
        if (userUrn is null)
        {
            // TODO: RegisterSupportsUserUrn: Throw an exception if the user is not found in the register?
            // throw new InvalidOperationException($"User {userId} not found in register.");
            return new ActorDto
            {
                ActorType = ActorType.PartyRepresentative,
                ActorName = "Unknown user"
            };
        }

        // A userId is always a person or a system user (or a business user); it is never a service owner.
        return new ActorDto
        {
            ActorId = userUrn,
            ActorType = ActorType.PartyRepresentative
        };
    }

    private static ActorDto GetPerformedBy(PlatformUser user, Dictionary<int, string> nationalIdentityNumberByUserId)
    {
        if (!string.IsNullOrWhiteSpace(user.OrgId))
        {
            return new ActorDto { ActorType = ActorType.ServiceOwner,  };
        }

        if (user.UserId.HasValue && nationalIdentityNumberByUserId.TryGetValue(user.UserId.Value, out var nationalId))
        {
            return new ActorDto
            {
                ActorType = ActorType.PartyRepresentative,
                ActorId = $"{Constants.PersonUrnPrefix}{nationalId}"
            };
        }

        if (!string.IsNullOrWhiteSpace(user.SystemUserOwnerOrgNo))
        {
            return new ActorDto
            {
                ActorType = ActorType.PartyRepresentative,
                ActorId = $"{Constants.OrganizationUrnPrefix}{user.SystemUserOwnerOrgNo}"
            };
        }

        return new ActorDto
        {
            ActorType = ActorType.PartyRepresentative,
            ActorName = "Unknown user"
        };
    }
}