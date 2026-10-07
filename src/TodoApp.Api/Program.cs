using Azure.Identity;
using Microsoft.Azure.Cosmos;
using Microsoft.Extensions.Options;
using System.Text.Json;
using TodoApp.Api.Configuration;
using TodoApp.Api.Repositories;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks();

// "InMemory" (default) keeps local runs and CI free of any Azure dependency.
// The Azure deployment sets this to "Cosmos" via app settings.
var provider = builder.Configuration["Todo:Provider"] ?? "InMemory";

if (string.Equals(provider, "Cosmos", StringComparison.OrdinalIgnoreCase))
{
    builder.Services
        .AddOptions<CosmosOptions>()
        .Bind(builder.Configuration.GetSection(CosmosOptions.SectionName))
        .Validate(o => !string.IsNullOrWhiteSpace(o.AccountEndpoint),
            "Cosmos:AccountEndpoint must be set when Todo:Provider is 'Cosmos'.")
        .ValidateOnStart();

    builder.Services.AddSingleton(sp =>
    {
        var options = sp.GetRequiredService<IOptions<CosmosOptions>>().Value;

        var clientOptions = new CosmosClientOptions
        {
            // Match the API's camelCase JSON so documents read the same from either side.
            UseSystemTextJsonSerializerWithOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web),
        };

        return string.IsNullOrWhiteSpace(options.AccountKey)
            ? new CosmosClient(options.AccountEndpoint, new DefaultAzureCredential(), clientOptions)
            : new CosmosClient(options.AccountEndpoint, options.AccountKey, clientOptions);
    });

    builder.Services.AddSingleton(sp =>
    {
        var options = sp.GetRequiredService<IOptions<CosmosOptions>>().Value;
        return sp.GetRequiredService<CosmosClient>()
            .GetContainer(options.DatabaseName, options.ContainerName);
    });

    builder.Services.AddSingleton<ITodoRepository, CosmosTodoRepository>();
}
else
{
    builder.Services.AddSingleton<ITodoRepository, InMemoryTodoRepository>();
}

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

// Serves the React bundle that Vite writes into wwwroot. UseStaticFiles (rather than
// MapStaticAssets) because the bundle is produced after this project builds, so no
// build-time asset manifest exists; Vite already content-hashes its filenames.
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapHealthChecks("/healthz");
app.MapControllers();

// Any unmatched route falls through to the React app so client-side routing works.
app.MapFallbackToFile("index.html");

app.Run();

// Exposed so the integration tests can boot the real application host.
public partial class Program;
