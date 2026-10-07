using System.Net;
using Microsoft.Azure.Cosmos;
using TodoApp.Api.Models;

namespace TodoApp.Api.Repositories;

public class CosmosTodoRepository : ITodoRepository
{
    private readonly Container _container;

    public CosmosTodoRepository(Container container) => _container = container;

    public async Task<IReadOnlyList<TodoItem>> GetAllAsync(CancellationToken ct = default)
    {
        var query = new QueryDefinition("SELECT * FROM c ORDER BY c.createdAt DESC");
        var results = new List<TodoItem>();

        using var iterator = _container.GetItemQueryIterator<TodoItem>(query);
        while (iterator.HasMoreResults)
        {
            results.AddRange(await iterator.ReadNextAsync(ct));
        }

        return results;
    }

    public async Task<TodoItem?> GetAsync(string id, CancellationToken ct = default)
    {
        try
        {
            // Partition key is /id, so this is a point read: 1 RU.
            var response = await _container.ReadItemAsync<TodoItem>(id, new PartitionKey(id), cancellationToken: ct);
            return response.Resource;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<TodoItem> CreateAsync(TodoItem item, CancellationToken ct = default)
    {
        var response = await _container.CreateItemAsync(item, new PartitionKey(item.Id), cancellationToken: ct);
        return response.Resource;
    }

    public async Task<TodoItem?> UpdateAsync(string id, string title, bool isComplete, CancellationToken ct = default)
    {
        var existing = await GetAsync(id, ct);
        if (existing is null)
        {
            return null;
        }

        existing.Title = title;
        existing.CompletedAt = TodoCompletion.ResolveCompletedAt(existing, isComplete);
        existing.IsComplete = isComplete;

        var response = await _container.ReplaceItemAsync(existing, id, new PartitionKey(id), cancellationToken: ct);
        return response.Resource;
    }

    public async Task<bool> DeleteAsync(string id, CancellationToken ct = default)
    {
        try
        {
            await _container.DeleteItemAsync<TodoItem>(id, new PartitionKey(id), cancellationToken: ct);
            return true;
        }
        catch (CosmosException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return false;
        }
    }
}
