# Create-SelfSignedCert.ps1
# Generates a self-signed certificate for testing MSIX package signing
#
# Usage:
#   .\Create-SelfSignedCert.ps1                           # Interactive password prompt
#   .\Create-SelfSignedCert.ps1 -Password "MyPassword"    # Use provided password

param(
    [string]$Password,
    [string]$OutputPath = (Join-Path $PSScriptRoot "..\certs\FocusTray-SignKey.pfx"),
    [int]$ValidityMonths = 12
)

$ErrorActionPreference = "Stop"

# Prompt for password if not provided
if (-not $Password) {
    $securePassword = Read-Host "Enter password for certificate" -AsSecureString
    $Password = [System.Runtime.InteropServices.Marshal]::PtrToStringAuto(
        [System.Runtime.InteropServices.Marshal]::SecureStringToCoTaskMemUnicode($securePassword)
    )
}

Write-Host "🔐 Generating self-signed certificate for MSIX signing..."

# Certificate parameters
$params = @{
    Subject = "CN=Kubis1982.FocusTray"
    FriendlyName = "FocusTray Self-Signed Testing Certificate"
    CertStoreLocation = "Cert:\CurrentUser\My"
    KeyExportPolicy = "Exportable"
    KeyLength = 2048
    KeyUsage = "DigitalSignature"
    NotAfter = (Get-Date).AddMonths($ValidityMonths)
    Type = "CodeSigningCert"
    HashAlgorithm = "SHA256"
}

try {
    # Create self-signed certificate
    $cert = New-SelfSignedCertificate @params
    Write-Host "✅ Certificate created: $($cert.Thumbprint)"
    
    # Export to PFX
    $securePass = ConvertTo-SecureString -String $Password -AsPlainText -Force
    
    Export-PfxCertificate -Cert "Cert:\CurrentUser\My\$($cert.Thumbprint)" `
                          -FilePath $OutputPath `
                          -Password $securePass `
                          -Force | Out-Null
    
    Write-Host "✅ Certificate exported to: $OutputPath"
    
    # Store password in environment variable (PowerShell session only)
    $env:FOCUSTRAY_CERT_PASSWORD = $Password
    Write-Host "✅ Password stored in `$env:FOCUSTRAY_CERT_PASSWORD (session only)"
    
    # Display certificate info
    $cert = Get-ChildItem "Cert:\CurrentUser\My\$($cert.Thumbprint)"
    Write-Host "`n📋 Certificate Details:"
    Write-Host "   Subject: $($cert.Subject)"
    Write-Host "   Thumbprint: $($cert.Thumbprint)"
    Write-Host "   Valid Until: $($cert.NotAfter)"
    
    Write-Host "`n⚠️  To trust this certificate for MSIX installation:"
    Write-Host "   1. Enable Windows Developer Mode (Settings > For developers)"
    Write-Host "   2. Run: Import-PfxCertificate -FilePath '$OutputPath' -CertStoreLocation Cert:\LocalMachine\Root -Password (ConvertTo-SecureString '$Password' -AsPlainText -Force)"
    Write-Host "   3. Restart Explorer or reboot"
    
}
catch {
    Write-Error "Failed to create certificate: $_"
    exit 1
}

Write-Host "`n✨ Self-signed certificate ready for MSIX testing!"
