# WinGet Manifest Templates

This directory contains template manifests for submitting FocusTray to the Windows Package Manager (WinGet) repository.

## Files

- **Kubis1982.FocusTray.yaml** - Version manifest (main entry point)
- **Kubis1982.FocusTray.installer.yaml** - Installer configuration (portable type)
- **Kubis1982.FocusTray.locale.en-US.yaml** - English (US) localization
- **Kubis1982.FocusTray.locale.pl-PL.yaml** - Polish localization

## Usage

### Before Creating a Release

These manifests serve as templates. Before each GitHub Release:

1. **Update version** in all manifest files:
   ```yaml
   PackageVersion: X.Y.Z
   ```

2. **Update InstallerUrl** in `Kubis1982.FocusTray.installer.yaml`:
   ```yaml
   InstallerUrl: https://github.com/kubis1982/FocusTray/releases/download/vX.Y.Z/FocusTray-vX.Y.Z-win-x64.exe
   ```

3. **DO NOT update SHA256** yet - this will be available after GitHub Release is created

### After GitHub Release is Created

1. Get the SHA256 hash from the GitHub Release notes
2. Update `InstallerSha256` in `Kubis1982.FocusTray.installer.yaml`:
   ```yaml
   InstallerSha256: <actual-hash-from-release>
   ```

### Local Testing

Test the manifests locally before submitting to WinGet:

```bash
# Validate manifest syntax
winget validate --manifest .winget\

# Test installation locally
winget install --manifest .winget\

# Verify installation
focustray --help
```

### Submitting to WinGet

#### First Submission

1. Fork https://github.com/microsoft/winget-pkgs
2. Create directory structure:
   ```
   manifests/k/Kubis1982/FocusTray/X.Y.Z/
   ```
3. Copy all manifest files to this directory
4. Validate manifests:
   ```bash
   winget validate --manifest manifests/k/Kubis1982/FocusTray/X.Y.Z/
   ```
5. Commit and create Pull Request:
   ```bash
   git add manifests/k/Kubis1982/FocusTray/
   git commit -m "New package: Kubis1982.FocusTray version X.Y.Z"
   git push origin main
   ```
6. Create PR with title: "New package: Kubis1982.FocusTray version X.Y.Z"

#### Subsequent Releases

Use `wingetcreate` for easier updates:

```bash
# Install wingetcreate
winget install Microsoft.WingetCreate

# Update package
wingetcreate update Kubis1982.FocusTray --version X.Y.Z --urls https://github.com/kubis1982/FocusTray/releases/download/vX.Y.Z/FocusTray-vX.Y.Z-win-x64.exe --submit
```

The `--submit` flag will automatically:
- Update version
- Calculate new SHA256 hash
- Create PR to winget-pkgs

## Manifest Schema

All manifests use WinGet schema version `1.12.0`.

### Key Fields

**Version Manifest (`*.yaml`):**
- `PackageIdentifier`: Unique package ID (Publisher.PackageName)
- `PackageVersion`: Semantic version (X.Y.Z)
- `DefaultLocale`: Default language code
- `ManifestType`: Always `version`

**Installer Manifest (`*.installer.yaml`):**
- `InstallerType`: `portable` (no installation, direct execution)
- `Architecture`: `x64` (win-x64)
- `InstallerUrl`: Direct download link to GitHub Release
- `InstallerSha256`: SHA256 checksum for integrity verification
- `PortableCommandAlias`: Command name for CLI access (`focustray`)
- `MinimumOSVersion`: `10.0.19041.0` (Windows 10 build 19041)

**Locale Manifest (`*.locale.*.yaml`):**
- `PackageLocale`: Language code (e.g., `en-US`, `pl-PL`)
- `Publisher`, `PackageName`: Display names
- `ShortDescription`: One-line description
- `Description`: Detailed multi-line description
- `Tags`: Searchable keywords

## Resources

- [WinGet CLI Repository](https://github.com/microsoft/winget-cli)
- [WinGet Packages Repository](https://github.com/microsoft/winget-pkgs)
- [Manifest Schema Documentation](https://github.com/microsoft/winget-pkgs/tree/master/doc/manifest/schema/1.12.0)
- [Contribution Guidelines](https://github.com/microsoft/winget-pkgs/blob/master/CONTRIBUTING.md)
- [WinGetCreate Tool](https://github.com/microsoft/winget-create)

## Troubleshooting

### Validation Errors

**YAML syntax errors:**
- Use spaces, not tabs for indentation
- Ensure consistent 2-space indentation
- Check for trailing spaces

**Required field missing:**
- All manifests must have `ManifestVersion: 1.12.0`
- Installer manifest requires `InstallerSha256` (no placeholders in submission)

### Hash Mismatch

If WinGet reports hash mismatch during installation:

1. Download the release asset
2. Calculate correct hash:
   ```powershell
   Get-FileHash -Path FocusTray-vX.Y.Z-win-x64.exe -Algorithm SHA256
   ```
3. Update `InstallerSha256` in installer manifest
4. Re-validate and re-test

### Portable Command Alias Not Working

If `focustray` command doesn't work after installation:

1. Check that WinGet Links directory is in PATH:
   ```
   %LOCALAPPDATA%\Microsoft\WinGet\Links
   ```
2. Restart terminal/PowerShell to reload PATH
3. Verify installation:
   ```bash
   winget list Kubis1982.FocusTray
   ```

## Notes

- **First submission**: Expect 1-3 days for review by WinGet maintainers
- **Subsequent updates**: Often automated with bots, faster approval
- **Breaking changes**: Document clearly in release notes
- **Portable installer**: Application is copied to WinGet managed directory, not system-wide installation
- **Updates**: Users must run `winget upgrade FocusTray` manually, no auto-update
