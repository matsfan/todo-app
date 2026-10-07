using TodoApp.Api.Models;

namespace TodoApp.Api.Repositories;

/// <summary>
/// Keeps the CompletedAt stamp consistent across repository implementations: set it the
/// first time a todo is completed, clear it when reopened, preserve it otherwise.
/// </summary>
public static class TodoCompletion
{
    public static DateTimeOffset? ResolveCompletedAt(TodoItem existing, bool isComplete)
        => isComplete
            ? existing.CompletedAt ?? DateTimeOffset.UtcNow
            : null;
}
