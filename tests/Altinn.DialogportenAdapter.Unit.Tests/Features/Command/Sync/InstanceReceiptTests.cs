using System.Net;
using Altinn.DialogportenAdapter.Test.Common.Builder;
using Altinn.DialogportenAdapter.WebApi.Features.Command.Sync;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Register;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Storage;
using Altinn.Platform.Storage.Interface.Models;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Refit;
using IAltinnOrgs = Altinn.DialogportenAdapter.WebApi.Infrastructure.Register.IAltinnOrgs;
using IApplicationRepository = Altinn.DialogportenAdapter.WebApi.Infrastructure.Storage.IApplicationRepository;
using IRegisterApi = Altinn.DialogportenAdapter.WebApi.Infrastructure.Register.IRegisterApi;
using IStorageApi = Altinn.DialogportenAdapter.WebApi.Infrastructure.Storage.IStorageApi;

namespace Altinn.DialogportenAdapter.Unit.Tests.Features.Command.Sync;

public class InstanceReceiptTests
{
    private static readonly RefitSettings RefitSettings = new();

    [Fact]
    public async Task GetReceipt_InvalidLanguageCode_ReturnsInvalidLanguageCode()
    {
        var (sut, _, _, _, _, _) = CreateSut();

        var result = await sut.GetReceipt(
            new GetReceiptDto(Guid.NewGuid(), Guid.NewGuid(), "de"),
            CancellationToken.None);

        Assert.IsType<GetReceiptResponse.InvalidLanguageCode>(result);
    }

    [Fact]
    public async Task GetReceipt_DialogNotFound_ReturnsNotFound()
    {
        var dialogApi = Substitute.For<IDialogportenApi>();
        dialogApi.Get(Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IApiResponse<DialogDto>>(ApiNotFound<DialogDto>()));

        var (sut, _, _, _, _, _) = CreateSut(dialogApi: dialogApi);

        var result = await sut.GetReceipt(
            new GetReceiptDto(Guid.NewGuid(), Guid.NewGuid(), "nb"),
            CancellationToken.None);

        Assert.IsType<GetReceiptResponse.NotFound>(result);
    }

    [Fact]
    public async Task GetReceipt_InvalidStorageUrn_ReturnsNotFound()
    {
        var data = CreateHappyPathData();
        data.Dialog.ServiceOwnerContext = new ServiceOwnerContext
        {
            ServiceOwnerLabels = [new ServiceOwnerLabel { Value = "not-a-storage-urn" }]
        };

        var (sut, _, _, _, _, _) = CreateSutFromData(data);

        var result = await sut.GetReceipt(
            new GetReceiptDto(data.DialogId, data.TransmissionId, "nb"),
            CancellationToken.None);

        Assert.IsType<GetReceiptResponse.NotFound>(result);
    }

    [Fact]
    public async Task GetReceipt_InstanceNotFound_ReturnsNotFound()
    {
        var data = CreateHappyPathData();
        var (sut, storageApi, _, _, _, _) = CreateSutFromData(data);
        storageApi.GetInstance(data.PartyId, data.InstanceGuid, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IApiResponse<Instance>>(
                ApiNotFound<Instance>()));

        var result = await sut.GetReceipt(
            new GetReceiptDto(data.DialogId, data.TransmissionId, "nb"),
            CancellationToken.None);

        Assert.IsType<GetReceiptResponse.NotFound>(result);
    }

    [Fact]
    public async Task GetReceipt_ApplicationNotFound_ReturnsNotFound()
    {
        var data = CreateHappyPathData();
        var (sut, _, applicationRepository, _, _, _) = CreateSutFromData(data);
        applicationRepository.GetApplication(data.Instance.AppId, Arg.Any<CancellationToken>())
            .Returns((Application?)null);

        var result = await sut.GetReceipt(
            new GetReceiptDto(data.DialogId, data.TransmissionId, "nb"),
            CancellationToken.None);

        Assert.IsType<GetReceiptResponse.NotFound>(result);
    }

    [Fact]
    public async Task GetReceipt_TransmissionNotFound_ReturnsNotFound()
    {
        var data = CreateHappyPathData();
        var missingTransmissionId = Guid.NewGuid();
        var (sut, _, _, _, _, _) = CreateSutFromData(data);

        var result = await sut.GetReceipt(
            new GetReceiptDto(data.DialogId, missingTransmissionId, "nb"),
            CancellationToken.None);

        Assert.IsType<GetReceiptResponse.NotFound>(result);
    }

    [Fact]
    public async Task GetReceipt_AltinnOrgsNotFound_ReturnsMarkDownWithOrg()
    {
        var data = CreateHappyPathData();
        var (sut, _, _, _, altinnOrgs, _) = CreateSutFromData(data);
        altinnOrgs.GetAltinnOrgs(Arg.Any<CancellationToken>())
            .Returns((AltinnOrgData?)null);

        var result = await sut.GetReceipt(
            new GetReceiptDto(data.DialogId, data.TransmissionId, "nb"),
            CancellationToken.None);

        var success = Assert.IsType<GetReceiptResponse.Success>(result);
        Assert.Contains("Dato sendt", success.Markdown);
        Assert.Contains("123456*****-Ola Nordmann", success.Markdown);
        Assert.Contains("digdir", success.Markdown);
        Assert.Contains("Referansenummer", success.Markdown);
        Assert.Contains("123456789abc", success.Markdown);
    }

    [Fact]
    public async Task GetReceipt_HappyPath_ReturnsMarkdownNb()
    {
        var data = CreateHappyPathData();
        var (sut, _, _, _, _, _) = CreateSutFromData(data);

        var result = await sut.GetReceipt(
            new GetReceiptDto(data.DialogId, data.TransmissionId, "nb"),
            CancellationToken.None);

        var success = Assert.IsType<GetReceiptResponse.Success>(result);
        Assert.Contains("Dato sendt", success.Markdown);
        Assert.Contains("Avsender:", success.Markdown);
        Assert.Contains("123456*****-Ola Nordmann", success.Markdown);
        Assert.Contains("Mottaker", success.Markdown);
        Assert.Contains("Digitaliseringsdirektoratet", success.Markdown);
        Assert.Contains("Referansenummer", success.Markdown);
        Assert.Contains("123456789abc", success.Markdown);
        Assert.Contains("Det er gjennomført en maskinell kontroll under utfylling", success.Markdown);
    }

    [Fact]
    public async Task GetReceipt_HappyPath_ReturnsMarkdownNn()
    {
        var data = CreateHappyPathData();
        var (sut, _, _, _, _, _) = CreateSutFromData(data);

        var result = await sut.GetReceipt(
            new GetReceiptDto(data.DialogId, data.TransmissionId, "nn"),
            CancellationToken.None);

        var success = Assert.IsType<GetReceiptResponse.Success>(result);
        Assert.Contains("Dato sendt", success.Markdown);
        Assert.Contains("Avsendar:", success.Markdown);
        Assert.Contains("123456*****-Ola Nordmann", success.Markdown);
        Assert.Contains("Mottakar", success.Markdown);
        Assert.Contains("Digitaliseringsdirektoratet", success.Markdown);
        Assert.Contains("Referansenummer", success.Markdown);
        Assert.Contains("123456789abc", success.Markdown);
        Assert.Contains("Det er gjennomført ein maskinell kontroll under utfylling", success.Markdown);
    }

    [Fact]
    public async Task GetReceipt_HappyPath_ReturnsMarkdownEn()
    {
        var data = CreateHappyPathData();
        var (sut, _, _, _, _, _) = CreateSutFromData(data);

        var result = await sut.GetReceipt(
            new GetReceiptDto(data.DialogId, data.TransmissionId, "en"),
            CancellationToken.None);

        var success = Assert.IsType<GetReceiptResponse.Success>(result);
        Assert.Contains("Date sent", success.Markdown);
        Assert.Contains("Sender:", success.Markdown);
        Assert.Contains("123456*****-Ola Nordmann", success.Markdown);
        Assert.Contains("Receiver", success.Markdown);
        Assert.Contains("Norwegian Digitalisation Agency", success.Markdown);
        Assert.Contains("Reference number", success.Markdown);
        Assert.Contains("123456789abc", success.Markdown);
        Assert.Contains("A mechanical check has been completed while filling in", success.Markdown);
    }

    private static (
        InstanceReceipt Sut,
        IStorageApi StorageApi,
        IApplicationRepository ApplicationRepository,
        IDialogportenApi DialogportenApi,
        IAltinnOrgs AltinnOrgs,
        IRegisterApi RegisterApi)
        CreateSut(
            IStorageApi? storageApi = null,
            IApplicationRepository? applicationRepository = null,
            IDialogportenApi? dialogApi = null,
            IAltinnOrgs? altinnOrgs = null,
            IRegisterApi? registerApi = null)
    {
        storageApi ??= Substitute.For<IStorageApi>();
        applicationRepository ??= Substitute.For<IApplicationRepository>();
        dialogApi ??= Substitute.For<IDialogportenApi>();
        altinnOrgs ??= Substitute.For<IAltinnOrgs>();
        registerApi ??= Substitute.For<IRegisterApi>();

        ILogger<InstanceReceipt> logger = Substitute.For<ILogger<InstanceReceipt>>();

        return (
            new InstanceReceipt(storageApi, applicationRepository, dialogApi, altinnOrgs, registerApi, logger),
            storageApi,
            applicationRepository,
            dialogApi,
            altinnOrgs,
            registerApi
        );
    }

    private static (
        InstanceReceipt Sut,
        IStorageApi StorageApi,
        IApplicationRepository ApplicationRepository,
        IDialogportenApi DialogportenApi,
        IAltinnOrgs AltinnOrgs,
        IRegisterApi RegisterApi)
        CreateSutFromData(
            HappyPathData data,
            IStorageApi? storageApi = null,
            IApplicationRepository? applicationRepository = null,
            IDialogportenApi? dialogApi = null,
            IAltinnOrgs? altinnOrgs = null,
            IRegisterApi? registerApi = null)
    {
        var created = CreateSut(storageApi, applicationRepository, dialogApi, altinnOrgs, registerApi);

        created.DialogportenApi.Get(data.DialogId, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IApiResponse<DialogDto>>(ApiOk(data.Dialog)));

        created.StorageApi.GetInstance(data.PartyId, data.InstanceGuid, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult<IApiResponse<Instance>>(ApiOk(data.Instance)));

        created.ApplicationRepository.GetApplication(data.Instance.AppId, Arg.Any<CancellationToken>())
            .Returns(data.Application);

        created.ApplicationRepository.GetApplicationTexts(data.Instance.AppId, data.Application.VersionId, Arg.Any<CancellationToken>())
            .Returns(new ApplicationTexts { Translations = [] });

        created.AltinnOrgs.GetAltinnOrgs(Arg.Any<CancellationToken>())
            .Returns(data.Orgs);

        created.RegisterApi.GetPartiesByUrns(Arg.Any<PartyQueryRequest>(), Arg.Any<CancellationToken>())
            .Returns(new PartyQueryResponse(
                [
                    new PartyIdentifier(
                        PartyId: 1,
                        PartyType: "Person",
                        DisplayName: "Ola Nordmann",
                        ExternalUrn: null,
                        PersonIdentifier: "12345678910",
                        OrganizationIdentifier: null)
                ]));

        return created;
    }

    private static HappyPathData CreateHappyPathData()
    {
        var dialogId = Guid.Parse("0195a56f-87af-7d13-8df5-7559f4f9d8c3");
        var transmissionId = Guid.Parse("0195a56f-912b-7f7e-9c0d-3514fcfc935b");
        var instanceGuid = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-123456789abc");
        const string partyId = "512345";

        var dialog = new DialogDto
        {
            Id = dialogId,
            Org = "digdir",
            Party = "urn:altinn:person:identifier-no:12345678910",
            ServiceResource = "urn:altinn:resource:test-resource",
            Content = new ContentDto
            {
                Title = new ContentValueDto(),
                Summary = new ContentValueDto()
            },
            ServiceOwnerContext = new ServiceOwnerContext
            {
                ServiceOwnerLabels =
                [
                    new ServiceOwnerLabel
                    {
                        Value = $"urn:altinn:integration:storage:{partyId}/{instanceGuid:D}"
                    }
                ]
            },
            Transmissions =
            [
                new TransmissionDto
                {
                    Id = transmissionId,
                    CreatedAt = new DateTimeOffset(2026, 3, 20, 10, 0, 0, TimeSpan.Zero),
                    Sender = new ActorDto { ActorType = ActorType.PartyRepresentative, ActorName = "Unused" },
                    Content = new TransmissionContentDto
                    {
                        Title = new ContentValueDto(),
                        Summary = new ContentValueDto()
                    }
                }
            ]
        };

        var instance = AltinnInstanceBuilder.NewArchivedAltinnInstance()
            .WithAppId("ttd/app")
            .WithId($"512345/{instanceGuid:D}")
            .Build();

        var application = AltinnApplicationBuilder.NewDefaultAltinnApplication()
            .WithVersionId("1.0")
            .Build();

        var orgs = new AltinnOrgData(new Dictionary<string, Org>
        {
            ["digdir"] = new(
                Name: new Dictionary<string, string>
                {
                    ["nb"] = "Digitaliseringsdirektoratet",
                    ["nn"] = "Digitaliseringsdirektoratet",
                    ["en"] = "Norwegian Digitalisation Agency"
                },
                OrgNr: "991825827",
                Environments: ["tt02", "production"],
                Logo: "logo",
                Emblem: "emblem",
                HomePage: "https://www.digdir.no",
                Contact: null)
        });

        return new HappyPathData(dialogId, transmissionId, partyId, instanceGuid, dialog, instance, application, orgs);
    }

    private static ApiResponse<T> ApiOk<T>(T content) =>
        new ApiResponse<T>(
            response: new HttpResponseMessage(HttpStatusCode.OK),
            content: content,
            settings: RefitSettings,
            error: null);

    private static ApiResponse<T> ApiNotFound<T>() =>
        new ApiResponse<T>(
            response: new HttpResponseMessage(HttpStatusCode.NotFound),
            content: default,
            settings: RefitSettings,
            error: null);

    private sealed record HappyPathData(
        Guid DialogId,
        Guid TransmissionId,
        string PartyId,
        Guid InstanceGuid,
        DialogDto Dialog,
        Instance Instance,
        Application Application,
        AltinnOrgData Orgs);
}
