using Altinn.DialogportenAdapter.WebApi.Infrastructure.Storage;
using Altinn.Platform.Storage.Interface.Models;

namespace Altinn.DialogportenAdapter.Unit.Tests.Infrastructure.Storage;

public class ApplicationRepositoryTests
{
    [Fact]
    public void CreateTextsDictionary_KeepsFirstOccurrenceForDuplicateIds()
    {
        var resources = new List<TextResourceElement>
        {
            new() { Id = "dp.title", Value = "first-title" },
            new() { Id = "dp.title", Value = "second-title" },
            new() { Id = "dp.summary", Value = "summary" }
        };

        var result = ApplicationRepository.CreateTextsDictionary(resources);

        Assert.Equal(2, result.Count);
        Assert.Equal("first-title", result["dp.title"]);
        Assert.Equal("summary", result["dp.summary"]);
    }
}
