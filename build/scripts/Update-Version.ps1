# Update-Version.ps1
# Updates version in csproj and AppxManifest.xml, optionally creates Git tag
#
# Usage:
#   .\Update-Version.ps1 -Version 0.2.0                    # Update to 0.2.0
#   .\Update-Version.ps1 -Version 0.2.0 -GitTag            # Also create Git tag

param(
    [Parameter(Mandatory=$true)]
    [string]$Version,
    
    [switch]$GitTag
)

$ErrorActionPreference = "Stop"

# Validate version format
if ($Version -notmatch '^\d+\.\d+\.\d+$') {
    throw "Invalid version format: $Version (expected: X.X.X)"
}

Write-Host "📝 Updating version to $Version"

# Update csproj
$csprojPath = Join-Path $PSScriptRoot "..\..\src\FocusTray\FocusTray.csproj"
[xml]$csproj = Get-Content $csprojPath

$csproj.Project.PropertyGroup | 
    Where-Object { $_.Version } | 
    ForEach-Object { $_.Version = $Version }

$csproj.Save($csprojPath)
Write-Host "✅ Updated $csprojPath"

# Update AppxManifest
$manifestPath = Join-Path $PSScriptRoot "..\..\build\msix\AppxManifest.xml"
[xml]$manifest = Get-Content $manifestPath

$msixVersion = "$Version.0"
$manifest.Package.Identity.Version = $msixVersion

$manifest.Save($manifestPath)
Write-Host "✅ Updated $manifestPath to version $msixVersion"

# Create Git tag if requested
if ($GitTag) {
    $tagName = "v$Version"
    try {
        git tag $tagName
        Write-Host "✅ Created Git tag: $tagName"
    }
    catch {
        Write-Warning "Could not create Git tag (may already exist): $tagName"
    }
}

Write-Host "✨ Version updated to $Version"
