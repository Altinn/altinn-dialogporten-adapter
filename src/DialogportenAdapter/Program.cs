using DialogportenAdapter.Clients;
using DialogportenAdapter.Configuration;
using DialogportenAdapter.Services;

var builder = WebApplication.CreateBuilder(args);

// Adding configuration for user secrets in development environment
if (builder.Environment.IsDevelopment())
{
    builder.Configuration.AddUserSecrets<Program>();
}

// Adding logging
builder.Logging.AddConsole();
builder.Logging.AddDebug();

// Configure services
builder.Services
    // Configuration
    .Configure<GeneralSettings>(builder.Configuration.GetSection(nameof(GeneralSettings)))

    // Clients
    .AddHttpClient<IAppStorageClient, AppStorageClient>().Services
    .AddHttpClient<IDialogportenClient, DialogportenClient>().Services

    // Services
    .AddSingleton<IInstanceSyncService, InstanceSyncService>()

    // Utilities
    .AddEndpointsApiExplorer()
    .AddSwaggerGen();

builder.Services.AddControllers();

var app = builder.Build();

app.UseSwagger()
    .UseSwaggerUI()
    .UseHttpsRedirection()
    .UseAuthorization()
    .UseRouting()
    .UseEndpoints(endpoints =>
    {
        endpoints.MapControllers();
    });

await app.RunAsync();
