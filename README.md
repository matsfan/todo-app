# Todo App

A small todo application that runs entirely on Azure free tiers.

| Layer | Technology |
| --- | --- |
| Frontend | React 19 + TypeScript 7, built with Vite 8 |
| Backend | ASP.NET Core Web API on .NET 10 (LTS) |
| Database | Azure Cosmos DB for NoSQL (free tier) |
| Tests | MSTest 4.5 (unit + integration) |
| CI/CD | Azure DevOps Pipelines |
| Hosting | Azure App Service, Linux **F1 (Free)** |

One App Service serves both the API and the React bundle: Vite builds into
`src/TodoApp.Api/wwwroot`, and ASP.NET Core serves it alongside `/api`. That keeps the
whole thing on a single free resource with no CORS configuration.

## Requirements

- **.NET 10 SDK** — `dotnet --list-sdks` must show a `10.0.x` GA build. A `10.0.100-rc.*`
  SDK builds the code but prints `NETSDK1057`, and the RC runtime is not offered by App
  Service. Install the GA SDK from <https://dotnet.microsoft.com/download/dotnet/10.0>.
- **Node.js 20 or newer** (developed against 24).
- **Azure CLI** — only needed to provision Azure (`brew install azure-cli`).

## Running locally

No Azure resources are needed. The API defaults to an in-memory store
(`Todo:Provider=InMemory`), so data resets when you stop it.

Terminal 1 — the API on <http://localhost:5280>:

```bash
dotnet run --project src/TodoApp.Api
```

Terminal 2 — the React dev server on <http://localhost:5173>, proxying `/api` to the API:

```bash
cd src/todo-web
npm install
npm run dev
```

Open <http://localhost:5173>.

To run the app the way Azure does — one server, bundle served from `wwwroot`:

```bash
cd src/todo-web && npm run build && cd ../..
dotnet run --project src/TodoApp.Api
# then open http://localhost:5280
```

## Tests

```bash
dotnet test
```

38 tests in two layers:

- **Unit tests** (`InMemoryTodoRepositoryTests`, `TodosControllerTests`) call the
  repository and controller directly — fast, no HTTP.
- **Integration tests** (`TodosApiTests`) boot the real application with
  `WebApplicationFactory` and drive it over HTTP. These cover model binding,
  DataAnnotations validation, status codes and JSON casing, none of which the unit tests
  touch. A validation bug that returned HTTP 500 instead of 400 slipped past the unit
  tests during development and was only caught here, so keep this layer.

Frontend type checking:

```bash
cd src/todo-web && npm run typecheck
```

### A note on the MSTest runner

MSTest 4.x ships both the classic VSTest adapter and the newer
Microsoft.Testing.Platform (MTP) runner. This project uses **VSTest**
(`EnableMSTestRunner=false`) because that is what `dotnet test` and the Azure Pipelines
TRX publisher expect without extra configuration.

To switch to MTP later, replace the test project's SDK and packages with
`<Project Sdk="MSTest.Sdk/4.5.1">` and add a `dotnet.config` containing:

```ini
[dotnet.test.runner]
name = "Microsoft.Testing.Platform"
```

Verify it on a GA .NET 10 SDK first — on `10.0.100-rc.1` the new `dotnet test` discovered
zero tests from an MTP project that ran correctly when invoked directly.

## API

Base path `/api/todos`.

| Method | Route | Success | Notes |
| --- | --- | --- | --- |
| GET | `/api/todos` | 200 | Newest first |
| GET | `/api/todos/{id}` | 200 / 404 | Point read, 1 RU |
| POST | `/api/todos` | 201 | Body `{ "title": "..." }`, returns `Location` |
| PUT | `/api/todos/{id}` | 200 / 404 | Body `{ "title": "...", "isComplete": true }` |
| DELETE | `/api/todos/{id}` | 204 / 404 | |
| GET | `/healthz` | 200 | Used by the pipeline smoke test |
| GET | `/openapi/v1.json` | 200 | Development environment only |

Titles are trimmed, must be non-blank, and are capped at 200 characters. Invalid input
returns a 400 `ValidationProblemDetails` body. Completing a todo stamps `completedAt`;
reopening it clears the stamp.

## Deploying to Azure

### 1. Provision

```bash
az login
./infra/provision.sh mytodo rg-todo-app eastus
```

`<app-name>` must be 3-20 lowercase letters and digits. The script validates the
template, deploys it, and prints the generated web app name and URL.
`infra/PORTAL.md` documents the same resources as portal steps if you prefer clicking.

What gets created, all within free allowances:

- **Cosmos DB for NoSQL**, free tier (1000 RU/s + 25 GB free), with a `todos` container
  partitioned on `/id` at 400 RU/s.
- **App Service plan**, Linux **F1 (Free)**.
- **Web app** with a system-assigned managed identity, granted the built-in Cosmos DB
  Data Contributor role.

Key-based auth is **disabled** on the Cosmos account (`disableLocalAuth: true`), so the
app authenticates purely with its managed identity. There is no connection string or key
anywhere in this repository — which matters, since the source lives in a public repo.

> **Cosmos free tier is one account per subscription.** If you already have one, the
> script detects it, warns you, and deploys a billable account instead. Pass
> `useCosmosFreeTier=false` explicitly to skip the prompt.

### 2. Set up the pipeline

The source lives on GitHub; Azure Pipelines builds it from there.

1. Create an Azure DevOps project and make it **Public** — public projects get 10
   Microsoft-hosted parallel jobs for free with no waiting. A private project is limited
   to a grant you must request from Microsoft, which takes a few business days.
2. **Pipelines > New pipeline > GitHub**, authorize the Azure Pipelines GitHub App, pick
   the repository, and choose *Existing Azure Pipelines YAML file* → `/azure-pipelines.yml`.
3. **Project Settings > Service connections > New > Azure Resource Manager**, scoped to
   the `rg-todo-app` resource group.
4. On the pipeline, add two variables (**Edit > Variables**):

   | Variable | Value |
   | --- | --- |
   | `azureServiceConnection` | the service connection name from step 3 |
   | `webAppName` | the `webAppName` printed by `provision.sh` |

5. Push to `main`.

The pipeline runs two stages. **Build** installs the SDKs, builds the React bundle into
`wwwroot` *before* the .NET build (MSBuild globs that folder at evaluation time, so the
order matters), runs `dotnet test` with TRX and Cobertura output, publishes the results
and coverage, then uploads the published app as an artifact. **Deploy** runs only on
`main`, pushes the artifact to App Service, and polls `/healthz` until it returns 200 —
with retries, because F1 has no Always On and cold-starts after each deploy.

Pull requests build and test but do not deploy.

### Free-tier limits worth knowing

- **F1 App Service**: 60 CPU-minutes/day, 1 GB RAM, 1 GB storage, no Always On (so the
  first request after idling is slow), no custom-domain TLS, no deployment slots. Fine
  for a todo app, not for production traffic.
- **Cosmos DB free tier**: first 1000 RU/s and 25 GB free, forever, one account per
  subscription. Usage beyond that is billed.
- **Azure DevOps**: 10 free parallel jobs on public projects; private projects need the
  requested grant (1 job, 1800 minutes/month).

### Tearing it down

```bash
az group delete --name rg-todo-app --yes --no-wait
```

## Project layout

```
TodoApp.sln
azure-pipelines.yml          Build + deploy stages
infra/
  main.bicep                 Cosmos DB + App Service F1 + managed identity
  provision.sh              One-time provisioning
  PORTAL.md                 Portal walkthrough (same resources)
src/
  TodoApp.Api/              ASP.NET Core Web API
    Controllers/            TodosController
    Models/                 TodoItem, request records
    Repositories/           ITodoRepository, InMemory + Cosmos implementations
    Configuration/          CosmosOptions
  todo-web/                 React + TypeScript + Vite
tests/
  TodoApp.Api.Tests/        MSTest unit + integration tests
```

`ITodoRepository` has two implementations. `InMemoryTodoRepository` is the default and
keeps local development and CI free of any Azure dependency; `CosmosTodoRepository` is
selected when `Todo:Provider` is `Cosmos`, which the Bicep template sets in App Service
configuration. Swapping on configuration rather than on build flavour is what lets the
integration tests exercise the real HTTP pipeline without touching Azure.

## Configuration

| Setting | Default | Purpose |
| --- | --- | --- |
| `Todo:Provider` | `InMemory` | `InMemory` or `Cosmos` |
| `Cosmos:AccountEndpoint` | — | Required when provider is `Cosmos` |
| `Cosmos:DatabaseName` | `tododb` | |
| `Cosmos:ContainerName` | `todos` | |
| `Cosmos:AccountKey` | — | Local emulator only; leave unset in Azure to use managed identity |

In App Service, nested keys use double underscores: `Cosmos__AccountEndpoint`.
