namespace Altinn.Platform.DialogportenAdapter.WebApi.Common;

public static class MediaTypes
{
    public const string EmbeddablePrefix = "application/vnd.dialogporten.frontchannelembed";
    public const string EmbeddableMarkdown = $"{EmbeddablePrefix}+json;type=markdown";
    public const string LegacyEmbeddableHtml = $"{EmbeddablePrefix}+json;type=html";

    public const string LegacyHtml = "text/html";
    public const string Markdown = "text/markdown";
    public const string PlainText = "text/plain";
}