namespace DialogportenAdapter.Clients;

public class AppStorageClient : IAppStorageClient
{
    private readonly HttpClient _httpClient;

    public AppStorageClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
}
