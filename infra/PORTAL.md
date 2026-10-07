# Portal fallback

`main.bicep` is the source of truth. Use this walkthrough only if you would rather click
through the Azure Portal, or to understand what the template creates.

Everything below stays inside free allowances, with one caveat: the Cosmos DB free tier
is limited to **one account per subscription**.

## 1. Resource group

**Resource groups > Create**

- Name: `rg-todo-app`
- Region: pick one near you (the examples use East US)

## 2. Cosmos DB account

**Create a resource > Azure Cosmos DB > Azure Cosmos DB for NoSQL**

| Setting | Value |
| --- | --- |
| Resource group | `rg-todo-app` |
| Account name | globally unique, e.g. `cosmos-mytodo-01` |
| Capacity mode | Provisioned throughput |
| Apply Free Tier Discount | **Apply** |

On the **Networking** tab leave public access on. Create the account (takes 5-10 minutes).

Once it exists:

1. **Data Explorer > New Container**
   - Database id: `tododb` (create new)
   - Container id: `todos`
   - Partition key: `/id`
   - Container throughput: Manual, **400 RU/s**
2. **Settings > Keys**, then disable key auth to match the template:
   there is no portal toggle, so run
   `az cosmosdb update --name <account> --resource-group rg-todo-app --disable-local-auth true`.
   Skip this if you prefer key-based auth, but then you must store the key in App Service
   configuration rather than relying on managed identity.

## 3. App Service

**Create a resource > Web App**

| Setting | Value |
| --- | --- |
| Resource group | `rg-todo-app` |
| Name | globally unique, e.g. `app-mytodo-01` |
| Publish | Code |
| Runtime stack | .NET 10 (LTS) |
| Operating System | Linux |
| Region | same as Cosmos DB |
| Pricing plan | **Free F1** |

> The F1 tier is only offered when you create a new App Service plan. If the dropdown
> hides it, click **Explore pricing plans** and choose Free F1 under *Dev/Test*.

## 4. Managed identity

**Your web app > Settings > Identity > System assigned > Status: On**, then Save. Copy
the **Object (principal) ID**.

## 5. Grant the identity access to Cosmos data

The portal cannot assign Cosmos DB data-plane roles, so run this once:

```bash
az cosmosdb sql role assignment create \
  --account-name <cosmos-account-name> \
  --resource-group rg-todo-app \
  --role-definition-id 00000000-0000-0000-0000-000000000002 \
  --principal-id <principal-id-from-step-4> \
  --scope "/"
```

`...0002` is the built-in **Cosmos DB Built-in Data Contributor** role: read/write on
documents, no control-plane permissions.

Without this step the app starts but every request to `/api/todos` returns a 403.

## 6. App settings

**Your web app > Settings > Environment variables > App settings**, add:

| Name | Value |
| --- | --- |
| `Todo__Provider` | `Cosmos` |
| `Cosmos__AccountEndpoint` | `https://<cosmos-account-name>.documents.azure.com:443/` |
| `Cosmos__DatabaseName` | `tododb` |
| `Cosmos__ContainerName` | `todos` |
| `ASPNETCORE_ENVIRONMENT` | `Production` |

The double underscore is how ASP.NET Core maps a flat environment variable onto nested
configuration (`Cosmos:AccountEndpoint`). Save and let the app restart.

## 7. Verify

Browse to `https://<app-name>.azurewebsites.net/healthz`. The first request cold-starts
the app, because F1 does not support Always On, so give it up to 30 seconds.
