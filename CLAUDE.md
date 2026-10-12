# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

**Current status and what to do next live in [PLAN.md](PLAN.md) — read it first.** This
file describes how the code works; `PLAN.md` tracks where the project stands, what is
verified vs. merely compiling, and the open decisions.

## Commands

```bash
# Tests — 38 of them, unit + integration
dotnet test

# A single test, or a class
dotnet test --filter "FullyQualifiedName~TodosApiTests.HealthCheck_ReturnsOk"
dotnet test --filter "FullyQualifiedName~InMemoryTodoRepositoryTests"

# API alone, http://localhost:5280 (defaults to the in-memory store, no Azure needed)
dotnet run --project src/TodoApp.Api

# React dev server, http://localhost:5173, proxies /api to 5280
cd src/todo-web && npm install && npm run dev

# Frontend type check
cd src/todo-web && npm run typecheck

# Run the way Azure does: one server, bundle served from wwwroot
cd src/todo-web && npm run build && cd ../.. && dotnet run --project src/TodoApp.Api
# then http://localhost:5280

# Provision Azure (needs `az login`; Azure CLI via `brew install azure-cli`)
./infra/provision.sh <app-name> rg-todo-app eastus
```

`dotnet build` treats warnings as errors on the API project (`TreatWarningsAsErrors`).

## Architecture

One Azure App Service (Linux F1 free tier) serves both the API and the React SPA. Vite's
`outDir` is `../TodoApp.Api/wwwroot`, so the bundle lands inside the API project and
ASP.NET Core serves it alongside `/api`. That is why there is no CORS configuration
anywhere — in production there is only one origin.

**Build order is load-bearing.** `npm run build` must run *before* `dotnet build`/`publish`,
because MSBuild globs `wwwroot` when it evaluates the project, not when it builds. Reverse
the order and the bundle silently does not ship. `azure-pipelines.yml` encodes this order
and has a comment saying so; preserve it when editing the pipeline.

### Repository swap is configuration, not build flavour

`ITodoRepository` has two implementations, selected in `Program.cs` from `Todo:Provider`:

- `InMemoryTodoRepository` — the default (`appsettings.json`), keeps local runs and CI free
  of any Azure dependency.
- `CosmosTodoRepository` — selected when `Todo:Provider` is `Cosmos`, which the Bicep
  template sets in App Service config.

Choosing at runtime rather than compile time is what lets the integration tests boot the
*real* application host with `WebApplicationFactory<Program>` and drive it over HTTP
without touching Azure. The bare `public partial class Program;` at the bottom of
`Program.cs` exists only to give those tests a type to reference — don't remove it.

When adding a repository method, implement it in **both** classes and keep the
`CompletedAt` invariant in `TodoCompletion.ResolveCompletedAt`, which exists specifically
so the two implementations cannot drift: stamp on first completion, clear on reopen,
preserve otherwise.

### Cosmos access

Partition key is `/id`. Reads/updates/deletes by id are point operations at 1 RU;
`GetAllAsync` is a deliberate cross-partition fan-out query (`ORDER BY c.createdAt DESC`),
acceptable at todo-app scale. A multi-user version of this app would want `/userId`
instead, which is a container recreation, not a config change.

Auth is managed identity only. `disableLocalAuth: true` on the Cosmos account means keys
do not work even if someone adds one, and `CosmosClient` falls back to
`DefaultAzureCredential` whenever `Cosmos:AccountKey` is blank. The key path exists solely
for the local emulator. There is no connection string or key in this repo, and the repo is
public — keep it that way.

The Cosmos client is configured with `JsonSerializerDefaults.Web` so stored documents use
the same camelCase as the API responses.

### Static file serving

`UseDefaultFiles` + `UseStaticFiles`, not `MapStaticAssets`, because the bundle is produced
*after* this project builds so no build-time asset manifest exists; Vite already
content-hashes filenames. `CompressionEnabled=false` in the csproj follows from that —
precompressed `.br`/`.gz` copies are only served by `MapStaticAssets`.
`MapFallbackToFile("index.html")` sends unmatched routes to the SPA for client-side routing.

## Validation, and why tests come in two layers

Validation attributes on the request records must sit on the **primary constructor
parameters**, not the generated properties — MVC throws
`ThrowIfRecordTypeHasValidationOnProperties` otherwise. There is a comment in
`TodoRequests.cs` to this effect.

`[Required]`/`[StringLength]` do not reject whitespace-only titles, so
`TodosController.TryNormalizeTitle` trims and rejects blanks, returning
`ValidationProblem` (400 `ValidationProblemDetails`). Titles are capped at
`TodoLimits.MaxTitleLength` (200).

A validation bug that returned HTTP 500 instead of 400 passed the controller unit tests
and was caught only by the integration tests, which exercise model binding, DataAnnotations,
status codes and JSON casing. Keep both layers: `TodosControllerTests` /
`InMemoryTodoRepositoryTests` call objects directly, `TodosApiTests` goes over HTTP with a
fresh host per test.

## Test runner

MSTest 4.x ships both the classic VSTest adapter and the newer Microsoft.Testing.Platform
runner. This project pins VSTest via `EnableMSTestRunner=false`, because that is what
`dotnet test` and the Azure Pipelines TRX publisher expect without extra setup. On
`10.0.100-rc.1`, an MTP project discovered zero tests under `dotnet test` while running
fine when invoked directly. README.md documents how to switch if you want to retry on GA.

## Environment state

- **The installed .NET 10 SDK is RC (`10.0.100-rc.1`), not GA.** It builds and tests fine
  but emits `NETSDK1057`, and App Service will not serve the RC runtime — a GA `10.0.x` SDK
  is required before deploying.
- **Azure CLI is not installed**, so `infra/provision.sh` cannot run yet.
- Nothing is provisioned in Azure and the Azure DevOps pipeline is not yet set up. The
  pipeline needs two variables once it is: `azureServiceConnection` and `webAppName`.

## Deployment shape

`infra/main.bicep` creates Cosmos DB (free tier, `todos` container at 400 RU/s), an F1
Linux App Service plan, and a web app with a system-assigned managed identity granted the
Cosmos DB Data Contributor role. `infra/PORTAL.md` is the same thing as clickable portal
steps.

The pipeline's **Build** stage runs on PRs and `main`; **Deploy** is gated on
`refs/heads/main`. The deploy smoke-tests `/healthz` with twelve retries because F1 has no
Always On and cold-starts after every deploy.

Cosmos free tier is **one account per subscription**. `provision.sh` detects an existing
one, warns, and deploys a billable account instead; pass `useCosmosFreeTier=false` to skip
the prompt.
