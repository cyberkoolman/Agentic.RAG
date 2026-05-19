# upload-to-blob.ps1
# Uploads all files from the data/ directory to the rag-documents blob container.
# Run from repo root on a machine with network access to the storage account
# (e.g., the jumpbox VM).
# Usage: .\scripts\upload-to-blob.ps1

$ErrorActionPreference = "Stop"

$storageAccount = "stfoundryrag"
$containerName  = "rag-documents"
$dataDir        = Join-Path $PSScriptRoot "..\data"

if (-not (Test-Path $dataDir)) {
    Write-Error "Data directory not found: $dataDir"
    exit 1
}

$files = Get-ChildItem -Path $dataDir -File
if ($files.Count -eq 0) {
    Write-Warning "No files found in $dataDir"
    exit 0
}

Write-Host "Uploading $($files.Count) file(s) to $storageAccount/$containerName..." -ForegroundColor Cyan

foreach ($file in $files) {
    Write-Host "  Uploading: $($file.Name)" -ForegroundColor White
    az storage blob upload `
        --account-name $storageAccount `
        --container-name $containerName `
        --file $file.FullName `
        --name $file.Name `
        --auth-mode login `
        --overwrite
}

Write-Host "`nDone. $($files.Count) file(s) uploaded." -ForegroundColor Green
