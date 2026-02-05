using System.Diagnostics.CodeAnalysis;
using System.Text.Json;

namespace Altinn.DialogportenAdapter.Unit.Tests.Common;

public static class Extensions
{
    extension<T>(T? value)
    {
        [return: NotNullIfNotNull(nameof(value))]
        public T? DeepClone() =>
            value is null ? default : JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value));
    }
}