namespace Altinn.DialogportenAdapter.WebApi.Features.Command.Sync;

internal static class GuiActionConstants
{
    public const string GoTo = "DialogGuiActionGoTo";
    public const string Delete = "DialogGuiActionDelete";
    public const string Copy = "DialogGuiActionCopy";

    public static List<string> Keys = [ GoTo, Delete, Copy ];
}