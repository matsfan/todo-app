using Microsoft.AspNetCore.Mvc;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TodoApp.Api.Controllers;
using TodoApp.Api.Models;
using TodoApp.Api.Repositories;

namespace TodoApp.Api.Tests;

[TestClass]
public class TodosControllerTests
{
    private InMemoryTodoRepository _repository = null!;
    private TodosController _controller = null!;

    [TestInitialize]
    public void Setup()
    {
        _repository = new InMemoryTodoRepository();
        _controller = new TodosController(_repository);
    }

    [TestMethod]
    public async Task GetAll_ReturnsOkWithItems()
    {
        await _repository.CreateAsync(new TodoItem { Title = "First" });

        var result = await _controller.GetAll(CancellationToken.None);

        var ok = result.Result as OkObjectResult;
        Assert.IsNotNull(ok);
        var items = ok.Value as IReadOnlyList<TodoItem>;
        Assert.IsNotNull(items);
        Assert.AreEqual(1, items.Count);
    }

    [TestMethod]
    public async Task Get_WithUnknownId_ReturnsNotFound()
    {
        var result = await _controller.Get("does-not-exist", CancellationToken.None);

        Assert.IsInstanceOfType<NotFoundResult>(result.Result);
    }

    [TestMethod]
    public async Task Create_ReturnsCreatedAtActionWithTheNewTodo()
    {
        var result = await _controller.Create(new CreateTodoRequest("Walk the dog"), CancellationToken.None);

        var created = result.Result as CreatedAtActionResult;
        Assert.IsNotNull(created);
        var item = created.Value as TodoItem;
        Assert.IsNotNull(item);
        Assert.AreEqual("Walk the dog", item.Title);
        Assert.AreEqual(nameof(TodosController.Get), created.ActionName);
        Assert.AreEqual(item.Id, created.RouteValues!["id"]);
    }

    [TestMethod]
    public async Task Create_TrimsSurroundingWhitespaceFromTitle()
    {
        var result = await _controller.Create(new CreateTodoRequest("   Pay rent  "), CancellationToken.None);

        var created = result.Result as CreatedAtActionResult;
        Assert.IsNotNull(created);
        Assert.AreEqual("Pay rent", ((TodoItem)created.Value!).Title);
    }

    [TestMethod]
    [DataRow(" ")]
    [DataRow("   ")]
    [DataRow("\t")]
    [DataRow("\n")]
    public async Task Create_WithWhitespaceOnlyTitle_ReturnsValidationProblem(string title)
    {
        var result = await _controller.Create(new CreateTodoRequest(title), CancellationToken.None);

        Assert.IsInstanceOfType<ObjectResult>(result.Result);
        Assert.AreEqual(0, (await _repository.GetAllAsync()).Count, "no todo should be stored");
    }

    [TestMethod]
    public async Task Update_ChangesTitleAndCompletion()
    {
        var created = await _repository.CreateAsync(new TodoItem { Title = "Draft" });

        var result = await _controller.Update(
            created.Id,
            new UpdateTodoRequest("Final", IsComplete: true),
            CancellationToken.None);

        var ok = result.Result as OkObjectResult;
        Assert.IsNotNull(ok);
        var item = ok.Value as TodoItem;
        Assert.IsNotNull(item);
        Assert.AreEqual("Final", item.Title);
        Assert.IsTrue(item.IsComplete);
    }

    [TestMethod]
    public async Task Update_WithUnknownId_ReturnsNotFound()
    {
        var result = await _controller.Update(
            "does-not-exist",
            new UpdateTodoRequest("Nope", IsComplete: false),
            CancellationToken.None);

        Assert.IsInstanceOfType<NotFoundResult>(result.Result);
    }

    [TestMethod]
    public async Task Update_WithWhitespaceOnlyTitle_LeavesTodoUnchanged()
    {
        var created = await _repository.CreateAsync(new TodoItem { Title = "Keep me" });

        var result = await _controller.Update(
            created.Id,
            new UpdateTodoRequest("  ", IsComplete: true),
            CancellationToken.None);

        Assert.IsInstanceOfType<ObjectResult>(result.Result);
        var unchanged = await _repository.GetAsync(created.Id);
        Assert.AreEqual("Keep me", unchanged!.Title);
        Assert.IsFalse(unchanged.IsComplete);
    }

    [TestMethod]
    public async Task Delete_ReturnsNoContent()
    {
        var created = await _repository.CreateAsync(new TodoItem { Title = "Gone" });

        var result = await _controller.Delete(created.Id, CancellationToken.None);

        Assert.IsInstanceOfType<NoContentResult>(result);
    }

    [TestMethod]
    public async Task Delete_WithUnknownId_ReturnsNotFound()
    {
        var result = await _controller.Delete("does-not-exist", CancellationToken.None);

        Assert.IsInstanceOfType<NotFoundResult>(result);
    }
}
