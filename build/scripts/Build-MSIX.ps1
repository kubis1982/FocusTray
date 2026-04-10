# Build-MSIX.ps1
# Builds a signed MSIX package for FocusTray
#
# Usage:
#   .\Build-MSIX.ps1                                        # Build with self-signed cert
#   .\Build-MSIX.ps1 -OutputPath "C:\Release"              # Custom output directory

param(
    [Parameter(Mandatory=$false)]
    [ValidateSet("Debug", "Release")]
    [string]$Configuration = "Release",
    
    [Parameter(Mandatory=$false)]
    [string]$OutputPath = (Join-Path $PSScriptRoot "..\..\dist\msix"),
    
    [switch]$NoClean
)

$ErrorActionPreference = "Stop"

Write-Host @"
╔════════════════════════════════════════════════════════════════════╗
║        Building FocusTray MSIX Package                             ║
╚════════════════════════════════════════════════════════════════════╝
"@

$projectRoot = Join-Path $PSScriptRoot "..\..\."
Set-Location $projectRoot

# Determine certificate to use
$certPath = Join-Path $projectRoot "build\certs\FocusTray-SignKey.pfx"
$certPass = Read-Host "Enter certificate password" -AsSecureString

if (-not (Test-Path $certPath)) {
    Write-Error "Certificate not found: $certPath`n`nRun first: .\build\scripts\Create-SelfSignedCert.ps1"
    exit 1
}

Write-Host "`n📋 Build Configuration:"
Write-Host "   Configuration: $Configuration"
Write-Host "   Output Path: $OutputPath"

# Validate required assets exist
Write-Host "`n🔍 Validating required assets..."
$requiredAssets = @(
    "build\msix\AppxManifest.xml",
    "build\msix\Assets\Square150x150Logo.png",
    "build\msix\Assets\Square44x44Logo.png", 
    "build\msix\Assets\StoreLogo.png",
    "build\msix\Assets\SplashScreen.png"
)

foreach ($asset in $requiredAssets) {
    if (-not (Test-Path $asset)) {
        Write-Error "Required asset not found: $asset"
        exit 1
    }
    Write-Host "   ✅ Found: $asset"
}

# Clean previous build
if (-not $NoClean) {
    Write-Host "`n🧹 Cleaning previous builds..."
    $binPaths = @(
        "src/FocusTray/bin/$Configuration",
        "src/FocusTray/obj",
        $OutputPath,
        "dist/temp"
    )
    
    foreach ($path in $binPaths) {
        if (Test-Path $path) {
            Remove-Item $path -Recurse -Force | Out-Null
            Write-Host "   Removed: $path"
        }
    }
}

# Build and publish
Write-Host "`n🔨 Publishing application..."
$publishArgs = @(
    "publish",
    "src/FocusTray/FocusTray.csproj",
    "-c", $Configuration,
    "-r", "win-x64",
    "-p:SelfContained=true",
    "-p:PublishSingleFile=true",
    "-p:IncludeNativeLibrariesForSelfExtract=true",
    "-p:EnableCompressionInSingleFile=true",
    "-p:PublishDir=$OutputPath"
)

try {
    & dotnet @publishArgs
    if ($LASTEXITCODE -ne 0) {
        Write-Error "Build failed with exit code: $LASTEXITCODE"
        exit 1
    }
}
catch {
    Write-Error "Failed to publish: $_"
    exit 1
}

Write-Host "✅ Application published"

# Create MSIX package structure
Write-Host "`n📦 Creating MSIX package structure..."

$appPackageDir = Join-Path $projectRoot "dist\temp\AppPackage"
if (-not (Test-Path $appPackageDir)) {
    New-Item -ItemType Directory -Path $appPackageDir | Out-Null
}

# Copy application files
Write-Host "   Copying application files..."
Copy-Item "$OutputPath\*" $appPackageDir -Recurse -Force | Out-Null

# Copy manifest
Write-Host "   Copying AppxManifest.xml..."
Copy-Item "build\msix\AppxManifest.xml" $appPackageDir -Force | Out-Null

# Create Assets directory and copy
$assetsDir = Join-Path $appPackageDir "Assets"
if (-not (Test-Path $assetsDir)) {
    New-Item -ItemType Directory -Path $assetsDir | Out-Null
}
Copy-Item "build\msix\Assets\*.png" $assetsDir -Force | Out-Null

# Create MSIX using MakeAppx
Write-Host "`n📦 Creating MSIX package..."

# Ensure output directory exists for the MSIX file
$msixOutputDir = Join-Path $projectRoot "dist"
if (-not (Test-Path $msixOutputDir)) {
    New-Item -ItemType Directory -Path $msixOutputDir | Out-Null
}

$msixPath = Join-Path $msixOutputDir "FocusTray.msix"

# Find MakeAppx tool
$makeAppxPaths = @(
    "C:\Program Files (x86)\Windows Kits\10\bin\*\x64\MakeAppx.exe",
    "C:\Program Files (x86)\Windows Kits\11\bin\*\x64\MakeAppx.exe"
)

$makeAppx = $null
foreach ($pattern in $makeAppxPaths) {
    $found = @(Get-Item $pattern -ErrorAction SilentlyContinue | Sort-Object -Descending)
    if ($found.Count -gt 0) {
        $makeAppx = $found[0].FullName
        break
    }
}

if (-not $makeAppx) {
    Write-Error "MakeAppx tool not found. Install Windows SDK or Visual Studio Build Tools."
    exit 1
}

Write-Host "   Using: $makeAppx"
Write-Host "   Package directory: $appPackageDir"
Write-Host "   Output MSIX: $msixPath"

try {
    Write-Host "   Running MakeAppx..."
    & $makeAppx pack /d $appPackageDir /p $msixPath /l
    
    if ($LASTEXITCODE -ne 0) {
        Write-Error "MakeAppx failed with exit code: $LASTEXITCODE"
        exit 1
    }
    
    if (Test-Path $msixPath) {
        Write-Host "✅ MSIX package created: $msixPath"
        $fileInfo = Get-Item $msixPath
        Write-Host "   Size: $([math]::Round($fileInfo.Length / 1MB, 2)) MB"
    } else {
        Write-Error "MSIX file was not created at expected location: $msixPath"
        exit 1
    }
}
catch {
    Write-Error "Failed to create MSIX: $_"
    exit 1
}

# Sign MSIX if enabled
Write-Host "`n🔏 Signing MSIX package..."

$signTool = "C:\Program Files (x86)\Windows Kits\10\bin\*\x64\SignTool.exe"
$signToolPath = @(Get-Item $signTool -ErrorAction SilentlyContinue | Sort-Object -Descending)

if ($signToolPath.Count -eq 0) {
    Write-Error "SignTool not found. Install Windows SDK."
    exit 1
}

$signTool = $signToolPath[0].FullName

try {
    & $signTool sign /fd SHA256 /f $certPath /p $certPass /t http://timestamp.digicert.com $msixPath
    Write-Host "✅ MSIX signed successfully"
}
catch {
    Write-Error "Failed to sign MSIX: $_"
    exit 1
}

# Cleanup
Write-Host "`n🧹 Cleaning up..."
Remove-Item (Join-Path $projectRoot "dist\temp") -Recurse -Force -ErrorAction SilentlyContinue | Out-Null

# Summary
$fileSize = (Get-Item $msixPath).Length / 1MB
Write-Host @"
`n✨ Build Complete!
═══════════════════════════════════════════════════════════════
    📦 MSIX Package: $msixPath
    📊 Size: $([math]::Round($fileSize, 2)) MB
═══════════════════════════════════════════════════════════════

"@

exit 0
