// Free-tier infrastructure for the todo app:
//   - Cosmos DB for NoSQL account on the free tier (1000 RU/s + 25 GB free)
//   - Linux App Service plan on F1 (Free), hosting the API and the React bundle
//   - A system-assigned identity for the web app, granted Cosmos data-plane access
//
// Key-based auth is disabled on the Cosmos account, so there is no secret to leak into
// the public GitHub repo; the app authenticates with its managed identity.

targetScope = 'resourceGroup'

@description('Base name for the resources. Must be 3-20 characters of lowercase letters and digits.')
@minLength(3)
@maxLength(20)
param appName string

@description('Azure region for all resources.')
param location string = resourceGroup().location

@description('Cosmos DB database name.')
param databaseName string = 'tododb'

@description('Cosmos DB container name.')
param containerName string = 'todos'

@description('''
Request the Cosmos DB free tier. Only one free-tier account is allowed per subscription,
so set this to false if you already have one or deployment will fail.
''')
param useCosmosFreeTier bool = true

@description('Throughput for the container, in RU/s. 400 fits inside the free-tier allowance.')
@minValue(400)
@maxValue(1000)
param containerThroughput int = 400

var uniqueSuffix = uniqueString(resourceGroup().id, appName)
var cosmosAccountName = toLower('cosmos-${appName}-${uniqueSuffix}')
var planName = 'plan-${appName}'
var webAppName = 'app-${appName}-${uniqueSuffix}'

// Built-in "Cosmos DB Built-in Data Contributor" role: read/write on data, no control plane.
var cosmosDataContributorRoleId = '00000000-0000-0000-0000-000000000002'

resource cosmosAccount 'Microsoft.DocumentDB/databaseAccounts@2024-11-15' = {
  name: cosmosAccountName
  location: location
  kind: 'GlobalDocumentDB'
  properties: {
    databaseAccountOfferType: 'Standard'
    enableFreeTier: useCosmosFreeTier
    // Managed identity only: no account keys are ever valid, so none can be committed.
    disableLocalAuth: true
    minimalTlsVersion: 'Tls12'
    consistencyPolicy: {
      defaultConsistencyLevel: 'Session'
    }
    locations: [
      {
        locationName: location
        failoverPriority: 0
        isZoneRedundant: false
      }
    ]
  }
}

resource cosmosDatabase 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases@2024-11-15' = {
  parent: cosmosAccount
  name: databaseName
  properties: {
    resource: {
      id: databaseName
    }
  }
}

resource cosmosContainer 'Microsoft.DocumentDB/databaseAccounts/sqlDatabases/containers@2024-11-15' = {
  parent: cosmosDatabase
  name: containerName
  properties: {
    resource: {
      id: containerName
      // Partitioning on /id keeps every read and write a single-partition operation.
      partitionKey: {
        paths: [
          '/id'
        ]
        kind: 'Hash'
      }
      indexingPolicy: {
        indexingMode: 'consistent'
        automatic: true
        includedPaths: [
          {
            path: '/createdAt/?'
          }
          {
            path: '/isComplete/?'
          }
        ]
        excludedPaths: [
          {
            path: '/*'
          }
        ]
      }
    }
    options: {
      throughput: containerThroughput
    }
  }
}

resource appServicePlan 'Microsoft.Web/serverfarms@2024-04-01' = {
  name: planName
  location: location
  sku: {
    name: 'F1'
    tier: 'Free'
  }
  kind: 'linux'
  properties: {
    reserved: true // required for Linux plans
  }
}

resource webApp 'Microsoft.Web/sites@2024-04-01' = {
  name: webAppName
  location: location
  identity: {
    type: 'SystemAssigned'
  }
  properties: {
    serverFarmId: appServicePlan.id
    httpsOnly: true
    siteConfig: {
      linuxFxVersion: 'DOTNETCORE|10.0'
      ftpsState: 'FtpsOnly'
      minTlsVersion: '1.2'
      http20Enabled: true
      // alwaysOn is not available on the Free (F1) tier and must stay false.
      alwaysOn: false
      appSettings: [
        {
          name: 'Todo__Provider'
          value: 'Cosmos'
        }
        {
          name: 'Cosmos__AccountEndpoint'
          value: cosmosAccount.properties.documentEndpoint
        }
        {
          name: 'Cosmos__DatabaseName'
          value: databaseName
        }
        {
          name: 'Cosmos__ContainerName'
          value: containerName
        }
        {
          name: 'ASPNETCORE_ENVIRONMENT'
          value: 'Production'
        }
      ]
    }
  }
}

// Lets the web app's managed identity read and write documents. Without this the app
// starts but every Cosmos call returns 403.
resource cosmosDataAccess 'Microsoft.DocumentDB/databaseAccounts/sqlRoleAssignments@2024-11-15' = {
  parent: cosmosAccount
  name: guid(cosmosAccount.id, webApp.id, cosmosDataContributorRoleId)
  properties: {
    principalId: webApp.identity.principalId
    roleDefinitionId: '${cosmosAccount.id}/sqlRoleDefinitions/${cosmosDataContributorRoleId}'
    scope: cosmosAccount.id
  }
}

@description('Name of the created web app, needed by the deployment pipeline.')
output webAppName string = webApp.name

@description('Public URL of the deployed app.')
output webAppUrl string = 'https://${webApp.properties.defaultHostName}'

@description('Cosmos DB account name.')
output cosmosAccountName string = cosmosAccount.name

@description('Cosmos DB endpoint configured on the web app.')
output cosmosEndpoint string = cosmosAccount.properties.documentEndpoint
