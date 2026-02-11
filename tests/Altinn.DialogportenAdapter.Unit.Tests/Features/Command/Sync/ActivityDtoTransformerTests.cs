using Altinn.DialogportenAdapter.WebApi.Features.Command.Sync;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Register;
using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.DialogportenAdapter.Unit.Tests.Features.Command.Sync;

public class ActivityDtoTransformerTests
{
    [Fact]
    public async Task GetActivities_SavedEvents_CollapsesPerUser_AndIgnoresServiceOwner()
    {
        var registerRepository = new FakeRegisterRepository(new Dictionary<string, string>
        {
            ["1001"] = "urn:altinn:person:identifier-no:01017012345",
            ["1002"] = "urn:altinn:person:identifier-no:02027012345"
        });

        var sut = new ActivityDtoTransformer(registerRepository);
        var t0 = new DateTime(2026, 2, 9, 12, 0, 0, DateTimeKind.Utc);

        var events = new InstanceEventList
        {
            InstanceEvents =
            [
                new InstanceEvent { Id = Guid.NewGuid(), Created = t0, EventType = "Saved", User = new PlatformUser { UserId = 1001 } },
                new InstanceEvent { Id = Guid.NewGuid(), Created = t0.AddMinutes(1), EventType = "Saved", User = new PlatformUser { OrgId = "ttd" } },
                new InstanceEvent { Id = Guid.NewGuid(), Created = t0.AddMinutes(2), EventType = "Saved", User = new PlatformUser { UserId = 1001 } },
                new InstanceEvent { Id = Guid.NewGuid(), Created = t0.AddMinutes(3), EventType = "Saved", User = new PlatformUser { UserId = 1002 } },
                new InstanceEvent { Id = Guid.NewGuid(), Created = t0.AddMinutes(4), EventType = "Saved", User = new PlatformUser { OrgId = "ttd" } },
                new InstanceEvent { Id = Guid.NewGuid(), Created = t0.AddMinutes(5), EventType = "Saved", User = new PlatformUser { UserId = 1002 } }
            ]
        };

        var activities = await sut.GetActivities(events, new InstanceOwner(), CancellationToken.None);
        var savedActivities = activities.Where(x => x.Type == DialogActivityType.FormSaved).ToList();

        Assert.Equal(2, savedActivities.Count);

        Assert.Equal("urn:altinn:person:identifier-no:01017012345", savedActivities[0].PerformedBy.ActorId);
        Assert.Equal(new DateTimeOffset(t0.AddMinutes(2)), savedActivities[0].CreatedAt);

        Assert.Equal("urn:altinn:person:identifier-no:02027012345", savedActivities[1].PerformedBy.ActorId);
        Assert.Equal(new DateTimeOffset(t0.AddMinutes(5)), savedActivities[1].CreatedAt);
    }

    private sealed class FakeRegisterRepository : IRegisterRepository
    {
        private readonly Dictionary<string, string> _actorUrnByUserId;

        public FakeRegisterRepository(Dictionary<string, string> actorUrnByUserId)
        {
            _actorUrnByUserId = actorUrnByUserId;
        }

        public Task<Dictionary<string, string>> GetActorUrnByUserId(IEnumerable<string> userIds, CancellationToken cancellationToken)
        {
            return Task.FromResult(
                userIds
                    .Where(_actorUrnByUserId.ContainsKey)
                    .ToDictionary(x => x, x => _actorUrnByUserId[x]));
        }

        public Task<Dictionary<string, string>> GetActorUrnByPartyId(IEnumerable<string> partyIds, CancellationToken cancellationToken)
            => Task.FromResult(new Dictionary<string, string>());
    }
}
