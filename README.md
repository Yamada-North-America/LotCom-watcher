# LotCom-watcher

[![CI/CD](https://github.com/Yamada-North-America/LotCom-watcher/actions/workflows/cicd.yml/badge.svg?branch=stable)](https://github.com/Yamada-North-America/LotCom-watcher/actions/workflows/cicd.yml) [![CodeQL](https://github.com/Yamada-North-America/LotCom-watcher/actions/workflows/github-code-scanning/codeql/badge.svg)](https://github.com/Yamada-North-America/LotCom-watcher/actions/workflows/github-code-scanning/codeql)

[LotCom](https://github.com/LotCom) System dedicated `WinService` that processes scan results from YNA's fleet of production floor handheld scanners. Reads, formats, processes, and inserts new Label scans into the LotCom database.

### This is a privately-directed project

Please read the [Contribution Guide](https://github.com/Yamada-North-America/LotCom-watcher/blob/develop/CONTRIBUTING.md) for more information.

## Build a stable release

The **Build release** workflow supports manual runs and version tags. Both create a **draft GitHub Release**
with `LotComWatcher_VERSION.msi`, `LotComWatcher_VERSION_win-x64.zip`, and `SHA256SUMS.txt`.
The Windows x64 packages include the .NET runtime. Workflow artifacts are retained for 30 days.

### Manual release

1. Open **Actions → Build release → Run workflow**.
2. Enter a new three-part version without `v`, for example `1.0.7`.
3. Run the workflow, then review and publish the draft under **Releases**.

Manual runs always check out **stable**, including when the workflow is dispatched from `develop`.
The chosen version is applied to the compiled assembly, application display version, and MSI during the build.
There is no commit-message parsing and no source-version edit.

GitHub requires `workflow_dispatch` on the **default branch** to display the **Run workflow** button.
The default branch is currently `develop`: merge the release implementation into `stable` first, then
merge the companion workflow-only change into `develop`. That workflow checks out the implementation
from `stable`; it does not build the `develop` application. Keep the workflow definition synchronized
between these branches when changing the workflow in the future.

### Tag release

Create a tag on a commit that is already part of `stable` and contains this workflow:

```sh
git switch stable
git pull --ff-only
git tag v1.0.7
git push origin v1.0.7
```

Tags on commits outside `stable` are rejected. Historical numeric tags such as `1.0.7` are supported too.
Use three numeric components without leading zeroes or prerelease suffixes. MSI limits major/minor to 255
and patch to 65535. Choose a higher version for upgrades. Existing releases under either `VERSION` or
`vVERSION` are rejected, and existing tags must point to the built commit. A manual release creates
`vVERSION` at the exact built stable commit. The built-in `GITHUB_TOKEN` does not trigger a second run
when it creates that tag.

If a run fails before creating a draft, fix the problem and rerun it. If asset upload fails after draft
creation, delete that incomplete draft, keep the tag, and rerun the same commit (or choose a new version).
Published releases and existing assets are never overwritten. Repository policy must allow the release
job's `contents: write` permission.

## Build requirements and installer

The workflow retains the requirements from the previous action:

- A Windows runner, .NET 9 SDK, `net9.0-windows10.0.19041.0`, and self-contained `win-x64` publishing.
  `global.json` selects the latest installed .NET 9 feature band even when the runner also has .NET 10.
  The Worker SDK project requires NuGet targeting packs; no MAUI/WinUI workload is used.
- `LotCom-libraries` checked out as a **sibling** of `LotCom-watcher`, matching the existing project reference.
  It is pinned to `ef2da369b7bda48abc0d3b073625754dcae10bd8`, the develop revision inspected for this change.
  Update that workflow ref deliberately when updating the library. Its exact revision is recorded in release notes.
- WixSharp 2.7.1 and .NET Framework 4.7.2 reference assemblies for setup compilation; the Windows runner supplies
  the .NET Framework runtime. WiX 4.0.6 and both its **UI and Util extensions** are installed explicitly;
  the .NET 8 runtime and tool roll-forward support the WiX CLI.

`installer/Program.cs` adapts the setup from `LotCom-watcher-setup` commit
`545d153fb74be934d9c81abb0030bc16591604dd`. Keeping the adapted source here removes the original setup's
hard-coded `D:\a` paths and makes the installer version travel with the application source. The workflow
therefore no longer clones a moving setup branch or uses `PAT_1`; both repositories read by the action
are public and release writes use the repository token.

The installer retains the existing upgrade GUID, product-code algorithm, YNA metadata, application-close
behavior, and Start menu/Desktop shortcuts. It packages all published files recursively, including
`appsettings.json`, and installs under `%ProgramData%\Yamada North America\LotCom Watcher` with administrator
privileges. It uses a progress-only UI without a custom license file. As in the existing setup, it does not
register a Windows service; deployment must retain its current service registration/configuration process.
The MSI and ZIP are unsigned.

For local packaging, use PowerShell 7 on Windows, install the SDK/runtimes and WiX tools above, and clone
`LotCom-libraries` alongside this repository at the pinned revision. Run:

```powershell
dotnet tool install --global wix --version 4.0.6 --allow-roll-forward
wix extension add --global WixToolset.UI.wixext/4.0.6
wix extension add --global WixToolset.Util.wixext/4.0.6
./.github/scripts/build-release.ps1 -Version 1.0.7
```

Packages are written to `artifacts/packages/`.
