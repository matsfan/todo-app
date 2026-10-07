using TodoApp.Api.Models;

namespace TodoApp.Api.Repositories;

public interface ITodoRepository
{
    Task<IReadOnlyList<TodoItem>> GetAllAsync(CancellationToken ct = default);

    Task<TodoItem?> GetAsync(string id, CancellationToken ct = default);

    Task<TodoItem> CreateAsync(TodoItem item, CancellationToken ct = default);

    /// <summary>Returns the updated item, or null when no todo has that id.</summary>
    Task<TodoItem?> UpdateAsync(string id, string title, bool isComplete, CancellationToken ct = default);

    /// <summary>Returns false when no todo has that id.</summary>
    Task<bool> DeleteAsync(string id, CancellationToken ct = default);
}
