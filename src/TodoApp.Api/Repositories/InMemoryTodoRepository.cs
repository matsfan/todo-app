using System.Collections.Concurrent;
using TodoApp.Api.Models;

namespace TodoApp.Api.Repositories;

/// <summary>
/// Default repository for local development and tests, so the app runs with no Azure
/// dependency. Contents are lost on restart.
/// </summary>
public class InMemoryTodoRepository : ITodoRepository
{
    private readonly ConcurrentDictionary<string, TodoItem> _items = new();

    public Task<IReadOnlyList<TodoItem>> GetAllAsync(CancellationToken ct = default)
    {
        IReadOnlyList<TodoItem> ordered = _items.Values
            .OrderByDescending(i => i.CreatedAt)
            .ToList();

        return Task.FromResult(ordered);
    }

    public Task<TodoItem?> GetAsync(string id, CancellationToken ct = default)
        => Task.FromResult(_items.TryGetValue(id, out var item) ? item : null);

    public Task<TodoItem> CreateAsync(TodoItem item, CancellationToken ct = default)
    {
        _items[item.Id] = item;
        return Task.FromResult(item);
    }

    public Task<TodoItem?> UpdateAsync(string id, string title, bool isComplete, CancellationToken ct = default)
    {
        if (!_items.TryGetValue(id, out var existing))
        {
            return Task.FromResult<TodoItem?>(null);
        }

        existing.Title = title;
        existing.CompletedAt = TodoCompletion.ResolveCompletedAt(existing, isComplete);
        existing.IsComplete = isComplete;

        return Task.FromResult<TodoItem?>(existing);
    }

    public Task<bool> DeleteAsync(string id, CancellationToken ct = default)
        => Task.FromResult(_items.TryRemove(id, out _));
}
