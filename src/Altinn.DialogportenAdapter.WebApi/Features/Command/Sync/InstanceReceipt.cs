using System.Globalization;
using System.Text.RegularExpressions;
using Altinn.ApiClients.Dialogporten;
using Altinn.DialogportenAdapter.WebApi.Common.Extensions;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Register;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Storage;
using Altinn.Platform.Storage.Interface.Models;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using ZiggyCreatures.Caching.Fusion;
using static Altinn.DialogportenAdapter.WebApi.Features.Command.Sync.UrnParser;

namespace Altinn.DialogportenAdapter.WebApi.Features.Command.Sync;

internal sealed record GetReceiptDto(
    Guid DialogId,
    Guid TransactionId,
    string DialogToken,
    string? LanguageCode);

public abstract record GetReceiptResponse
{
    public sealed record Success(string Markdown) : GetReceiptResponse;

    public sealed record NotFound : GetReceiptResponse;

    public sealed record UnAuthorized : GetReceiptResponse;

    public sealed record InvalidLanguageCode : GetReceiptResponse;
}

internal sealed class InstanceReceipt(
    IStorageApi storageApi,
    IDialogTokenValidator dialogTokenValidator,
    IApplicationRepository applicationRepository,
    IDialogportenApi dialogportenApi,
    IAltinnOrgs altinnOrgs,
    IRegisterApi registerApi
    )
{
    private readonly IStorageApi _storageApi = storageApi ?? throw new ArgumentNullException(nameof(storageApi));
    private readonly IApplicationRepository _applicationRepository = applicationRepository ?? throw new ArgumentNullException(nameof(applicationRepository));
    private readonly IDialogTokenValidator _dialogTokenValidator = dialogTokenValidator ?? throw new ArgumentNullException(nameof(dialogTokenValidator));
    private readonly IDialogportenApi _dialogportenApi = dialogportenApi ?? throw new ArgumentNullException(nameof(dialogportenApi));
    private readonly IAltinnOrgs _altinnOrgs = altinnOrgs ?? throw new ArgumentNullException(nameof(altinnOrgs));
    private readonly IRegisterApi _registerApi = registerApi ?? throw new ArgumentNullException(nameof(registerApi));

    private const string DefaultLanguageCode = "nb";
    private const string InstanceReceiptSummaryKey = "receipt-transmission-summary";

    private static readonly List<string> LanguageCodes = ["nb", "nn", "en"];

    public static string GetSupportedLanguageCodes() => string.Join(", ", LanguageCodes);

    public async Task<GetReceiptResponse> GetReceipt(GetReceiptDto request, CancellationToken cancellationToken)
    {
        if (!ValidateDialogToken(request.DialogToken, request.DialogId, ["read"]))
            return new GetReceiptResponse.UnAuthorized();

        if (request.LanguageCode is not null && !LanguageCodes.Contains(request.LanguageCode))
            return new GetReceiptResponse.InvalidLanguageCode();

        var dialog = await _dialogportenApi.Get(request.DialogId, cancellationToken).ContentOrDefault();
        if (dialog is null)
            return new GetReceiptResponse.NotFound();

        if (!TryParseStorageUrn(dialog.ServiceOwnerContext?.ServiceOwnerLabels.FirstOrDefault()?.Value,
                out string? partyId, out var instanceGuid))
            return new GetReceiptResponse.NotFound();

        var instance = await _storageApi.GetInstance(partyId, instanceGuid, cancellationToken)
            .ContentOrDefault();
        if (instance is null)
            return new GetReceiptResponse.NotFound();

        var application = await _applicationRepository.GetApplication(instance.AppId, cancellationToken);
        if (application is null)
            return new GetReceiptResponse.NotFound();

        var transmission = dialog.Transmissions.FirstOrDefault(x => x.Id == request.TransactionId);
        if (transmission is null)
            return new GetReceiptResponse.NotFound();

        var orgs = await _altinnOrgs.GetAltinnOrgs(cancellationToken);
        if (orgs is null)
            return new GetReceiptResponse.NotFound();

        var langCode = request.LanguageCode ?? DefaultLanguageCode;
        var createdAt = transmission.CreatedAt
            .ToLocalTime()
            .ToString("dd.MM.yyyy / HH:mm", CultureInfo.InvariantCulture);
        var sender = await GetSender(dialog.Party, cancellationToken);
        var receiver = GetReceiverName(orgs, dialog.Org, langCode);
        var referenceNumber = instance.Id.Split("-").Last();

        var summary = await GetReceiptSummary(instance, application.VersionId, langCode, cancellationToken);
        var receipt =
            $"""
             | | |
             |---|---|
             | **{GetFieldText(langCode, FieldTexts.DateSent)}:** | {createdAt} |
             | **{GetFieldText(langCode, FieldTexts.Sender)}:** | {sender} |
             | **{GetFieldText(langCode, FieldTexts.Receiver)}:** | {receiver} |
             | **{GetFieldText(langCode, FieldTexts.ReferenceNumber)}:** | {referenceNumber} |

             {summary}
             """;
        return new GetReceiptResponse.Success(receipt);
    }

    private static string GetReceiverName(AltinnOrgData orgs, string orgCode, string languageCode)
    {
        if (!orgs.Orgs.TryGetValue(orgCode, out var org))
        {
            return orgCode;
        }

        return org.Name.GetValueOrDefault(languageCode) ?? orgCode;
    }

    private async Task<string> GetSender(string partyUrn, CancellationToken cancellationToken)
    {
        var partyResponse = await _registerApi.GetPartiesByUrns(new PartyQueryRequest([partyUrn]), cancellationToken);
        var partyIdentifier = partyResponse.Data.FirstOrDefault();
        if (partyIdentifier is not null)
        {
            string prePendSender = String.Empty;
            if (partyIdentifier.PersonIdentifier != null)
            {
                // Mask last 5 digits in the Norwegian national number
                prePendSender = $"{partyIdentifier.PersonIdentifier[..6]}*****-";
            }
            else if (partyIdentifier.OrganizationIdentifier != null)
            {
                prePendSender = $"{partyIdentifier.OrganizationIdentifier}-";
            }

            return $"{prePendSender}{partyIdentifier.DisplayName}";
        }
        return string.Empty;
    }

    private async Task<string> GetReceiptSummary(Instance instance, string versionId, string languageCode,
        CancellationToken cancellationToken)
    {
        var applicationTexts =
            await _applicationRepository.GetApplicationTexts(instance.AppId, versionId, cancellationToken);

        InstanceDerivedStatus status = InstanceDerivedStatus.ArchivedConfirmed;
        var receiptSummaries = ApplicationTextParser
            .GetLocalizationsFromApplicationTexts(InstanceReceiptSummaryKey,
                instance, applicationTexts, status);

        // Check if app have the "dp.receipt-transmission-summary" field set. If not return default text in correct language
        return receiptSummaries
            .Where(s => s.LanguageCode == languageCode)
            .Select(s => s.Value)
            .FirstOrDefault(GetFieldText(languageCode, FieldTexts.DefaultSummary));
    }

    private static string GetFieldText(string languageCode, FieldTexts fieldText)
    {
        var lang = languageCode switch
        {
            "en" => "en",
            "nn" => "nn",
            _ => "nb"
        };

        return (lang, fieldText) switch
        {
            ("en", FieldTexts.DateSent) => "Date sent",
            ("en", FieldTexts.DefaultSummary) =>
                "A mechanical check has been completed while filling in, but we reserve the right to detect errors during the processing of the case and that other documentation may be necessary. Please provide the reference number in case of any inquiries to the agency.",
            ("en", FieldTexts.Receiver) => "Receiver",
            ("en", FieldTexts.ReferenceNumber) => "Reference number",
            ("en", FieldTexts.Sender) => "Sender",

            ("nn", FieldTexts.DateSent) => "Dato sendt",
            ("nn", FieldTexts.DefaultSummary) =>
                "Det er gjennomført ein maskinell kontroll under utfylling, men vi tek atterhald om at det kan bli oppdaga feil under sakshandsaminga og at annan dokumentasjon kan vere naudsynt. Ver venleg oppgi referansenummer ved eventuelle førespurnadar til etaten.",
            ("nn", FieldTexts.Receiver) => "Mottakar",
            ("nn", FieldTexts.ReferenceNumber) => "Referansenummer",
            ("nn", FieldTexts.Sender) => "Avsendar",

            ("nb", FieldTexts.DateSent) => "Dato sendt",
            ("nb", FieldTexts.DefaultSummary) =>
                "Det er gjennomført en maskinell kontroll under utfylling, men vi tar forbehold om at det kan bli oppdaget feil under saksbehandlingen og at annen dokumentasjon kan være nødvendig. Vennligst oppgi referansenummer ved eventuelle henvendelser til etaten.",
            ("nb", FieldTexts.Receiver) => "Mottaker",
            ("nb", FieldTexts.ReferenceNumber) => "Referansenummer",
            ("nb", FieldTexts.Sender) => "Avsender",
            _ => throw new ArgumentOutOfRangeException(
                $"Unsupported combination: {lang}, {fieldText}")
        };
    }


    private bool ValidateDialogToken(ReadOnlySpan<char> token, Guid dialogId, string[] actions)
    {
        const string bearerPrefix = "Bearer ";
        token = token.StartsWith(bearerPrefix, StringComparison.OrdinalIgnoreCase)
            ? token[bearerPrefix.Length..]
            : token;
        var result = _dialogTokenValidator.Validate(token, dialogId, actions);
        return result.IsValid;
    }

    private enum FieldTexts
    {
        DateSent,
        DefaultSummary,
        Receiver,
        ReferenceNumber,
        Sender
    }
}

public static class UrnParser
{
    private static readonly Regex StorageUrnRegex = new(
        @"^urn:altinn:integration:storage:(?<partyId>\d+)/(?<instanceGuid>[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12})$",
        RegexOptions.Compiled
    );

    public static bool TryParseStorageUrn(string? urn, out string partyId, out Guid instanceGuid)
    {
        partyId = String.Empty;
        instanceGuid = Guid.Empty;

        if (string.IsNullOrWhiteSpace(urn))
            return false;

        var match = StorageUrnRegex.Match(urn);
        if (!match.Success)
            return false;

        partyId = match.Groups["partyId"].Value;

        return Guid.TryParse(match.Groups["instanceGuid"].Value, out instanceGuid);
    }
}
