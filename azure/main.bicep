@description('Location for all resources')
param location string = resourceGroup().location

@description('Application name prefix')
param appName string = 'comments'

@description('Container image tag to deploy')
param imageTag string = 'latest'

@description('GitHub Container Registry image (API)')
param apiImage string = 'ghcr.io/youruser/comments-api:${imageTag}'

@description('GitHub Container Registry image (Web)')
param webImage string = 'ghcr.io/youruser/comments-web:${imageTag}'

@secure()
@description('PostgreSQL administrator password')
param postgresPassword string

@secure()
@description('Redis access key')
param redisKey string = ''

// ── Log Analytics ────────────────────────────────────────────────────────────
resource logAnalytics 'Microsoft.OperationalInsights/workspaces@2022-10-01' = {
  name: '${appName}-logs'
  location: location
  properties: {
    sku: { name: 'PerGB2018' }
    retentionInDays: 30
  }
}

// ── Container Apps Environment ───────────────────────────────────────────────
resource caEnv 'Microsoft.App/managedEnvironments@2023-05-01' = {
  name: '${appName}-env'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: logAnalytics.properties.customerId
        sharedKey: logAnalytics.listKeys().primarySharedKey
      }
    }
  }
}

// ── Azure Database for PostgreSQL – Flexible Server ──────────────────────────
resource postgres 'Microsoft.DBforPostgreSQL/flexibleServers@2023-03-01-preview' = {
  name: '${appName}-postgres'
  location: location
  sku: { name: 'Standard_B1ms', tier: 'Burstable' }
  properties: {
    administratorLogin: 'postgres'
    administratorLoginPassword: postgresPassword
    version: '17'
    storage: { storageSizeGB: 32 }
    backup: { backupRetentionDays: 7, geoRedundantBackup: 'Disabled' }
  }
}

resource postgresDb 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2023-03-01-preview' = {
  parent: postgres
  name: 'comments_app'
}

// ── Azure Cache for Redis ─────────────────────────────────────────────────────
resource redisCache 'Microsoft.Cache/redis@2023-08-01' = {
  name: '${appName}-redis'
  location: location
  properties: {
    sku: { name: 'Basic', family: 'C', capacity: 0 }
    enableNonSslPort: false
    minimumTlsVersion: '1.2'
  }
}

// ── API Container App ─────────────────────────────────────────────────────────
resource apiApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: '${appName}-api'
  location: location
  properties: {
    managedEnvironmentId: caEnv.id
    configuration: {
      ingress: {
        external: true
        targetPort: 8080
        transport: 'http'
      }
    }
    template: {
      containers: [
        {
          name: 'api'
          image: apiImage
          env: [
            { name: 'ASPNETCORE_URLS', value: 'http://+:8080' }
            { name: 'ConnectionStrings__DefaultConnection', value: 'Host=${postgres.properties.fullyQualifiedDomainName};Port=5432;Database=comments_app;Username=postgres;Password=${postgresPassword};SslMode=Require' }
            { name: 'ConnectionStrings__Redis', value: '${redisCache.properties.hostName}:6380,password=${redisKey},ssl=True,abortConnect=False' }
            { name: 'RabbitMQ__Host', value: 'YOUR_RABBITMQ_HOST' }
            { name: 'Elasticsearch__Url', value: 'https://YOUR_ELASTIC_CLOUD_URL' }
            { name: 'Cors__AllowedOrigin', value: 'https://${appName}-web.${caEnv.properties.defaultDomain}' }
          ]
          resources: { cpu: json('0.5'), memory: '1.0Gi' }
        }
      ]
      scale: { minReplicas: 1, maxReplicas: 3 }
    }
  }
}

// ── Web Container App ─────────────────────────────────────────────────────────
resource webApp 'Microsoft.App/containerApps@2023-05-01' = {
  name: '${appName}-web'
  location: location
  properties: {
    managedEnvironmentId: caEnv.id
    configuration: {
      ingress: {
        external: true
        targetPort: 4200
        transport: 'http'
      }
    }
    template: {
      containers: [
        {
          name: 'web'
          image: webImage
          resources: { cpu: json('0.25'), memory: '0.5Gi' }
        }
      ]
      scale: { minReplicas: 1, maxReplicas: 2 }
    }
  }
}

// ── Outputs ───────────────────────────────────────────────────────────────────
output apiUrl string = 'https://${apiApp.properties.configuration.ingress.fqdn}'
output webUrl string = 'https://${webApp.properties.configuration.ingress.fqdn}'
output postgresHost string = postgres.properties.fullyQualifiedDomainName
output redisHost string = redisCache.properties.hostName
