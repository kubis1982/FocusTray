# Releasing FocusTray

This document describes the release process for FocusTray.

## Prerequisites

- Write access to the repository
- All tests passing
- CHANGELOG.md updated with release notes
- Version decided (following Semantic Versioning)

## Release Process

### 1. Prepare the Release

1. Update version in `src/FocusTray/FocusTray.csproj`:
   ```xml
   <Version>X.Y.Z</Version>
   ```

2. Update CHANGELOG.md with release notes:
   ```markdown
   ## [X.Y.Z] - YYYY-MM-DD
   ### Added
   - New feature description
   ### Changed
   - Changed feature description
   ### Fixed
   - Bug fix description
   ```

3. Commit changes:
   ```bash
   git add src/FocusTray/FocusTray.csproj CHANGELOG.md
   git commit -m "chore: prepare release vX.Y.Z"
   git push origin main
   ```

### 2. Create and Push Tag

Create an annotated tag with the version number:

```bash
git tag -a vX.Y.Z -m "Release vX.Y.Z"
git push origin vX.Y.Z
```

**Important**: The tag MUST start with `v` (e.g., `v1.0.0`, not `1.0.0`) to trigger the GitHub Actions workflow.

### 3. Automated Build & Release

Once you push the tag, GitHub Actions will automatically:

1. ✅ Checkout the code
2. ✅ Setup .NET 10.0
3. ✅ Restore dependencies
4. ✅ Run all tests
5. ✅ Publish the application (Release configuration, win-x64)
6. ✅ Rename executable to `FocusTray-vX.Y.Z-win-x64.exe`
7. ✅ Calculate SHA256 hash
8. ✅ Create GitHub Release with:
   - Release notes
   - SHA256 checksum
   - Executable as downloadable asset

You can monitor the workflow at: https://github.com/kubis1982/FocusTray/actions

### 4. Update WinGet Manifest (After First Release)

After the GitHub Release is created and the SHA256 hash is available:

1. Update `.winget/Kubis1982.FocusTray.yaml` with new version
2. Update `.winget/Kubis1982.FocusTray.installer.yaml`:
   - Update `PackageVersion`
   - Update `InstallerUrl` to the new release
   - Replace `PLACEHOLDER_SHA256_HASH` with actual hash from GitHub Release
3. Test locally:
   ```bash
   winget install --manifest .winget\
   ```

#### Creating WinGet Pull Request

**For the first submission:**

1. Fork https://github.com/microsoft/winget-pkgs
2. Create directory: `manifests/k/Kubis1982/FocusTray/X.Y.Z/`
3. Copy all manifest files from `.winget/` to this directory
4. Validate manifests:
   ```bash
   winget validate --manifest manifests/k/Kubis1982/FocusTray/X.Y.Z/
   ```
5. Create Pull Request with title: "New package: Kubis1982.FocusTray version X.Y.Z"
6. Follow contribution guidelines and respond to reviewer feedback

**For subsequent releases:**

Use `wingetcreate` tool for easier updates:
```bash
wingetcreate update Kubis1982.FocusTray --version X.Y.Z --urls https://github.com/kubis1982/FocusTray/releases/download/vX.Y.Z/FocusTray-vX.Y.Z-win-x64.exe --submit
```

## Semantic Versioning

FocusTray follows [Semantic Versioning 2.0.0](https://semver.org/):

- **MAJOR** (X.0.0): Incompatible API changes or major breaking changes
- **MINOR** (0.Y.0): New functionality in a backwards-compatible manner
- **PATCH** (0.0.Z): Backwards-compatible bug fixes

Examples:
- `0.1.0` → `0.2.0`: New JIRA features added
- `0.2.0` → `0.2.1`: Bug fix in timer logic
- `0.9.0` → `1.0.0`: First stable release with breaking configuration changes

## Troubleshooting

### Build Fails in GitHub Actions

**Problem**: "dotnet: command not found" or SDK version mismatch

**Solution**: Check that `dotnet-version: '10.0.x'` in `.github/workflows/release.yml` matches the SDK version in `global.json` (if present).

### SHA256 Hash Mismatch

**Problem**: WinGet installation fails with "hash mismatch" error

**Solution**: 
1. Download the release asset from GitHub
2. Calculate hash: `Get-FileHash -Path FocusTray-vX.Y.Z-win-x64.exe -Algorithm SHA256`
3. Update `.winget/Kubis1982.FocusTray.installer.yaml` with correct hash
4. The hash in GitHub Release notes is correct - use that one

### WinGet Manifest Validation Fails

**Problem**: `winget validate` reports schema errors

**Solution**:
1. Check manifest syntax (YAML indentation, no tabs)
2. Ensure `ManifestVersion: 1.12.0` is consistent across all files
3. Verify required fields are present (PackageIdentifier, PackageVersion, InstallerUrl, InstallerSha256)
4. Review schema at: https://github.com/microsoft/winget-pkgs/tree/master/doc/manifest/schema

### Tag Already Exists

**Problem**: Cannot push tag because it already exists

**Solution**:
```bash
# Delete local tag
git tag -d vX.Y.Z
# Delete remote tag
git push origin :refs/tags/vX.Y.Z
# Recreate tag with correct commit
git tag -a vX.Y.Z -m "Release vX.Y.Z"
git push origin vX.Y.Z
```

### Release Build Size Too Large

**Problem**: FocusTray.exe is unexpectedly large (>50 MB)

**Solution**:
1. Verify `PublishReadyToRun=false` in FocusTray.csproj (reduces size, slower first start)
2. Consider `PublishTrimmed=true` (requires thorough testing)
3. Ensure `EnableCompressionInSingleFile=true` is set
4. Check that `DebugType=none` for Release configuration

## Post-Release Checklist

- [ ] GitHub Release created successfully
- [ ] Executable asset available for download
- [ ] SHA256 hash documented in release notes
- [ ] WinGet manifest updated (if not first release)
- [ ] WinGet PR created and merged (for new versions)
- [ ] Installation tested: `winget install Kubis1982.FocusTray`
- [ ] CHANGELOG.md reflects the release
- [ ] Social media announcement (optional)

## Resources

- [WinGet Package Manager](https://github.com/microsoft/winget-cli)
- [WinGet Packages Repository](https://github.com/microsoft/winget-pkgs)
- [WinGet Manifest Schema](https://github.com/microsoft/winget-pkgs/tree/master/doc/manifest/schema)
- [WinGetCreate Tool](https://github.com/microsoft/winget-create)
- [Semantic Versioning](https://semver.org/)
- [GitHub Actions Documentation](https://docs.github.com/en/actions)
