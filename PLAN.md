# Project plan and status

Where this project stands and what happens next. **Update the Position line and the
step checkboxes as you go** — this file exists so that picking the project back up after
a gap costs minutes instead of an archaeology session.

Companion to `CLAUDE.md`: that file explains *how the code works*, this one tracks
*where we are*.

---

## Position

> **The application is code-complete and fully tested on the backend, but has never been
> run end to end by a human, and nothing exists in Azure yet.**
>
> Next action: **Step 1 — run it locally and click through it.**

Last verified: **2026-10-12**. Commit `fdce9bb`, clean, in sync with `origin/main`.

---

## What is actually true

Separating *verified* from *assumed* matters here, because "it builds and the tests pass"
is not the same claim as "it works".

| Area | State | Evidence |
| --- | --- | --- |
| Backend API | **Verified** | 38/38 tests pass, unit + integration over real HTTP |
| Frontend compiles | **Verified** | `tsc --noEmit` clean; bundle built into `wwwroot` |
| Frontend behaviour | **UNVERIFIED** | No test script exists; never opened in a browser |
| Git | **Verified** | Clean, `main` == `origin/main` == `fdce9bb` |
| Infra template | **Written, never executed** | `infra/main.bicep` has never been deployed |
| CI/CD pipeline | **Written, never executed** | `azure-pipelines.yml` has never run |
| Azure resources | **None exist** | Nothing provisioned |

### The gap worth knowing about

All 38 tests are backend. `src/todo-web` has no `test` script — only `dev`, `build`,
`preview`, `typecheck`. So the whole browser-to-API path is untested by anything:

- `fetch` calls and JSON casing round-trip
- the `204 No Content` branch in `send()` (returns `undefined`, not parsed JSON)
- `describeFailure()` rendering a `ValidationProblemDetails` body into a message

This is why Step 1 is first. Finding a frontend bug now costs one terminal; finding it
after provisioning and wiring a pipeline means three candidate causes instead of one.

---

## Plan

### Step 1 — Run it locally, end to end  ⬜

The only completely unverified layer, and the cheapest to check: no installs, no Azure,
no spend.

```bash
dotnet run --project src/TodoApp.Api        # terminal 1, :5280
cd src/todo-web && npm run dev              # terminal 2, :5173
```

Then at <http://localhost:5173> exercise: create, complete, reopen, edit, delete, and
**submit a blank/whitespace title** to confirm the 400 path renders a readable message
rather than a raw error.

Also worth running the single-server shape, since that is how Azure serves it and it
exercises `wwwroot` + `MapFallbackToFile` instead of the Vite proxy:

```bash
cd src/todo-web && npm run build && cd ../.. && dotnet run --project src/TodoApp.Api
# then http://localhost:5280
```

**Done when:** every CRUD operation works in the browser in both shapes, and invalid
input shows a sensible message.

---

### Step 2 — Commit `CLAUDE.md` and this file  ⬜

Both are currently untracked, which means they are exactly as losable as the session that
produced them.

**Done when:** `git status` is clean and pushed.

---

### Step 3 — Install the .NET 10 GA SDK  ⬜

The hard blocker on deployment. Installed SDK is `10.0.100-rc.1.25451.107`; it compiles
and tests fine but emits `NETSDK1057` (twice, confirmed), and **App Service will not serve
the RC runtime**.

Get a GA `10.0.x` from <https://dotnet.microsoft.com/download/dotnet/10.0>.

Keep this as its own step and re-run `dotnet test` immediately after, so that a GA
regression surfaces on its own rather than tangled up with a deployment failure.

**Done when:** `dotnet --list-sdks` shows a GA `10.0.x`, `dotnet build -c Release` is free
of `NETSDK1057`, and 38/38 still pass.

---

### Step 4 — Install Azure CLI and provision  ⬜

```bash
brew install azure-cli
az login
./infra/provision.sh <app-name> rg-todo-app eastus     # app-name: 3-20 lowercase alphanumeric
```

First step that creates real resources. **Resolve the Cosmos free-tier question below
before running this.**

Creates: Cosmos DB (free tier, `todos` container at 400 RU/s), Linux F1 App Service plan,
and a web app with a managed identity granted Cosmos DB Data Contributor.

**Done when:** the script prints `webAppName` and `webAppUrl` — record both here, Step 5
needs `webAppName`.

---

### Step 5 — Wire up Azure DevOps  ⬜

1. Create an Azure DevOps project — make it **Public**, which gets 10 Microsoft-hosted
   parallel jobs free. A private project needs a parallelism grant from Microsoft that
   takes several business days.
2. **Pipelines → New → GitHub**, authorize the app, pick `matsfan/todo-app`, choose
   *Existing YAML* → `/azure-pipelines.yml`.
3. **Project Settings → Service connections → New → Azure Resource Manager**, scoped to
   `rg-todo-app`.
4. Add pipeline variables: `azureServiceConnection` (the connection name) and `webAppName`
   (from Step 4).
5. Push to `main`.

**Done when:** the pipeline goes green and its `/healthz` smoke test returns 200. Expect
the health check to need several of its twelve retries — F1 has no Always On and
cold-starts after each deploy.

---

## Open decisions

**Cosmos DB free tier is one account per subscription.** Check the target subscription
before Step 4. If a free-tier account already exists anywhere in it, `provision.sh`
detects it, warns, and provisions a **billable** account instead — which quietly ends this
project's "runs entirely free" property. Decide knowingly: delete the other account, use a
different subscription, or accept the cost.

```bash
az cosmosdb list --query "[?enableFreeTier].{name:name,rg:resourceGroup}" -o table
```

**Azure DevOps project visibility.** Public gets free parallelism immediately; private
means waiting on Microsoft. The repo is already public, so this costs nothing — but it is
a deliberate choice, not an accident.

---

## Decisions already made

Recorded so they do not get re-litigated. Rationale for each is in `CLAUDE.md` or `README.md`.

- **One App Service serves both API and SPA.** Vite builds into the API's `wwwroot`.
  Keeps everything on one free resource and removes any need for CORS.
- **Managed identity only.** `disableLocalAuth: true` on Cosmos; no key or connection
  string anywhere in the repo, which matters because the repo is public.
- **Repository chosen by configuration, not build flavour.** This is what lets integration
  tests boot the real host with no Azure dependency.
- **Partition key `/id`.** 1 RU point reads; listing is a deliberate cross-partition
  fan-out. Fine at this scale. A multi-user version wants `/userId`, which would be a
  container recreation.
- **VSTest, not Microsoft.Testing.Platform** (`EnableMSTestRunner=false`). Revisit on the
  GA SDK — on RC1, an MTP project discovered zero tests under `dotnet test`.

## Deferred

- Frontend tests (Vitest + React Testing Library). The reason Step 1 is manual.
- Retry the MTP runner once on a GA SDK.
- Auth / multi-user. Would force the partition-key change above.

---

## Log

| Date | What happened |
| --- | --- |
| 2026-10-07 | App built and committed (`fdce9bb`); pushed to `matsfan/todo-app`. Scaffolded into `~/` by mistake, moved to `~/repos/todo-app`. |
| 2026-10-12 | Recovered the original session transcript. Re-verified: 38/38 tests, typecheck clean, git in sync. Wrote `CLAUDE.md` and this plan. |
