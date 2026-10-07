namespace TodoApp.Api.Configuration;

public class CosmosOptions
{
    public const string SectionName = "Cosmos";

    /// <summary>e.g. https://my-account.documents.azure.com:443/</summary>
    public string AccountEndpoint { get; set; } = string.Empty;

    public string DatabaseName { get; set; } = "tododb";

    public string ContainerName { get; set; } = "todos";

    /// <summary>
    /// Optional. Leave empty in Azure so the app authenticates with its managed identity;
    /// only set this for local runs against the Cosmos DB emulator.
    /// </summary>
    public string? AccountKey { get; set; }
}
