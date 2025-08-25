using System.Text.Json;
using Altinn.DialogportenAdapter.WebApi.Infrastructure.Dialogporten;

namespace Altinn.DialogportenAdapter.WebApi.Common.Extensions;

public static class DialogDtoExtensions
{
    public static DialogDto? DeepClone(this DialogDto? dialog)
    {
        // TODO! Create something more efficient
        if (dialog == null) return null;
        var json = JsonSerializer.Serialize(dialog);
        return JsonSerializer.Deserialize<DialogDto>(json)!;
    }
}