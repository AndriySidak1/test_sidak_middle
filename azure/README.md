# Azure Deployment

Infrastructure is defined as code using **Azure Bicep** (`main.bicep`).

## Resources provisioned

| Resource | Azure Service |
|----------|--------------|
| API | Azure Container Apps |
| Frontend | Azure Container Apps |
| PostgreSQL | Azure Database for PostgreSQL – Flexible Server (v17) |
| Redis | Azure Cache for Redis (Basic C0) |
| RabbitMQ | CloudAMQP (external) or Azure Container Apps sidecar |
| Elasticsearch | Elastic Cloud on Azure |
| Logs | Azure Log Analytics Workspace |

## Deploy with Azure CLI

```bash
# 1. Login
az login

# 2. Create resource group
az group create --name comments-rg --location westeurope

# 3. Deploy infrastructure
az deployment group create \
  --resource-group comments-rg \
  --template-file azure/main.bicep \
  --parameters postgresPassword='<STRONG_PASSWORD>'

# 4. Get outputs
az deployment group show \
  --resource-group comments-rg \
  --name main \
  --query properties.outputs
```

## CI/CD (GitHub Actions)

Required GitHub Secrets:

| Secret | Description |
|--------|-------------|
| `AZURE_CREDENTIALS` | Output of `az ad sp create-for-rbac --sdk-auth` |
| `AZURE_RESOURCE_GROUP` | e.g. `comments-rg` |

Workflow files:
- `.github/workflows/ci.yml` — runs on every push/PR (build + Docker build validation)
- `.github/workflows/deploy-azure.yml` — runs on push to `main` (build → GHCR push → Azure deploy)
