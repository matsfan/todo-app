using Microsoft.AspNetCore.Mvc;
using TodoApp.Api.Models;
using TodoApp.Api.Repositories;

namespace TodoApp.Api.Controllers;

[ApiController]
[Route("api/todos")]
public class TodosController : ControllerBase
{
    private readonly ITodoRepository _repository;

    public TodosController(ITodoRepository repository) => _repository = repository;

    [HttpGet]
    public async Task<ActionResult<IReadOnlyList<TodoItem>>> GetAll(CancellationToken ct)
        => Ok(await _repository.GetAllAsync(ct));

    [HttpGet("{id}")]
    public async Task<ActionResult<TodoItem>> Get(string id, CancellationToken ct)
    {
        var item = await _repository.GetAsync(id, ct);
        return item is null ? NotFound() : Ok(item);
    }

    [HttpPost]
    public async Task<ActionResult<TodoItem>> Create(CreateTodoRequest request, CancellationToken ct)
    {
        if (!TryNormalizeTitle(request.Title, out var title))
        {
            return ValidationProblem(ModelState);
        }

        var created = await _repository.CreateAsync(new TodoItem { Title = title }, ct);
        return CreatedAtAction(nameof(Get), new { id = created.Id }, created);
    }

    [HttpPut("{id}")]
    public async Task<ActionResult<TodoItem>> Update(string id, UpdateTodoRequest request, CancellationToken ct)
    {
        if (!TryNormalizeTitle(request.Title, out var title))
        {
            return ValidationProblem(ModelState);
        }

        var updated = await _repository.UpdateAsync(id, title, request.IsComplete, ct);
        return updated is null ? NotFound() : Ok(updated);
    }

    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(string id, CancellationToken ct)
        => await _repository.DeleteAsync(id, ct) ? NoContent() : NotFound();

    /// <summary>
    /// Trims the title and rejects whitespace-only input, which passes the [Required]
    /// attribute but is not a usable todo.
    /// </summary>
    private bool TryNormalizeTitle(string raw, out string title)
    {
        title = raw.Trim();
        if (title.Length == 0)
        {
            ModelState.AddModelError(nameof(CreateTodoRequest.Title), "Title must not be blank.");
            return false;
        }

        return true;
    }
}
