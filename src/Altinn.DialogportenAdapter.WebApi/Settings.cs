using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
using Altinn.ApiClients.Maskinporten.Config;
using Newtonsoft.Json;

namespace Altinn.DialogportenAdapter.WebApi;

public sealed record Settings(DialogportenAdapterSettings DialogportenAdapter);

public sealed record DialogportenAdapterSettings(
    MaskinportenSettings Maskinporten,
    AltinnPlatformSettings Altinn,
    DialogportenSettings Dialogporten,
    AdapterSettings Adapter,
    AuthenticationSettings Authentication,
    SyncAdapterSettings SyncAdapter);

public sealed record AuthenticationSettings(string JwtBearerWellKnown);

public record AdapterSettings(Uri BaseUri);

public sealed record DialogportenSettings(Uri BaseUri);

public sealed record AltinnPlatformSettings(Uri BaseUri, Uri ApiStorageEndpoint, string SubscriptionKey)
{
    public Uri GetAppUriForOrg(string org, string appId) => new($"{BaseUri.Scheme}://{org}.apps.{BaseUri.Host}/{appId}");
    public Uri GetPlatformUri() => new($"{BaseUri.Scheme}://platform.{BaseUri.Host}");
}

public record KeyVaultSettings(string ClientId, string ClientSecret, string TenantId, string SecretUri);

internal sealed record LocalDevelopmentSettings(bool MockDialogportenApi, bool DisableAuth)
{
    public const string ConfigurationSectionName = "LocalDevelopment";
}

internal static class LocalDevelopmentExtensions
{
    public static bool TryGetLocalDevelopmentSettings(this IConfiguration configuration, [NotNullWhen(true)] out LocalDevelopmentSettings? settings)
    {
        settings = configuration
            .GetSection(LocalDevelopmentSettings.ConfigurationSectionName)
            .Get<LocalDevelopmentSettings>();
        return settings is not null;
    }

    public static IConfigurationBuilder AddLocalDevelopmentSettings(this IConfigurationBuilder config, IHostEnvironment hostingEnvironment)
    {
        const string localAppsettingsJsonFileName = "appsettings.local.json";
        if (!hostingEnvironment.IsDevelopment())
        {
            return config;
        }

        config.AddJsonFile(localAppsettingsJsonFileName, optional: true, reloadOnChange: true);
        return config;
    }
}

[JsonObject(ItemNullValueHandling = NullValueHandling.Ignore)]
public class SyncAdapterSettings
{
    public static SyncAdapterSettings Instance = new();

    /// <summary>
    /// Gets or sets the flag controlling whether the sync adapter should disable all dialog synchronization.
    /// This overrides all other settings.
    /// </summary>
    [JsonProperty(PropertyName = "disableSync")]
    [DefaultValue(false)]
    public bool DisableSync { get; set; }

    /// <summary>
    /// Gets or sets the flag controlling whether the sync adapter should disable dialog creation.
    /// </summary>
    [JsonProperty(PropertyName = "disableCreate")]
    [DefaultValue(false)]
    public bool DisableCreate { get; set; }

    /// <summary>
    /// Gets or sets the flag controlling whether the sync adapter should disable dialog deletion.
    /// </summary>
    [JsonProperty(PropertyName = "disableDelete")]
    [DefaultValue(false)]
    public bool DisableDelete { get; set; }

    /// <summary>
    /// Gets or sets the flag controlling whether the sync adapter should disable adding activities.
    /// </summary>
    [JsonProperty(PropertyName = "disableAddActivities")]
    [DefaultValue(false)]
    public bool DisableAddActivities { get; set; }

    /// <summary>
    /// Gets or sets the flag controlling whether the sync adapter should disable adding transmissions.
    /// </summary>
    [JsonProperty(PropertyName = "disableAddTransmissions")]
    [DefaultValue(false)]
    public bool DisableAddTransmissions { get; set; }

    /// <summary>
    /// Gets or sets the flag controlling whether the sync adapter should disable synchronizing (overwrite) the visible from date.
    /// </summary>
    [JsonProperty(PropertyName = "disableSyncVisibleFrom")]
    [DefaultValue(false)]
    public bool DisableSyncVisibleFrom { get; set; }

    /// <summary>
    /// Gets or sets the flag controlling whether the sync adapter should disable synchronizing (overwrite) the due at date.
    /// </summary>
    [JsonProperty(PropertyName = "disableSyncDueAt")]
    [DefaultValue(false)]
    public bool DisableSyncDueAt { get; set; }

    /// <summary>
    /// Gets or sets the flag controlling whether the sync adapter should disable synchronizing (overwrite) the status.
    /// </summary>
    [JsonProperty(PropertyName = "disableSyncStatus")]
    [DefaultValue(false)]
    public bool DisableSyncStatus { get; set; }

    /// <summary>
    /// Gets or sets the flag controlling whether the sync adapter should disable synchronizing (overwrite) the title.
    /// </summary>
    [JsonProperty(PropertyName = "disableSyncContentTitle")]
    [DefaultValue(false)]
    public bool DisableSyncContentTitle { get; set; }

    /// <summary>
    /// Gets or sets the flag controlling whether the sync adapter should disable synchronizing (overwrite) the summary.
    /// </summary>
    [JsonProperty(PropertyName = "disableSyncContentSummary")]
    [DefaultValue(false)]
    public bool DisableSyncContentSummary { get; set; }

    /// <summary>
    /// Gets or sets the flag controlling whether the sync adapter should disable synchronizing attachments at the dialog level.
    /// Will only add/remove attachments with recognized id's, which are derived from the URL.
    /// </summary>
    [JsonProperty(PropertyName = "disableSyncAttachments")]
    [DefaultValue(false)]
    public bool DisableSyncAttachments { get; set; }

    /// <summary>
    /// Gets or sets the flag controlling whether the sync adapter should disable synchronizing API actions.
    /// Will only add/remove API actions with recognized id's, which are derived from the URL.
    /// </summary>
    [JsonProperty(PropertyName = "disableSyncApiActions")]
    [DefaultValue(false)]
    public bool DisableSyncApiActions { get; set; }

    /// <summary>
    /// Gets or sets the flag controlling whether the sync adapter should disable synchronizing GUI actions.
    /// Will only add/remove GUI actions with recognized id's, which are derived from the URL.
    /// </summary>
    [JsonProperty(PropertyName = "disableSyncGuiActions")]
    [DefaultValue(false)]
    public bool DisableSyncGuiActions { get; set; }
}