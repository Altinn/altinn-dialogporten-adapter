using AwesomeAssertions;

namespace Altinn.DialogportenAdapter.Test.Common.Extensions;

public static class HttpResponseExtensions
{
    extension(HttpResponseMessage message)
    {
        public void ShouldBeSuccess()
        {
            if (message.IsSuccessStatusCode) return;
            var body = message.Content.ReadAsStringAsync().Result;
            var bodyReason = string.IsNullOrEmpty(body) ? " Body was null/empty" : $" Body: {body}";
            message.IsSuccessStatusCode.Should()
                .BeTrue("Status code should be successful, but was {0}.{1}", (int)message.StatusCode, bodyReason);
        }
    }
}
