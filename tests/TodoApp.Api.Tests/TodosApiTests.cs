using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TodoApp.Api.Models;

namespace TodoApp.Api.Tests;

/// <summary>
/// Drives the real application host over HTTP, so model binding, DataAnnotations
/// validation, status codes and JSON casing are all exercised. The controller unit
/// tests bypass that pipeline and cannot catch regressions in it.
/// </summary>
[TestClass]
public class TodosApiTests
{
    private WebApplicationFactory<Program> _factory = null!;
    private HttpClient _client = null!;

    [TestInitialize]
    public void Setup()
    {
        // A fresh host per test, so the in-memory store starts empty each time.
        _factory = new WebApplicationFactory<Program>();
        _client = _factory.CreateClient();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _client.Dispose();
        _factory.Dispose();
    }

    [TestMethod]
    public async Task HealthCheck_ReturnsOk()
    {
        var response = await _client.GetAsync("/healthz");

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);
    }

    [TestMethod]
    public async Task GetAll_OnAFreshHost_ReturnsEmptyArray()
    {
        var items = await _client.GetFromJsonAsync<List<TodoItem>>("/api/todos");

        Assert.IsNotNull(items);
        Assert.AreEqual(0, items.Count);
    }

    [TestMethod]
    public async Task Post_WithValidTitle_Returns201AndLocationHeader()
    {
        var response = await _client.PostAsJsonAsync("/api/todos", new { title = "Renew passport" });

        Assert.AreEqual(HttpStatusCode.Created, response.StatusCode);
        Assert.IsNotNull(response.Headers.Location);

        var created = await response.Content.ReadFromJsonAsync<TodoItem>();
        Assert.IsNotNull(created);
        Assert.AreEqual("Renew passport", created.Title);

        // The Location header must actually resolve.
        var followed = await _client.GetAsync(response.Headers.Location);
        Assert.AreEqual(HttpStatusCode.OK, followed.StatusCode);
    }

    [TestMethod]
    public async Task Post_SerializesResponseAsCamelCase()
    {
        var response = await _client.PostAsJsonAsync("/api/todos", new { title = "Check casing" });

        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var root = document.RootElement;

        Assert.IsTrue(root.TryGetProperty("id", out _));
        Assert.IsTrue(root.TryGetProperty("isComplete", out _));
        Assert.IsTrue(root.TryGetProperty("createdAt", out _));
    }

    [TestMethod]
    public async Task Post_TrimsTheTitle()
    {
        var response = await _client.PostAsJsonAsync("/api/todos", new { title = "   Mow the lawn  " });

        var created = await response.Content.ReadFromJsonAsync<TodoItem>();
        Assert.AreEqual("Mow the lawn", created!.Title);
    }

    [TestMethod]
    public async Task Post_WithWhitespaceOnlyTitle_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/todos", new { title = "   " });

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task Post_WithEmptyTitle_Returns400()
    {
        var response = await _client.PostAsJsonAsync("/api/todos", new { title = "" });

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task Post_WithTitleOverTheLengthLimit_Returns400()
    {
        var tooLong = new string('x', TodoLimits.MaxTitleLength + 1);

        var response = await _client.PostAsJsonAsync("/api/todos", new { title = tooLong });

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task Post_WithMalformedJson_Returns400()
    {
        using var content = new StringContent("{ not json", Encoding.UTF8, "application/json");

        var response = await _client.PostAsync("/api/todos", content);

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task Put_UpdatesTitleAndCompletion()
    {
        var created = await CreateTodoAsync("Write the README");

        var response = await _client.PutAsJsonAsync(
            $"/api/todos/{created.Id}",
            new { title = "Write the README properly", isComplete = true });

        Assert.AreEqual(HttpStatusCode.OK, response.StatusCode);

        var updated = await response.Content.ReadFromJsonAsync<TodoItem>();
        Assert.IsNotNull(updated);
        Assert.AreEqual("Write the README properly", updated.Title);
        Assert.IsTrue(updated.IsComplete);
        Assert.IsNotNull(updated.CompletedAt);
    }

    [TestMethod]
    public async Task Put_WithUnknownId_Returns404()
    {
        var response = await _client.PutAsJsonAsync(
            "/api/todos/does-not-exist",
            new { title = "Nope", isComplete = false });

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task Put_WithBlankTitle_Returns400()
    {
        var created = await CreateTodoAsync("Keep me");

        var response = await _client.PutAsJsonAsync(
            $"/api/todos/{created.Id}",
            new { title = "  ", isComplete = true });

        Assert.AreEqual(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [TestMethod]
    public async Task Delete_Returns204AndRemovesTheTodo()
    {
        var created = await CreateTodoAsync("Temporary");

        var response = await _client.DeleteAsync($"/api/todos/{created.Id}");

        Assert.AreEqual(HttpStatusCode.NoContent, response.StatusCode);

        var afterwards = await _client.GetAsync($"/api/todos/{created.Id}");
        Assert.AreEqual(HttpStatusCode.NotFound, afterwards.StatusCode);
    }

    [TestMethod]
    public async Task Delete_WithUnknownId_Returns404()
    {
        var response = await _client.DeleteAsync("/api/todos/does-not-exist");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    [TestMethod]
    public async Task Get_WithUnknownId_Returns404()
    {
        var response = await _client.GetAsync("/api/todos/does-not-exist");

        Assert.AreEqual(HttpStatusCode.NotFound, response.StatusCode);
    }

    private async Task<TodoItem> CreateTodoAsync(string title)
    {
        var response = await _client.PostAsJsonAsync("/api/todos", new { title });
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<TodoItem>())!;
    }
}
