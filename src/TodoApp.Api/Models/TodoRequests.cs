using System.ComponentModel.DataAnnotations;

namespace TodoApp.Api.Models;

// Validation attributes must sit on the primary constructor parameters, not on the
// generated properties: MVC throws ThrowIfRecordTypeHasValidationOnProperties for
// records whose validation metadata landed on the property instead.
public record CreateTodoRequest(
    [Required(AllowEmptyStrings = false)]
    [StringLength(TodoLimits.MaxTitleLength, MinimumLength = 1)]
    string Title);

public record UpdateTodoRequest(
    [Required(AllowEmptyStrings = false)]
    [StringLength(TodoLimits.MaxTitleLength, MinimumLength = 1)]
    string Title,
    bool IsComplete);

public static class TodoLimits
{
    public const int MaxTitleLength = 200;
}
