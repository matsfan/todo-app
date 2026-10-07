using Microsoft.VisualStudio.TestTools.UnitTesting;
using TodoApp.Api.Models;
using TodoApp.Api.Repositories;

namespace TodoApp.Api.Tests;

[TestClass]
public class InMemoryTodoRepositoryTests
{
    private InMemoryTodoRepository _repository = null!;

    [TestInitialize]
    public void Setup() => _repository = new InMemoryTodoRepository();

    [TestMethod]
    public async Task GetAllAsync_WhenEmpty_ReturnsEmptyList()
    {
        var all = await _repository.GetAllAsync();

        Assert.AreEqual(0, all.Count);
    }

    [TestMethod]
    public async Task CreateAsync_ThenGetAsync_RoundTripsTheItem()
    {
        var created = await _repository.CreateAsync(new TodoItem { Title = "Buy milk" });

        var fetched = await _repository.GetAsync(created.Id);

        Assert.IsNotNull(fetched);
        Assert.AreEqual("Buy milk", fetched.Title);
        Assert.IsFalse(fetched.IsComplete);
        Assert.IsNull(fetched.CompletedAt);
    }

    [TestMethod]
    public async Task GetAsync_WithUnknownId_ReturnsNull()
    {
        var fetched = await _repository.GetAsync("does-not-exist");

        Assert.IsNull(fetched);
    }

    [TestMethod]
    public async Task GetAllAsync_ReturnsNewestFirst()
    {
        var older = await _repository.CreateAsync(new TodoItem
        {
            Title = "Older",
            CreatedAt = DateTimeOffset.UtcNow.AddMinutes(-5),
        });
        var newer = await _repository.CreateAsync(new TodoItem
        {
            Title = "Newer",
            CreatedAt = DateTimeOffset.UtcNow,
        });

        var all = await _repository.GetAllAsync();

        Assert.AreEqual(2, all.Count);
        Assert.AreEqual(newer.Id, all[0].Id);
        Assert.AreEqual(older.Id, all[1].Id);
    }

    [TestMethod]
    public async Task UpdateAsync_MarkingComplete_StampsCompletedAt()
    {
        var created = await _repository.CreateAsync(new TodoItem { Title = "Ship it" });

        var updated = await _repository.UpdateAsync(created.Id, "Ship it", isComplete: true);

        Assert.IsNotNull(updated);
        Assert.IsTrue(updated.IsComplete);
        Assert.IsNotNull(updated.CompletedAt);
    }

    [TestMethod]
    public async Task UpdateAsync_ReopeningTodo_ClearsCompletedAt()
    {
        var created = await _repository.CreateAsync(new TodoItem { Title = "Ship it" });
        await _repository.UpdateAsync(created.Id, "Ship it", isComplete: true);

        var reopened = await _repository.UpdateAsync(created.Id, "Ship it", isComplete: false);

        Assert.IsNotNull(reopened);
        Assert.IsFalse(reopened.IsComplete);
        Assert.IsNull(reopened.CompletedAt);
    }

    [TestMethod]
    public async Task UpdateAsync_OnAlreadyCompleteTodo_PreservesOriginalCompletedAt()
    {
        var created = await _repository.CreateAsync(new TodoItem { Title = "Ship it" });
        var first = await _repository.UpdateAsync(created.Id, "Ship it", isComplete: true);
        var firstStamp = first!.CompletedAt;

        var second = await _repository.UpdateAsync(created.Id, "Ship it now", isComplete: true);

        Assert.AreEqual(firstStamp, second!.CompletedAt);
        Assert.AreEqual("Ship it now", second.Title);
    }

    [TestMethod]
    public async Task UpdateAsync_WithUnknownId_ReturnsNull()
    {
        var updated = await _repository.UpdateAsync("does-not-exist", "Nope", isComplete: true);

        Assert.IsNull(updated);
    }

    [TestMethod]
    public async Task DeleteAsync_RemovesTheItem()
    {
        var created = await _repository.CreateAsync(new TodoItem { Title = "Temporary" });

        var deleted = await _repository.DeleteAsync(created.Id);

        Assert.IsTrue(deleted);
        Assert.IsNull(await _repository.GetAsync(created.Id));
    }

    [TestMethod]
    public async Task DeleteAsync_WithUnknownId_ReturnsFalse()
    {
        var deleted = await _repository.DeleteAsync("does-not-exist");

        Assert.IsFalse(deleted);
    }
}
