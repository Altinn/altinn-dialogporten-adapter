using System.Text.Json.Nodes;
using Altinn.DialogportenAdapter.WebApi.Common;
using AwesomeAssertions;

namespace Altinn.DialogportenAdapter.Unit.Tests.Common;

public class LogRedactorTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Redact_NullOrWhitespace_ReturnsUnchanged(string? input)
    {
        LogRedactor.Redact(input).Should().Be(input);
    }

    [Fact]
    public void Redact_PersonUrnInParty_MasksIdentifierKeepsPrefix()
    {
        const string json = """{"party":"urn:altinn:person:identifier-no:12018212345"}""";

        var result = LogRedactor.Redact(json);

        result.Should().Contain("urn:altinn:person:identifier-no:[REDACTED]");
        result.Should().NotContain("12018212345");
    }

    [Fact]
    public void Redact_PersonUrnInActorId_MasksIdentifier()
    {
        const string json = """{"sender":{"actorId":"urn:altinn:person:identifier-no:01017012345"}}""";

        LogRedactor.Redact(json).Should().NotContain("01017012345");
    }

    [Theory]
    [InlineData("urn:altinn:organization:identifier-no:912345678")]
    [InlineData("urn:altinn:party:id:51234")]
    public void Redact_NonPersonParty_LeftIntact(string party)
    {
        var json = $$"""{"party":"{{party}}"}""";

        LogRedactor.Redact(json).Should().Contain(party);
    }

    [Fact]
    public void Redact_LegacySelfIdentifiedAndDisplayNameUrns_AreMasked()
    {
        const string json = """{"a":"urn:altinn:person:legacy-selfidentified:Per","b":"urn:altinn:displayName:Ola Nordmann"}""";

        var result = LogRedactor.Redact(json);

        result.Should().Contain("urn:altinn:person:legacy-selfidentified:[REDACTED]");
        result.Should().Contain("urn:altinn:displayName:[REDACTED]");
        result.Should().NotContain("Per");
    }

    [Theory]
    [InlineData("actorName")]
    [InlineData("title")]
    [InlineData("summary")]
    [InlineData("senderName")]
    [InlineData("additionalInfo")]
    public void Redact_FreeTextStringField_FullyRedacted(string property)
    {
        var json = $$"""{"{{property}}":"Ola Nordmann lives at Storgata 1"}""";

        var node = JsonNode.Parse(LogRedactor.Redact(json)!)!;

        node[property]!.GetValue<string>().Should().Be("[REDACTED]");
    }

    [Fact]
    public void Redact_ContentValueObjectFields_ReplacedWholesale()
    {
        // title/summary are ContentValueDto objects, not plain strings.
        const string json = """
            {"content":{"title":{"value":[{"value":"Sensitive name","languageCode":"nb"}],"mediaType":"text/plain"},
            "summary":{"value":[{"value":"Sensitive summary","languageCode":"nb"}]}}}
            """;

        var content = JsonNode.Parse(LogRedactor.Redact(json)!)!["content"]!;

        content["title"]!.GetValue<string>().Should().Be("[REDACTED]");
        content["summary"]!.GetValue<string>().Should().Be("[REDACTED]");
        LogRedactor.Redact(json).Should().NotContain("Sensitive");
    }

    [Fact]
    public void Redact_NestedTransmissionsAndActivities_MaskActorIds()
    {
        const string json = """
            {"transmissions":[{"sender":{"actorId":"urn:altinn:person:identifier-no:02027012345"}}],
            "activities":[{"performedBy":{"actorId":"urn:altinn:person:identifier-no:03037012345"}}]}
            """;

        var result = LogRedactor.Redact(json);

        result.Should().NotContain("02027012345");
        result.Should().NotContain("03037012345");
    }

    [Fact]
    public void Redact_BareIdentifierInUnlistedField_IsMasked()
    {
        const string json = """{"externalReference":"ref-04047012345-x"}""";

        LogRedactor.Redact(json).Should().NotContain("04047012345");
    }

    [Fact]
    public void Redact_NonJsonBodyWithIdentifier_StillMasked()
    {
        const string text = "Error: party urn:altinn:person:identifier-no:05057012345 not found";

        var result = LogRedactor.Redact(text);

        result.Should().NotContain("05057012345");
        result.Should().Contain("urn:altinn:person:identifier-no:[REDACTED]");
    }

    [Fact]
    public void Redact_PiiFreeJson_RemainsEquivalent()
    {
        const string json = """{"org":"ttd","serviceResource":"urn:altinn:resource:app_ttd_test","progress":50}""";

        var result = LogRedactor.Redact(json);

        JsonNode.DeepEquals(JsonNode.Parse(result!), JsonNode.Parse(json)).Should().BeTrue();
    }
}
