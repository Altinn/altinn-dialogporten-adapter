using System.Text.Json;
using WireMock;
using Xunit;

namespace Altinn.DialogportenAdapter.Integration.Tests.Common.Extensions;

public static class IRequestMessageExtensions
{
    extension(IRequestMessage requestMessage)
    {
        public T Deserialize<T>()
        {
            var body = requestMessage.Body;
            if (body == null)
            {
                Assert.Fail("Expected a request with a body");
            }

            return JsonSerializer.Deserialize<T>(body)!;
        }
    }
}
