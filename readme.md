# USCSandbox

**USCSandbox** is a high-performance, headless Unity shader decompiler designed to translate compiled shader binaries (DXBC/NVN) back into readable, compilable Unity HLSL. Based on an enhanced version of the Ultra Shader Converter (USC), this tool is engineered to handle the complexities of modern Unity versions, including **Unity 6**, with an emphasis on **high-fidelity code generation and zero data loss**.

## 🚀 Key Features

*   **100% Code Completeness (New)**: Even when Unity strips reflection metadata, USCSandbox infers exact resource dimensions (Cube, 3D, 2DArray, Structured, Raw) directly from bytecode instructions. This guarantees compile-ready fallback declarations (e.g., `TextureCube<float4>`) instead of generic or broken types.
*   **Universal Optimizer Execution (New)**: High-level math patterns (like `normalize()`, `length()`, `lerp()`, and Unity macros) are aggressively reconstructed across *all* shader variants, regardless of missing parameter data.
*   **Unity 6 & 2022.x LTS Ready**: Robust handling of Merged Keyword arrays and External Parameter Blobs introduced in the latest engine iterations.
*   **Intelligent Variant Grouping**: Eliminates the "million-line shader" problem. It analyzes all variants within a pass and automatically generates compact code using `#if`, `#elif`, and `#endif` preprocessor directives.
*   **Full Nintendo Switch (NVN) Support**: Utilizes the Ryujinx IR translation layer to provide functional decompilation for Switch binaries.
*   **Culture-Invariant Output**: Prevents formatting corruption; ensures floating-point numbers consistently use the `.` separator regardless of the host system's regional settings.
*   **Segmented Blob Parsing**: Correctly handles segmented shader storage (Unity 2019.3+) which previously caused crashes in older decompilers.
*   **Comprehensive Logging**: Features structured, timestamped file logging (`logs/session-*.log`) and isolated per-shader logs, allowing for easy debugging without breaking batch extraction runs.

## 🛠 Supported Architectures

*   **DirectX 11 (PC)**: Full support for Shader Model 4.0 and 5.0. Includes advanced opcode coverage (`sample_b`, `bfi`, `ubfe`, `ld_raw`, `ld_structured`, etc.).
*   **Nintendo Switch (NVN)**: Functional translation via Ryujinx.Graphics.Shader.

## 💻 How to Use

The tool is a standalone CLI executable. Ensure `classdata.tpk` is located next to the executable before running.

```bash
USCS [bundle path] [assets path] [shader path id] <--platform> <--version> <--all>
```

### Arguments
*   `bundle path`: Path to the `.unity3d` or `.bundle` file. Use `null` if opening an `.assets` file directly.
*   `assets path`: The internal name of the assets file (e.g., `CAB-xxx`) or the relative path to a `.assets` file.
*   `shader path id`: The PathID of the target shader.
*   `--platform`: `d3d11` (default) or `Switch`.
*   `--version`: Required if the asset version is stripped. Supports up to Unity 6 (e.g., `6000.0.1f1`).
*   `--all`: Decompiles every shader found in the specified file.

### Examples

**Decompile a specific Unity 6 shader from a bundle:**
```bash
uscsandbox Game.bundle CAB-1234 55 --version 6000.0.1f1
```

**Decompile all shaders from a shared assets file:**
```bash
uscsandbox null sharedassets0.assets 0 --all --version 2022.3.2f1
```

## 🧠 Technical Overview

1.  **Metadata Extraction & Merging**: The tool parses the `SerializedShader` metadata to map hardware registers to human-readable names. It dynamically merges blob-local parameters with global "Common" parameters. 
2.  **Bytecode Type Inference**: When metadata is missing, USCSandbox scans the DXBC headers (`dcl_resource`) to accurately type resources as `Texture2DArray`, `TextureCube`, or `StructuredBuffer` rather than defaulting to raw data buffers.
3.  **USIL Translation**: Compiled bytecode is lifted into **Ultra Shader Intermediate Language (USIL)**.
4.  **Optimizer & Fixer Pipeline**:
    *   **High-Level Math**: Reconstructs low-level math clusters back into readable HLSL operations.
    *   **SamplerFixer**: Identifies internal Unity textures (e.g., `unity_Lightmap`) to assign correct sampler states.
    *   **DimensionsFixer**: Consolidates multiple `resinfo` queries into standard HLSL `GetDimensions` calls.
5.  **HLSL Reconstruction**: USIL is converted into a structured `.shader` file containing standard `Properties`, `SubShader`, and `Pass` blocks, cleanly wrapped in keyword `#if` blocks.

## ⚖️ License & Credits

Licensed under **GPL v3**.

This project is a standalone rework of the shader exporter found in [AssetRipper](https://github.com/AssetRipper/AssetRipper). It integrates research and logic from:
*   [uTinyRipper](https://github.com/mafaca/UtinyRipper) (Initial parsing concepts)
*   [Ryujinx](https://github.com/Ryujinx/Ryujinx) (NVN translation logic)
*   [3dmigoto](https://github.com/bo3b/3Dmigoto) (DirectX disassembly foundations)

---
*Note: This project is an independent optimization fork. For support with modern Unity 6 assets, please use the latest builds from this repository.*