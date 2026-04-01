namespace Altinn.DialogportenAdapter.WebApi.Common;

public static class MediaTypes
{
    public const string EmbeddablePrefix = "application/vnd.dialogporten.frontchannelembed-url";
    public const string EmbeddableMarkdown = $"{EmbeddablePrefix};type=text/markdown";

    public const string Markdown = "text/markdown";
    public const string PlainText = "text/plain";
}
