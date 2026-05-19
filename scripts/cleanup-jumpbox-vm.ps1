# cleanup-jumpbox-vm.ps1
# Deallocates/removes the jumpbox VM and Bastion to stop charges.
# Usage: .\cleanup-jumpbox-vm.ps1 [-Delete]

param(
    [switch]$Delete  # If set, deletes resources entirely. Otherwise just deallocates VM.
)

$ErrorActionPreference = "Stop"

$resourceGroup = "rp-foundry-project-rg"
$vmName        = "vm-foundry-rag"
$bastionName   = "bastion-foundry-rag"
$bastionPip    = "pip-bastion-foundry-rag"

if ($Delete) {
    Write-Host "=== Deleting Bastion ===" -ForegroundColor Yellow
    az network bastion delete -g $resourceGroup -n $bastionName --yes
    
    Write-Host "=== Deleting Bastion public IP ===" -ForegroundColor Yellow
    az network public-ip delete -g $resourceGroup -n $bastionPip

    Write-Host "=== Deleting VM and associated resources ===" -ForegroundColor Yellow
    az vm delete -g $resourceGroup -n $vmName --yes
    
    # Clean up leftover NIC and disk
    $nicName = az vm show -g $resourceGroup -n $vmName --query "networkProfile.networkInterfaces[0].id" -o tsv 2>$null
    if ($nicName) { az network nic delete --ids $nicName }
    
    Write-Host "`nAll jumpbox resources deleted." -ForegroundColor Green
} else {
    Write-Host "=== Deallocating VM (stops compute charges) ===" -ForegroundColor Cyan
    az vm deallocate -g $resourceGroup -n $vmName
    
    Write-Host "=== Deleting Bastion (no deallocate option) ===" -ForegroundColor Cyan
    az network bastion delete -g $resourceGroup -n $bastionName --yes

    Write-Host "`nVM deallocated, Bastion deleted. Re-run create script when needed." -ForegroundColor Green
}
