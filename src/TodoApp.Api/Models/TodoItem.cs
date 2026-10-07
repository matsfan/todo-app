namespace TodoApp.Api.Models;

/// <summary>
/// A single todo. Stored in Cosmos DB as-is, partitioned by <see cref="Id"/>.
/// </summary>
public class TodoItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("n");

    public string Title { get; set; } = string.Empty;

    public bool IsComplete { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset? CompletedAt { get; set; }
}
