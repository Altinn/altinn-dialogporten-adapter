using Altinn.DialogportenAdapter.WebApi.Common;
using Altinn.DialogportenAdapter.WebApi.Common.Extensions;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;

namespace Altinn.DialogportenAdapter.Unit.Tests.Common.AssertHelpers;

public static class Regulars
{
    public static class ServiceOwnerContexts
    {
        public static readonly ServiceOwnerContext DefaultContext = new()
        {
            ServiceOwnerLabels = [new ServiceOwnerLabel { Value = "urn:altinn:integration:storage:instance-id" }]
        };
    }

    public static class Content
    {
        public static readonly ContentDto ReadyForSubmission = new()
        {
            Summary = new ContentValueDto
            {
                Value =
                [
                    new LocalizationDto
                    {
                        LanguageCode = "nb",
                        Value = "Innsendingen er klar for å fylles ut."
                    },
                    new LocalizationDto
                    {
                        LanguageCode = "nn",
                        Value = "Innsendinga er klar til å fyllast ut."
                    },
                    new LocalizationDto
                    {
                        LanguageCode = "en",
                        Value = "The submission is ready to be filled out."
                    }
                ],
                MediaType = "text/plain"
            },
            Title = new ContentValueDto
            {
                Value =
                [
                    new LocalizationDto { Value = "Test applikasjon", LanguageCode = "nb" },
                    new LocalizationDto { Value = "Test application", LanguageCode = "en" },
                ],
                MediaType = "text/plain",
            },
            AdditionalInfo = null,
            ExtendedStatus = null,
        };

        public static readonly ContentDto Submitted = new ContentDto
        {
            Summary = new ContentValueDto
            {
                Value =
                [
                    new LocalizationDto { LanguageCode = "nb", Value = "Innsendingen er bekreftet mottatt." },
                    new LocalizationDto { LanguageCode = "nn", Value = "Innsendinga er stadfesta motteken." },
                    new LocalizationDto
                    {
                        LanguageCode = "en",
                        Value = "The submission has been confirmed as received."
                    }
                ],
                MediaType = "text/plain"
            },
            Title = new ContentValueDto
            {
                Value =
                [
                    new LocalizationDto { Value = "Test applikasjon", LanguageCode = "nb" },
                    new LocalizationDto { Value = "Test application", LanguageCode = "en" },
                ],
                MediaType = "text/plain",
            },
            ExtendedStatus = null,
        };
    }

    public static class GuiActions
    {
        public static GuiActionDto GoTo(Guid dialogId)
        {
            return new GuiActionDto
            {
                Id = dialogId.CreateDeterministicSubUuidV7(Constants.GuiAction.GoTo),
                Action = "read",
                Url = "http://platform.altinn.localhost/authentication/api/v1/authentication?goto=http%3A%2F%2Fplatform.altinn.localhost%2Freceipt%2Finstance-id%3FdontChooseReportee%3Dtrue",
                AuthorizationAttribute = null,
                IsDeleteDialogAction = false,
                HttpMethod = HttpVerb.GET,
                Priority = DialogGuiActionPriority.Primary,
                Title =
                [
                    new LocalizationDto { LanguageCode = "nb", Value = "Se innsendt skjema" },
                    new LocalizationDto { LanguageCode = "nn", Value = "Sjå innsendt skjema" },
                    new LocalizationDto { LanguageCode = "en", Value = "See submitted form" }
                ],
                Prompt = null
            };
        }

        public static GuiActionDto Delete(Guid dialogId)
        {
            return new GuiActionDto
            {
                Id = dialogId.CreateDeterministicSubUuidV7(Constants.GuiAction.Delete),
                Action = "delete",
                Url = "http://adapter.localhost/api/v1/instance/instance-id",
                AuthorizationAttribute = null,
                IsDeleteDialogAction = true,
                HttpMethod = HttpVerb.DELETE,
                Priority = DialogGuiActionPriority.Secondary,
                Title =
                [
                    new LocalizationDto { LanguageCode = "nb", Value = "Slett" },
                    new LocalizationDto { LanguageCode = "nn", Value = "Slett" },
                    new LocalizationDto { LanguageCode = "en", Value = "Delete" }
                ],
                Prompt = null
            };
        }

        public static GuiActionDto Write(Guid dialogId, string? authorizationAttribute = null)
        {
            return
                new GuiActionDto
                {
                    Id = dialogId.CreateDeterministicSubUuidV7(Constants.GuiAction.GoTo),
                    Action = "write",
                    Url =
                        "http://platform.altinn.localhost/authentication/api/v1/authentication?goto=http%3A%2F%2Forg.apps.altinn.localhost%2Furn%3Aaltinn%3Ainstance-id%2F%3FdontChooseReportee%3Dtrue%23%2Finstance%2Finstance-id",
                    AuthorizationAttribute = authorizationAttribute,
                    IsDeleteDialogAction = false,
                    HttpMethod = HttpVerb.GET,
                    Priority = DialogGuiActionPriority.Primary,
                    Title =
                    [
                        new LocalizationDto { LanguageCode = "nb", Value = "Gå til skjemautfylling" },
                        new LocalizationDto { LanguageCode = "nn", Value = "Gå til skjemautfylling" },
                        new LocalizationDto { LanguageCode = "en", Value = "Go to form completion" }
                    ],
                    Prompt = null
                };
        }
    }

    public static class ApiActions
    {
        public static ApiActionDto GetSourceApiAction(Guid dialogId)
        {
            var apiActionId = dialogId.CreateDeterministicSubUuidV7(Constants.ApiAction.Read);
            return new ApiActionDto
            {
                Id = apiActionId,
                Action = "read",
                AuthorizationAttribute = null,
                Endpoints = [new ApiActionEndpointDto
                    {
                        Id = apiActionId.CreateDeterministicSubUuidV7("0"),
                        Version = "1.0",
                        Url = "http://org.apps.altinn.localhost/urn:altinn:instance-id/instances/instance-id",
                        HttpMethod = HttpVerb.GET,
                        DocumentationUrl = null,
                        RequestSchema = null,
                        ResponseSchema = null,
                        Deprecated = false,
                        SunsetAt = null
                    }
                ]
            };
        }
    }
}
