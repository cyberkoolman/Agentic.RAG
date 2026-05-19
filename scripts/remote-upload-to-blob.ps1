# remote-upload-to-blob.ps1
# Runs on the jumpbox VM via "az vm run-command" to upload a PDF to blob storage.
# Uses the VM's managed identity for authentication.
# Usage (from local machine):
#   az vm run-command invoke -g rp-foundry-project-rg -n vm-foundry-rag --command-id RunPowerShellScript --scripts @scripts/remote-upload-to-blob.ps1

$ErrorActionPreference = "Stop"

$storageAccount = "stfoundryrag"
$containerName  = "rag-documents"
$blobName       = "www-sec-gov-nvda-20260125.pdf"
$sourceUrl      = "https://raw.githubusercontent.com/cyberkoolman/Agentic.RAG/foundry-poc/data/www-sec-gov-nvda-20260125.pdf"
$tempFile       = "C:\temp\$blobName"

# Download PDF from GitHub
New-Item -Path C:\temp -ItemType Directory -Force | Out-Null
Write-Output "Downloading $blobName from GitHub..."
Invoke-WebRequest -Uri $sourceUrl -OutFile $tempFile
Write-Output "Downloaded: $((Get-Item $tempFile).Length) bytes"

# Get managed identity token for Azure Storage
Write-Output "Acquiring managed identity token..."
$tokenResponse = Invoke-RestMethod -Uri 'http://169.254.169.254/metadata/identity/oauth2/token?api-version=2018-02-01&resource=https://storage.azure.com/' -Headers @{Metadata='true'}
$token = $tokenResponse.access_token

# Upload to blob storage
Write-Output "Uploading to $storageAccount/$containerName/$blobName..."
$uploadHeaders = @{
    Authorization    = "Bearer $token"
    'x-ms-blob-type' = 'BlockBlob'
    'x-ms-version'   = '2020-10-02'
    'Content-Type'   = 'application/pdf'
}
$blobUri = "https://$storageAccount.blob.core.windows.net/$containerName/$blobName"
Invoke-RestMethod -Method Put -Uri $blobUri -InFile $tempFile -Headers $uploadHeaders

Write-Output "Upload complete: $blobUri"

# Cleanup
Remove-Item $tempFile -Force
Write-Output "Done."
