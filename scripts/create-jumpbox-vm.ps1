# create-jumpbox-vm.ps1
# Creates a small Windows VM in the Foundry RAG VNet for private endpoint access.
# Run from local machine with Azure CLI installed and logged in.
# Usage: .\create-jumpbox-vm.ps1

$ErrorActionPreference = "Stop"

# --- Configuration ---
$resourceGroup    = "rp-foundry-project-rg"
$location         = "centralus"
$vmName           = "vm-foundry-rag"
$vmSize           = "Standard_B1s"
$vnetName         = "vnet-foundry-rag-centralus"
$subnetName       = "default"
$adminUser        = "azureuser"
$bastionName      = "bastion-foundry-rag"
$bastionSubnet    = "AzureBastionSubnet"
$bastionPipName   = "pip-bastion-foundry-rag"

# --- Prompt for VM password ---
$adminPassword = Read-Host -Prompt "Enter VM admin password" -AsSecureString
$adminPasswordPlain = [Runtime.InteropServices.Marshal]::PtrToStringAuto(
    [Runtime.InteropServices.Marshal]::SecureStringToBSTR($adminPassword)
)

Write-Host "`n=== Step 1/4: Creating AzureBastionSubnet ===" -ForegroundColor Cyan
# Bastion requires a dedicated subnet named exactly "AzureBastionSubnet" with /26 or larger
az network vnet subnet create `
    --resource-group $resourceGroup `
    --vnet-name $vnetName `
    --name $bastionSubnet `
    --address-prefixes "10.0.1.0/26"

Write-Host "`n=== Step 2/4: Creating VM (no public IP) ===" -ForegroundColor Cyan
az vm create `
    --resource-group $resourceGroup `
    --name $vmName `
    --image "MicrosoftWindowsServer:WindowsServer:2022-datacenter-azure-edition-smalldisk:latest" `
    --size $vmSize `
    --vnet-name $vnetName `
    --subnet $subnetName `
    --admin-username $adminUser `
    --admin-password $adminPasswordPlain `
    --public-ip-address "" `
    --nsg "" `
    --output table

Write-Host "`n=== Step 3/4: Creating Bastion public IP ===" -ForegroundColor Cyan
az network public-ip create `
    --resource-group $resourceGroup `
    --name $bastionPipName `
    --sku Standard `
    --location $location

Write-Host "`n=== Step 4/4: Creating Bastion host (Developer SKU) ===" -ForegroundColor Cyan
az network bastion create `
    --resource-group $resourceGroup `
    --name $bastionName `
    --public-ip-address $bastionPipName `
    --vnet-name $vnetName `
    --sku Developer `
    --location $location

Write-Host "`n=== Done ===" -ForegroundColor Green
Write-Host "VM:      $vmName (no public IP)"
Write-Host "Bastion: $bastionName (Developer SKU)"
Write-Host ""
Write-Host "To connect:"
Write-Host "  1. Azure Portal -> Virtual Machines -> $vmName -> Connect -> Bastion"
Write-Host "  2. Username: $adminUser"
Write-Host "  3. From the VM browser, open portal.azure.com to access storage"
Write-Host ""
Write-Host "To deallocate when done (stop charges):"
Write-Host "  az vm deallocate -g $resourceGroup -n $vmName"
Write-Host "  az network bastion delete -g $resourceGroup -n $bastionName"
