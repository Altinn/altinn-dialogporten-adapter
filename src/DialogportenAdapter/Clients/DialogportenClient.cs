namespace DialogportenAdapter.Clients;

public class DialogportenClient : IDialogportenClient
{
    private readonly HttpClient _httpClient;

    public DialogportenClient(HttpClient httpClient)
    {
        _httpClient = httpClient;
    }
}
