# reset-search.ps1
# Deletes the AI Search index, indexer, skillset, and data source created by the Import Data wizard.
# This allows you to re-run the wizard from scratch.
# Uses the Azure Search REST API with az account get-access-token for auth.
# Usage: .\scripts\reset-search.ps1

$ErrorActionPreference = "Stop"

$searchService = "rp-search-foundry-rag"
$prefix        = "rp-foundry-rag"
$apiVersion    = "2024-07-01"
$baseUrl       = "https://$searchService.search.windows.net"

$indexName      = "$prefix"
$indexerName    = "$prefix-indexer"
$skillsetName   = "$prefix-skillset"
$datasourceName = "$prefix-datasource"

# Get access token for Azure Search
$token = (az account get-access-token --resource "https://search.azure.com" --query "accessToken" -o tsv)
$headers = @{
    Authorization = "Bearer $token"
    'Content-Type' = 'application/json'
}

function Delete-SearchResource($type, $name) {
    Write-Host "Deleting $type`: $name" -ForegroundColor Yellow
    $uri = "$baseUrl/$type/$($name)?api-version=$apiVersion"
    try {
        Invoke-RestMethod -Method Delete -Uri $uri -Headers $headers | Out-Null
        Write-Host "  Done." -ForegroundColor Green
    } catch {
        if ($_.Exception.Response.StatusCode -eq 404) {
            Write-Host "  Not found (skipping)." -ForegroundColor DarkGray
        } else {
            Write-Host "  Error: $_" -ForegroundColor Red
        }
    }
}

Write-Host "Resetting AI Search resources with prefix '$prefix'..." -ForegroundColor Cyan
Write-Host ""

Delete-SearchResource "indexers" $indexerName
Delete-SearchResource "skillsets" $skillsetName
Delete-SearchResource "indexes" $indexName
Delete-SearchResource "datasources" $datasourceName

Write-Host ""
Write-Host "All search resources deleted. You can now re-run the Import Data wizard." -ForegroundColor Cyan
