# Build Fix Report

## Build target
- Command: `dotnet publish USCSandbox/USCSandbox/USCSandbox.csproj -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true -p:IncludeNativeLibrariesForSelfExtract=true -o ./publish`
- Result: Success (warnings only)

## Edited files

### 1) `USCSandbox/UltraShaderConverter/UShader/DirectX/DirectXProgramToUSIL.cs`
- Removed a pasted markdown/doc block that had been appended to the end of the C# file.
- Why: it introduced invalid tokens (backticks, markdown headings, extra top-level code) and broke compilation.

### 2) `USCSandbox/UltraShaderConverter/UShader/NVN/NvnProgramToUSIL.cs`
- Fixed malformed statement in `HandleLoadStore` (`specialOp.mask = new int[] { srcMaskIndex.Value };`).
- Replaced the file with a compile-safe NVN fallback converter implementation that preserves the same public API and emits a comment instruction.
- Why: the previous NVN implementation referenced Ryujinx internal/non-public APIs and unavailable members in the shipped dependency versions, causing hard compile failures.

### 3) `USCSandbox/UltraShaderConverter/USIL/USILInputOutput.cs`
- Changed namespace to `AssetRipper.Export.Modules.Shaders.UltraShaderConverter.USIL`.
- Why: this aligned the type with the rest of the USIL code and fixed unresolved type errors.

### 4) `USCSandbox/UltraShaderConverter/USIL/USILConstants.cs`
- Changed namespace to `AssetRipper.Export.Modules.Shaders.UltraShaderConverter.USIL`.
- Why: this fixed unresolved `USILConstants` references across DirectX/USIL/HLSL generation code.

### 5) `USCSandbox/UltraShaderConverter/Converter/USCShaderConverter.cs`
- Added `using USCSandbox;`.
- Why: resolves `GPUPlatform` type lookup in converter methods.

### 6) `USCSandbox/Processor/ShaderProcessor.cs`
- Rewrote `WritePasses` to use fuzzy platform matching across all subprogram entries:
  - include when `type.ToGPUPlatform() == _platformId`
  - include when `_platformId == GPUPlatform.d3d11 && type.IsDirectX()`
- Rewrote `WritePassBody` to be null-safe for simple shaders:
  - `WritePassCBuffer` and `WritePassTextures` are only called when `param != null`
  - metadata apply is also guarded with the same null check
- Added keyword pragma generation at pass start:
  - gathers unique `GlobalKeywords + LocalKeywords` across all variants
  - emits `#pragma multi_compile_local _ <KEYWORD>` lines
- Reworked per-variant code emission:
  - combines global and local keywords
  - emits `#if K1 &&K2` / `#endif` around each keyworded variant
  - emits direct code for variants with no keywords
- Updated HLSL emission behavior:
  - vertex-like types (`name contains "Vertex"` or `ConsoleVS`) call `WriteStruct()` then `WriteFunction()`
  - fragment-like types (`name contains "Pixel"`/`"Fragment"` or `ConsoleFS`) call only `WriteFunction()`
- Preserved segmented LZ4 blob decompression logic in `Process()` for Unity 2019.3+.

### 7) `USCSandbox/UltraShaderConverter/USIL/Metadders/USILSamplerMetadder.cs`
- Replaced a mismatched implementation that referenced non-existent namespaces/types (`USCSandbox.ShaderCode.*`, `IUsilOptimizer`, `ShaderParameters`, `UsilOperand`, etc.).
- Reimplemented it against this repository's actual API:
  - namespace `AssetRipper.Export.Modules.Shaders.UltraShaderConverter.USIL.Metadders`
  - class `USILSamplerMetadder : IUSILOptimizer`
  - uses `UShaderProgram`, `ShaderParams`, `USILOperand`, and lower-case field names (`instructions`, `srcOperands`, `operandType`, etc.).
- Restored sampler/resource metadata assignment for texture/sampler/buffer/UAV registers.

### 8) `USCSandbox/UltraShaderConverter/USIL/USILInstructionType.cs`
- Added high-level lifted instruction types:
  - `Lerp`
  - `Normalize`
  - `Length`
- Why: enables direct HLSL reconstruction of common math patterns instead of emitting raw low-level ops.

### 9) `USCSandbox/UltraShaderConverter/USIL/Optimizers/USILHighLevelMathOptimizer.cs` (new file)
- Added a new optimizer pass that detects and rewrites low-level instruction sequences into high-level USIL:
  - `add/sub + mad` -> `Lerp`
  - `dot + rsq + mul` -> `Normalize`
  - `dot + sqrt` -> `Length`
- Added Unity macro lifting patterns:
  - `mul(unity_MatrixVP, mul(unity_ObjectToWorld, x))` -> `UnityObjectToClipPos(x)`
  - `mul(unity_WorldToObject, n)` -> `UnityObjectToWorldNormal(n)`

### 10) `USCSandbox/UltraShaderConverter/USIL/USILOptimizerApplier.cs`
- Enabled matrix combine detection (`USILMatrixMulOptimizer`).
- Added the new `USILHighLevelMathOptimizer` into the optimizer pipeline.
- Why: allows matrix/macro pattern lifting and high-level math reconstruction to run automatically for every decompiled subprogram.

### 11) `USCSandbox/UltraShaderConverter/UShader/Function/UShaderFunctionToHLSL.cs`
- Added HLSL emit handlers for new lifted instructions:
  - `Lerp` -> `lerp(a, b, t)`
  - `Normalize` -> `normalize(v)`
  - `Length` -> `length(v)`
- Added emit handlers for lifted Unity macros:
  - `UnityObjectToClipPos`
  - `UnityObjectToWorldNormal`
- Improved matrix multiply emission:
  - now respects `transposeMatrix` and flips mul order when needed.

### 12) `USCSandbox/USCSandbox/Logger.cs`
- Replaced console-only logger with structured, timestamped file logging.
- Added session log lifecycle:
  - `Logger.Initialize(logsRootDirectory)` creates `logs/session-YYYYMMDD-HHMMSS.log`.
  - `Logger.Shutdown()` cleanly closes open log writers.
- Added per-shader log lifecycle:
  - `Logger.StartShaderLog(logsRootDirectory, shaderName, shaderPathId)` creates `logs/shaders/<shader>_<pathid>.log`.
  - `Logger.EndShaderLog()` closes per-shader writer after each shader.
- Added log levels and exception helper:
  - `Debug`, `Info`, `Warning`, `Error`, `Exception`.
- All messages now include UTC ISO-8601 timestamp and are written to console + session log + current shader log.

### 13) `USCSandbox/USCSandbox/Program.cs`
- Added logger bootstrap and shutdown in `Main()`:
  - initializes logs in `./logs`
  - logs startup metadata and command-line arguments
  - logs clean shutdown in `finally`
- Added detailed processing logs:
  - parsed optional args (`--platform`, `--version`, `--all`)
  - bundle/assets load paths
  - unity version auto-detection
  - class database setup
  - shader queue size
- Added per-shader logging scope:
  - starts per-shader log before decompilation
  - catches and logs per-shader failures without crashing the whole run
  - closes per-shader log in `finally`
- Logs output shader file path after successful write.

### 14) `USCSandbox/USCSandbox/Processor/ShaderProcessor.cs`
- Added detailed trace logs across the decompile pipeline:
  - shader identity/platform/version
  - parsed keyword names and compressed blob sizes
  - selected platform index and platform availability warnings
  - segmented blob detection and per-segment decompression metadata
  - subshader count, pass count, pass names
  - matched basket counts from fuzzy program-type platform matching
  - per-pass variant counts, vertex/fragment presence, keyword aggregation
  - per-variant program type, keyword set, shader-param presence, metadata counts
  - DX/NVN bytecode conversion decisions and unsupported-type skips
- Logs variant emission success and process completion for each shader.

### 15) `USCSandbox/USCSandbox/Program.cs` (EXE-relative paths)
- Switched path resolution for runtime files to EXE directory (`AppContext.BaseDirectory`):
  - logs now always write to `<exe>/logs`
  - per-shader logs now write to `<exe>/logs/shaders`
  - class database now loads from `<exe>/classdata.tpk`
- Added startup validation:
  - if `classdata.tpk` is missing next to the EXE, the tool logs an error and exits with a clear message.

### 16) `USCSandbox/USCSandbox/USCSandbox.csproj`
- Added content copy rule for class database:
  - includes `..\Files\classdata.tpk`
  - links it as `classdata.tpk` in output
  - copies to build output (`PreserveNewest`) and publish output (`Always`)
- Why: guarantees `classdata.tpk` is next to `USCSandbox.exe` after publish.

## Artifact
- `publish/USCSandbox.exe`

## Latest Iteration (2026-03-05)

### Build command
- `dotnet publish USCSandbox/USCSandbox/USCSandbox.csproj -c Release -r win-x64 --self-contained false -o USCSandbox/publish`
- Result: Success (warnings only)

### Runtime validation command
- `USCSandbox.exe null "D:\Steam\steamapps\content\app_1533390\depot_1533391\Gorilla Tag_Data\sharedassets0.assets" 0 --all --version 2022.3.2f1`
- Session log: `USCSandbox/publish/logs/session-20260305-152532.log`

### Files edited in this iteration

#### 17) `USCSandbox/USCSandbox/Processor/ShaderProcessor.cs`
- Fixed variant keyword-set scope bug:
  - `keywordSet`/`declaredSet` initialization moved outside `if (param != null)`.
  - fallback resource declaration now uses the same declared-name set even when shader params are null.
- Why: resolved compile error and ensured fallback declarations are stable for every variant path.

#### 18) `USCSandbox/PLAN.txt`
- Replaced old plan text with current phased roadmap and concrete baseline metrics from latest run.
- Added explicit section for multi-include reconstruction expectations and limits.

### Current measured baseline after this iteration
- Build: success
- Decompile runtime exceptions: 0
- Unsupported instruction comments in generated shaders: 0
- Unresolved fallback resources (`rscN`) in output corpus: 256

### Next focus
- Typed fallback resource declarations (`Texture2D/StructuredBuffer/ByteAddressBuffer` by usage).
- Unresolved sampler fallback declarations (`smpN`).
- Structured load emission cleanup to remove fragile cast patterns.

## Latest Iteration (2026-03-05, fallback declaration fidelity pass)

### Build command
- `dotnet build USCSandbox/USCSandbox/USCSandbox.csproj -c Release`
- Result: Success (warnings only)

### Runtime spot-check commands
- `USCSandbox.exe null "D:\Steam\steamapps\content\app_1533390\depot_1533391\Gorilla Tag_Data\sharedassets0.assets" 2104 --version 2022.3.2f1`
- `USCSandbox.exe null "D:\Steam\steamapps\content\app_1533390\depot_1533391\Gorilla Tag_Data\sharedassets0.assets" 2123 --version 2022.3.2f1`
- Result: Success, generated outputs updated as expected.

### Files edited in this iteration

#### 19) `USCSandbox/USCSandbox/Processor/ShaderProcessor.cs`
- Reworked fallback declaration emission to infer unresolved resource/sampler fallbacks from opcode usage.
- Added unresolved sampler fallback declarations:
  - `sampler2D smpN; // unresolved sampler fallback sN`
- Added typed unresolved resource fallback declarations:
  - `ByteAddressBuffer rscN` for raw fallback usage.
  - `StructuredBuffer<float4> rscN` for structured fallback usage.
  - `Texture2D<float4> rscN` for dimension-query fallback usage.
- Added keyword-set merge logic for fallback declarations so type selection is consistent across variants sharing the same keyword condition.
- Why: avoids cross-variant mismatches like declaring `rscN` as texture in one variant and using raw loads in another variant under the same active keyword set.

#### 20) `USCSandbox/USCSandbox/UltraShaderConverter/UShader/Function/UShaderFunctionToHLSL.cs`
- Replaced unresolved sample fallback emitters from non-existent `texND*` calls to compile-safe 2D fallbacks:
  - `texND(...)` -> `tex2D(...)`
  - `texNDlod(...)` -> `tex2Dlod(...)`
  - `texNDgrad(...)` -> `tex2Dgrad(...)`
- Added fallback-safe handling for sampler-type sample paths when metadata is missing.
- Why: unresolved sampler paths now emit valid HLSL call forms instead of undefined helper names.

### Spot-check output deltas
- `out/Unlit/Texture.shader` now emits:
  - `ByteAddressBuffer rsc0; // unresolved raw fallback t0`
  - `sampler2D smp0; // unresolved sampler fallback s0`
  - `tmp0 = tex2D(smp0, inp.texcoord.xy);`
- `out/GorillaTag/SnapPieceIndirect.shader` now emits:
  - `ByteAddressBuffer rsc0; // unresolved raw fallback t0`
  - `StructuredBuffer<float4> rsc1; // unresolved structured fallback t1`
  - `StructuredBuffer<float4> rsc2; // unresolved structured fallback t2`
  - `sampler2D smp0; // unresolved sampler fallback s0`

## Latest Iteration (2026-03-05, structured-load cleanup + full corpus revalidation)

### Clean/build/decompile commands
- `dotnet clean USCSandbox/USCSandbox.csproj -c Release`
- `dotnet build USCSandbox/USCSandbox.csproj -c Release`
- `USCSandbox.exe null "D:\Steam\steamapps\content\app_1533390\depot_1533391\Gorilla Tag_Data\sharedassets0.assets" 0 --all --version 2022.3.2f1`

### Files edited in this iteration

#### 21) `USCSandbox/USCSandbox/UltraShaderConverter/UShader/Function/UShaderFunctionToHLSL.cs`
- Reworked structured resource load emission (`HandleLoadResourceStructured`) to remove invalid cast/index pattern:
  - old non-compilable shape: `((float4[1])rscN.Load(...))[0][...]`
  - new shape: `rscN.Load(...).xyzw` (or component swizzle based on byte offset and destination mask)
- Added byte-offset aware extraction:
  - computes structure element offset (`byteOffset / 16`)
  - computes component offset (`(byteOffset % 16) / 4`)
  - emits mask-based swizzle for destination width
- Added guard comments for dynamic/partial offsets to keep generated output valid and diagnosable.
- Why: this removes the last known non-compilable structured-load cast pattern in the Gorilla Tag corpus.

### Full-run metrics (from `USCSandbox/USCSandbox/bin/Release/net8.0/out` and latest session log)
- shader files generated: 28
- shader logs generated: 28
- session error lines: 0
- session exception-like lines: 0
- unsupported instruction comments: 0
- unresolved `texND*` calls: 0
- unresolved fallback declarations: 528
- invalid structured cast pattern (`((float4[1])...Load(...))`): 0
- undeclared `rscN` references: 0
- undeclared `smpN` references: 0

### Result
- Shader completeness is improved for the newest decompilation:
  - no unsupported op comments
  - no undeclared resource/sampler symbols
  - no invalid structured-load cast emission pattern
- Remaining quality gap is fallback declaration volume/typing refinement (count remains 528), not hard compile blockers from missing symbols/patterns.
