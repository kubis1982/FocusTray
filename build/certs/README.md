# Certificate Configuration for MSIX Packaging

This directory contains or will contain certificate files for signing MSIX packages.

This directory is protected by `.gitignore` to prevent accidental commits.

## Setup Instructions

### 1. Generate Self-Signed Certificate for Testing

```powershell
cd D:\GIT\GitHub\kubis1982\FocusTray

# Run the certificate generation script
.\build\scripts\Create-SelfSignedCert.ps1
```

### 2. Install Certificate for Local Testing

```powershell
# Import self-signed certificate to Trusted Root
$pfxPath = "D:\GIT\GitHub\kubis1982\FocusTray\build\certs\FocusTray-SignKey.pfx"
$cert = Import-PfxCertificate -FilePath $pfxPath `
                               -CertStoreLocation Cert:\LocalMachine\Root `
                               -Password (ConvertTo-SecureString "YourPassword" -AsPlainText -Force)

Write-Host "Certificate imported: $($cert.Thumbprint)"
```
