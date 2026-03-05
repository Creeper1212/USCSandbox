# USCSandbox

A powerful, headless Unity shader decompiler based on Ultra Shader Converter (USC). This tool extracts and decompiles compiled Unity shaders into readable HLSL.

## Key Features (New!)
- **Unity 6 Support:** Fully supports the new Merged Keyword arrays and External Parameter Blobs introduced in Unity 6 and late 2022/2023 LTS.
- **Smart Variant Grouping:** Unlike older versions that produced millions of lines, this version groups identical shader variants using `#if` and `#elif` preprocessor directives, drastically reducing file size.
- **Functional Switch NVN:** Integrated Ryujinx IR translation to support Nintendo Switch (NVN) shaders.
- **Culture Invariant:** Fixed the "German Comma" bug. Floats are now correctly written with `.` regardless of system region.
- **Modern DX11 Support:** Robust support for Shader Model 5.0 headers and segmented shader blobs (Unity 2019.3+).

## Supported Architectures
- **DirectX 11 (PC):** Robust support for SM 4.0 and 5.0.
- **Switch NVN:** Supported via Ryujinx translation layer.

## How to Use

The tool is run via Command Line:

```bash
USCS [bundle path] [assets path] [shader path id] <--platform> <--version> <--all>
```

### Command Arguments
- `bundle path`: Path to the `.unity3d` or `.bundle` file (use `null` if opening an `.assets` file directly).
- `assets path`: The internal name of the assets file (e.g., `CAB-xxx`) or the path to a `.assets` file.
- `shader path id`: The PathID of the shader you want to decompile.
- `--platform`: Set to `d3d11` (default) or `Switch`.
- `--version`: Override the Unity version (required if the file header is stripped).
- `--all`: Decompiles every shader found in the specified file.

### Examples

**List all assets files in a bundle:**
```bash
uscsandbox file.bundle
```

**List shader assets in a bundle:**
```bash
uscsandbox file.bundle CAB-abcdef0123456789abcdef
```

**Decompile a single shader in a bundle:**
```bash
uscsandbox file.bundle CAB-abcdef0123456789abcdef 123456789123456789
```

**Decompile a single shader in a bundle with stripped version (e.g., Unity 6.0.0.50f1):**
```bash
uscsandbox file.bundle CAB-abcdef0123456789abcdef 123456789123456789 --version 6000.0.50f1
```

**Decompile all shaders in a bundle:**
```bash
uscsandbox file.bundle CAB-abcdef0123456789abcdef --all
```

**List shader assets in an .assets file:**
```bash
uscsandbox null resources.assets
```

**Decompile a single shader in an .assets file:**
```bash
uscsandbox null resources.assets 123456789123456789
```

**Decompile all shaders in an .assets file:**
```bash
uscsandbox null resources.assets --all
```

**Decompile a Switch shader from a bundle:**
```bash
uscsandbox file.bundle CAB-abcdef0123456789abcdef 123456789123456789 --platform Switch
```

## How it Works
1.  **Metadata Merging:** The tool extracts "Common Parameters" from the asset metadata and merges them with the local binary blob parameters. This ensures global Unity variables like `unity_ObjectToWorld` are correctly identified and used in the shader code.
2.  **USIL Translation:** Compiled shader bytecode (DirectX bytecode or Ryujinx Intermediate Representation for NVN) is translated into a platform-agnostic Ultra Shader Intermediate Language (USIL).
3.  **Optimization:** USIL undergoes a series of "Fixers" and "Optimizers." Fixers correct structural issues and missing information (e.g., sampler type detection, merging `GetDimensions` calls). Optimizers then clean and simplify the code (e.g., removing redundant math, reordering comparisons, grouping shader variants).
4.  **HLSL Generation:** The optimized USIL is then written as a clean, human-readable Unity `.shader` file. This includes generating standard HLSL structs (`appdata`, `v2f`) and wrapping shader variants in `#if/#elif/#endif` preprocessor directives to produce compact output.

## License
Licensed under **GPL v3**.
This project integrates and builds upon logic derived from several sources:
-   Initial Unity shader parsing concepts from [uTinyRipper](https://github.com/mafaca/UtinyRipper).
-   Significant architectural and parsing fixes from [AssetRipper](https://github.com/AssetRipper/AssetRipper)'s shader exporter, including version-gated reading logic for various Unity iterations.
-   DirectX shader disassembly logic, which has historical ties to [3dmigoto](https://github.com/bo3b/3Dmigoto).
-   Nintendo Switch (NVN) shader translation utilizing the powerful [Ryujinx.Graphics.Shader](https://github.com/Ryujinx/Ryujinx/tree/master/Ryujinx.Graphics.Shader) library.
