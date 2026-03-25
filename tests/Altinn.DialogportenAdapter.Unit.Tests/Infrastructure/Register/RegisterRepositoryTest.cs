using Altinn.DialogportenAdapter.WebApi.Infrastructure.Register;
using AwesomeAssertions;
using NSubstitute;
using ZiggyCreatures.Caching.Fusion;
using ZiggyCreatures.Caching.Fusion.NullObjects;

namespace Altinn.DialogportenAdapter.Unit.Tests.Infrastructure.Register;

public class RegisterRepositoryTest
{
    [Fact]
    public async Task GetActorUrnByUserId_Should_Return_Empty_Dictionary_When_Not_Found()
    {
        var registerSub = Substitute.For<IRegisterApi>();
        var registerRepository = new RegisterRepository(registerSub, new NullFusionCache(new FusionCacheOptions()));
        registerSub.GetPartiesByUrns(
                Arg.Is<PartyQueryRequest>(x => x.Data.Single() == "urn:altinn:user:id:50496334"),
                Arg.Any<CancellationToken>()
            )
            .Returns(new PartyQueryResponse([]));

        var userId = "50496334";
        var actorUrn = await registerRepository.GetActorUrnByUserId([userId], CancellationToken.None);

        actorUrn.Should().BeEquivalentTo(new Dictionary<string, string>());
        _ = registerSub.Received(1).GetPartiesByUrns(Arg.Any<PartyQueryRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetActorUrnByUserId_Should_Not_Cache_Null_Entries()
    {
        var registerSub = Substitute.For<IRegisterApi>();
        var registerRepository = new RegisterRepository(registerSub, new FusionCache(new FusionCacheOptions()));
        registerSub.GetPartiesByUrns(
                Arg.Is<PartyQueryRequest>(x => x.Data.Single() == "urn:altinn:user:id:50496334"),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                new PartyQueryResponse([]),
                new PartyQueryResponse([
                    new PartyIdentifier(
                        PartyId: 50496334,
                        PartyType: "person",
                        PersonIdentifier: "57915600745",
                        OrganizationIdentifier: null,
                        ExternalUrn: "urn:altinn:person:identifier-no:57915600745",
                        DisplayName: "KRASS KOMPOSISJON"
                    )
                ]));

        var userId = "50496334";
        var actorUrnFirstTime = await registerRepository.GetActorUrnByUserId([userId], CancellationToken.None);
        var actorUrnSecondTime = await registerRepository.GetActorUrnByUserId([userId], CancellationToken.None);

        actorUrnFirstTime.Should().BeEquivalentTo(new Dictionary<string, string>());
        actorUrnSecondTime.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            { userId, "urn:altinn:person:identifier-no:57915600745" },
        });
        _ = registerSub.Received(2).GetPartiesByUrns(Arg.Any<PartyQueryRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetActorUrnByUserIdShould_Return_A_Non_Empty_Cached_Dictionary_When_Found()
    {
        var registerSub = Substitute.For<IRegisterApi>();
        registerSub.GetPartiesByUrns(
                Arg.Is<PartyQueryRequest>(x => x.Data.Single() == "urn:altinn:user:id:50496334"),
                Arg.Any<CancellationToken>()
            )
            .Returns(new PartyQueryResponse([
                new PartyIdentifier(
                    PartyId: 50496334,
                    PartyType: "person",
                    PersonIdentifier: "57915600745",
                    OrganizationIdentifier: null,
                    ExternalUrn: "urn:altinn:person:identifier-no:57915600745",
                    DisplayName: "KRASS KOMPOSISJON"
                ),
                new PartyIdentifier(
                    PartyId: 50496335,
                    PartyType: "organization",
                    PersonIdentifier: null,
                    OrganizationIdentifier: "12345678",
                    ExternalUrn: "urn:altinn:organization:identifier-no:12345678",
                    DisplayName: "KRASS VIRKSOMHET"
                )
            ]));
        var registerRepository = new RegisterRepository(registerSub, new FusionCache(new FusionCacheOptions()));

        var userId = "50496334";
        var actorUrnFirstTime = await registerRepository.GetActorUrnByUserId([userId], CancellationToken.None);
        var actorUrnSecondTime = await registerRepository.GetActorUrnByUserId([userId], CancellationToken.None);

        actorUrnFirstTime.Should().BeEquivalentTo(new Dictionary<string, string>()
        {
            { "50496334", "urn:altinn:person:identifier-no:57915600745" },
        });
        actorUrnFirstTime.Should().BeEquivalentTo(actorUrnSecondTime);
        _ = registerSub.Received(1).GetPartiesByUrns(Arg.Any<PartyQueryRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetActorUrnByPartyId_Should_Return_Empty_Dictionary_When_Not_Found()
    {
        var registerSub = Substitute.For<IRegisterApi>();
        var registerRepository = new RegisterRepository(registerSub, new NullFusionCache(new FusionCacheOptions()));
        registerSub.GetPartiesByUrns(
                Arg.Is<PartyQueryRequest>(x => x.Data.Single() == "urn:altinn:party:id:50496334"),
                Arg.Any<CancellationToken>()
            )
            .Returns(new PartyQueryResponse([]));

        var partyIds = "50496334";
        var actorUrn = await registerRepository.GetActorUrnByPartyId([partyIds], CancellationToken.None);

        actorUrn.Should().BeEquivalentTo(new Dictionary<string, string>());
    }

    [Fact]
    public async Task GetActorUrnByPartyId_Should_Not_Cache_Null_Entries()
    {
        var registerSub = Substitute.For<IRegisterApi>();
        var registerRepository = new RegisterRepository(registerSub, new FusionCache(new FusionCacheOptions()));
        registerSub.GetPartiesByUrns(
                Arg.Is<PartyQueryRequest>(x => x.Data.Single() == "urn:altinn:party:id:50496334"),
                Arg.Any<CancellationToken>()
            )
            .Returns(
                new PartyQueryResponse([]),
                new PartyQueryResponse([
                    new PartyIdentifier(
                        PartyId: 50496334,
                        PartyType: "person",
                        PersonIdentifier: "57915600745",
                        OrganizationIdentifier: null,
                        ExternalUrn: "urn:altinn:person:identifier-no:57915600745",
                        DisplayName: "KRASS KOMPOSISJON"
                    )
                ]));

        var partyId = "50496334";
        var actorUrnFirstTime = await registerRepository.GetActorUrnByPartyId([partyId], CancellationToken.None);
        var actorUrnSecondTime = await registerRepository.GetActorUrnByPartyId([partyId], CancellationToken.None);

        actorUrnFirstTime.Should().BeEquivalentTo(new Dictionary<string, string>());
        actorUrnSecondTime.Should().BeEquivalentTo(new Dictionary<string, string>
        {
            { partyId, "urn:altinn:person:identifier-no:57915600745" },
        });
        _ = registerSub.Received(2).GetPartiesByUrns(Arg.Any<PartyQueryRequest>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetActorUrnByPartyId_Return_A_Non_Empty_Cached_Dictionary_When_Found()
    {
        var registerSub = Substitute.For<IRegisterApi>();
        registerSub.GetPartiesByUrns(
                Arg.Is<PartyQueryRequest>(x => x.Data.Single() == "urn:altinn:party:id:50496334"),
                Arg.Any<CancellationToken>()
            )
            .Returns(new PartyQueryResponse([
                new PartyIdentifier(
                    PartyId: 50496334,
                    PartyType: "person",
                    PersonIdentifier: "57915600745",
                    OrganizationIdentifier: null,
                    ExternalUrn: "urn:altinn:person:identifier-no:57915600745",
                    DisplayName: "KRASS KOMPOSISJON"
                ),
                new PartyIdentifier(
                    PartyId: 50496335,
                    PartyType: "organization",
                    PersonIdentifier: null,
                    OrganizationIdentifier: "12345678",
                    ExternalUrn: "urn:altinn:organization:identifier-no:12345678",
                    DisplayName: "KRASS VIRKSOMHET"
                )
            ]));
        var registerRepository = new RegisterRepository(registerSub, new FusionCache(new FusionCacheOptions()));

        var partyId = "50496334";
        var actorUrnFirstTime = await registerRepository.GetActorUrnByPartyId([partyId], CancellationToken.None);
        var actorUrnSecondTime = await registerRepository.GetActorUrnByPartyId([partyId], CancellationToken.None);

        actorUrnFirstTime.Should().BeEquivalentTo(new Dictionary<string, string>()
        {
            { "50496334", "urn:altinn:person:identifier-no:57915600745" },
        });
        actorUrnFirstTime.Should().BeEquivalentTo(actorUrnSecondTime);
        _ = registerSub.Received(1).GetPartiesByUrns(Arg.Any<PartyQueryRequest>(), Arg.Any<CancellationToken>());
    }
}
