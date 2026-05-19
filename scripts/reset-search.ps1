# reset-search.ps1
# Deletes the AI Search index, indexer, skillset, and data source created by the Import Data wizard.
# This allows you to re-run the wizard from scratch.
# Usage: .\scripts\reset-search.ps1

$ErrorActionPreference = "Stop"

$searchService = "rp-search-foundry-rag"
$resourceGroup = "rp-foundry-project-rg"
$prefix        = "rp-foundry-rag"

$indexName      = "$prefix-index"
$indexerName    = "$prefix-indexer"
$skillsetName   = "$prefix-skillset"
$datasourceName = "$prefix-datasource"

Write-Host "Resetting AI Search resources with prefix '$prefix'..." -ForegroundColor Cyan
Write-Host ""

# Delete indexer first (depends on index and skillset)
Write-Host "Deleting indexer: $indexerName" -ForegroundColor Yellow
az search indexer delete `
    --service-name $searchService `
    --resource-group $resourceGroup `
    --name $indexerName `
    2>$null
Write-Host "  Done." -ForegroundColor Green

# Delete skillset
Write-Host "Deleting skillset: $skillsetName" -ForegroundColor Yellow
az search skillset delete `
    --service-name $searchService `
    --resource-group $resourceGroup `
    --name $skillsetName `
    2>$null
Write-Host "  Done." -ForegroundColor Green

# Delete index
Write-Host "Deleting index: $indexName" -ForegroundColor Yellow
az search index delete `
    --service-name $searchService `
    --resource-group $resourceGroup `
    --name $indexName `
    2>$null
Write-Host "  Done." -ForegroundColor Green

# Delete data source
Write-Host "Deleting data source: $datasourceName" -ForegroundColor Yellow
az search datasource delete `
    --service-name $searchService `
    --resource-group $resourceGroup `
    --name $datasourceName `
    2>$null
Write-Host "  Done." -ForegroundColor Green

Write-Host ""
Write-Host "All search resources deleted. You can now re-run the Import Data wizard." -ForegroundColor Cyan
