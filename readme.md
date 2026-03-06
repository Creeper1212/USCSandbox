# USCSandbox

USCSandbox is a command-line shader decompiler for Unity assets/bundles.
This README focuses on how to build and run it with working example commands.

## Requirements

- Windows x64
- .NET SDK 8.0+
- `classdata.tpk` available in the same folder as `USCSandbox.exe`

## Build (separate files, all dependencies)

Run from repo root (`G:\New folder\My AssetRipper\USCSandbox`):

```powershell
dotnet publish "USCSandbox\USCSandbox.csproj" -c Release -r win-x64 --self-contained true -p:PublishSingleFile=false -p:DebugType=None -p:DebugSymbols=false -o "publish\release_win-x64_separate"
```

Output executable:

`publish\release_win-x64_separate\USCSandbox.exe`

## Command Syntax

```powershell
USCSandbox.exe <bundlePath|null> <assetsPathOrCabName> <shaderPathId> [--platform d3d11|Switch] [--version <unityVersion>] [--all]
```

### Arguments

- `bundlePath|null`: Path to `.bundle`/`.unity3d`, or `null` when opening `.assets` directly.
- `assetsPathOrCabName`: CAB name inside bundle (example: `CAB-1234`) or `.assets` file path.
- `shaderPathId`: Target shader PathID (use `0` when using `--all`).
- `--platform`: `d3d11` (default) or `Switch`.
- `--version`: Unity version string when version metadata is stripped (example: `6000.0.1f1`).
- `--all`: Decompile every shader found in the specified file.

## Example Commands

### 1) Decompile one shader from a bundle (D3D11)

```powershell
.\USCSandbox.exe "Game.bundle" "CAB-1234" 55 --version 6000.0.1f1
```

### 2) Decompile all shaders from a direct `.assets` file

```powershell
.\USCSandbox.exe null "sharedassets0.assets" 0 --all --version 2022.3.2f1
```

### 3) Decompile one shader using Switch/NVN pipeline

```powershell
.\USCSandbox.exe "Game.bundle" "CAB-1234" 55 --platform Switch --version 2021.3.35f1
```

### 4) Run from published output folder

```powershell
cd "G:\New folder\My AssetRipper\USCSandbox\publish\release_win-x64_separate"
.\USCSandbox.exe null "sharedassets0.assets" 0 --all --version 2022.3.2f1
```

## Output

- Decompiled shader files are written by the tool into its configured output flow.
- Session logs are written under `logs\` (for example `logs\session-*.log`).

## Quick Troubleshooting

- If startup fails with missing data, verify `classdata.tpk` is next to `USCSandbox.exe`.
- If a file path has spaces, wrap it in quotes.
- If shader metadata is stripped, pass `--version` explicitly.
