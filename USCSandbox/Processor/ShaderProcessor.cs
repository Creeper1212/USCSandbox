using AssetRipper.Export.Modules.Shaders.UltraShaderConverter.Converter;
using AssetRipper.Export.Modules.Shaders.UltraShaderConverter.UShader.DirectX;
using AssetRipper.Export.Modules.Shaders.UltraShaderConverter.UShader.Function;
using AssetRipper.Export.Modules.Shaders.UltraShaderConverter.USIL;
using AssetRipper.Primitives;
using AssetsTools.NET;
using AssetsTools.NET.Extra.Decompressors.LZ4;
using System.Globalization;
using System.Text;
using USCSandbox.Extras;

namespace USCSandbox.Processor
{
    public class ShaderProcessor
    {
        private readonly AssetTypeValueField _shaderBf;
        private readonly GPUPlatform _platformId;
        private readonly UnityVersion _engVer;
        private readonly StringBuilderIndented _sb;

        public ShaderProcessor(AssetTypeValueField shaderBf, UnityVersion engVer, GPUPlatform platformId)
        {
            _engVer = engVer;
            _shaderBf = shaderBf;
            _platformId = platformId;
            _sb = new StringBuilderIndented();
        }

        public string Process()
        {
            _sb.Clear();

            var parsedForm = _shaderBf["m_ParsedForm"];
            var name = parsedForm["m_Name"].AsString;
            var keywordNames = parsedForm["m_KeywordNames.Array"].Select(i => i.AsString).ToList();
            Logger.Info($"ShaderProcessor.Process: shader='{name}', platform={_platformId}, unity={_engVer}");
            Logger.Debug($"Parsed keyword names ({keywordNames.Count}): {string.Join(", ", keywordNames)}");

            var platforms = _shaderBf["platforms.Array"].Select(i => i.AsInt).ToList();
            var offsets = _shaderBf["offsets.Array"];
            var compressedLengths = _shaderBf["compressedLengths.Array"];
            var decompressedLengths = _shaderBf["decompressedLengths.Array"];
            var compressedBlob = _shaderBf["compressedBlob.Array"].AsByteArray;
            Logger.Debug($"Compressed blob length: {compressedBlob.Length} bytes");

            var selectedIndex = platforms.IndexOf((int)_platformId);
            
            if (selectedIndex == -1)
            {
                Logger.Warning($"Shader does not contain requested platform {_platformId}. Available raw platform ids: {string.Join(", ", platforms)}");
                return $"// Shader does not contain platform: {_platformId}\n";
            }
            Logger.Info($"Selected platform index: {selectedIndex} (raw id {platforms[selectedIndex]})");

            // Unity 2019.3+ splits shaders into segments. We check if the elements contain arrays.
            bool hasSegments = offsets[selectedIndex].Children.Count > 0;
            int segmentCount = hasSegments ? offsets[selectedIndex]["Array"].Children.Count : 1;
            Logger.Info($"Segmented shader blob: {hasSegments}, segment count: {segmentCount}");

            byte[][] decompressedBlobs = new byte[segmentCount][];
            for (int i = 0; i < segmentCount; i++)
            {
                uint selectedOffset = hasSegments ? offsets[selectedIndex]["Array"][i].AsUInt : offsets[selectedIndex].AsUInt;
                uint selectedCompressedLength = hasSegments ? compressedLengths[selectedIndex]["Array"][i].AsUInt : compressedLengths[selectedIndex].AsUInt;
                uint selectedDecompressedLength = hasSegments ? decompressedLengths[selectedIndex]["Array"][i].AsUInt : decompressedLengths[selectedIndex].AsUInt;
                Logger.Debug($"Decompressing segment {i}: offset={selectedOffset}, compressed={selectedCompressedLength}, decompressed={selectedDecompressedLength}");

                decompressedBlobs[i] = new byte[selectedDecompressedLength];
                
                // Read from the exact offset within the compressed blob
                using (var compStream = new MemoryStream(compressedBlob, (int)selectedOffset, (int)selectedCompressedLength))
                using (var lz4Decoder = new Lz4DecoderStream(compStream))
                {
                    lz4Decoder.Read(decompressedBlobs[i], 0, (int)selectedDecompressedLength);
                }
            }

            var blobManager = new BlobManager(decompressedBlobs, _engVer);
            Logger.Info("BlobManager initialized from decompressed blobs.");

            _sb.AppendLine($"Shader \"{name}\" {{");
            _sb.Indent();
            {
                WriteProperties(parsedForm["m_PropInfo"]);
                WriteSubShaders(blobManager, parsedForm);
                if (!string.IsNullOrEmpty(parsedForm["m_FallbackName"].AsString))
                    _sb.AppendLine($"Fallback \"{parsedForm["m_FallbackName"].AsString}\"");
            }
            _sb.Unindent();
            _sb.AppendLine("}");
            Logger.Info($"ShaderProcessor.Process complete: '{name}'");

            return _sb.ToString();
        }

        private void WritePassBody(
            BlobManager blobManager,
            List<ShaderProgramBasket> baskets,
            int depth,
            string passName)
        {
            _sb.AppendLine("CGPROGRAM");
            string indent = new string(' ', depth * 4);
            var basketsInfo = baskets
                .Select(x => new
                {
                    progInfo = x.ProgramInfo,
                    subProgInfo = x.SubProgramInfo,
                    index = x.ParameterBlobIndex,
                    subProg = blobManager.GetShaderSubProgram((int)x.SubProgramInfo.BlobIndex)
                })
                .OrderBy(x => x.subProg.GetProgramType(_engVer).ToString(), StringComparer.Ordinal)
                .ThenByDescending(x => x.subProg.GlobalKeywords.Count + x.subProg.LocalKeywords.Count)
                .ToList();
            Logger.Debug($"WritePassBody('{passName}'): basket variants={basketsInfo.Count}");

            bool hasVertexVariant = basketsInfo.Any(x => IsVertexProgramType(x.subProg.GetProgramType(_engVer)));
            bool hasFragmentVariant = basketsInfo.Any(x => IsFragmentProgramType(x.subProg.GetProgramType(_engVer)));
            Logger.Debug($"WritePassBody('{passName}'): hasVertex={hasVertexVariant}, hasFragment={hasFragmentVariant}");

            if (hasVertexVariant)
            {
                _sb.AppendNoIndent($"{indent}#pragma vertex vert\n");
            }
            if (hasFragmentVariant)
            {
                _sb.AppendNoIndent($"{indent}#pragma fragment frag\n");
            }

            SortedSet<string> uniquePassKeywords = new(StringComparer.Ordinal);
            foreach (var basket in basketsInfo)
            {
                foreach (string keyword in basket.subProg.GlobalKeywords.Concat(basket.subProg.LocalKeywords))
                {
                    if (!string.IsNullOrWhiteSpace(keyword))
                    {
                        uniquePassKeywords.Add(keyword);
                    }
                }
            }

            foreach (string keyword in uniquePassKeywords)
            {
                _sb.AppendNoIndent($"{indent}#pragma multi_compile_local _ {keyword}\n");
            }
            Logger.Debug($"WritePassBody('{passName}'): unique pass keywords={uniquePassKeywords.Count}");
            _sb.AppendNoIndent("\n");

            var preparedVariants = new List<(
                ShaderGpuProgramType ProgramType,
                string[] Keywords,
                string KeywordSet,
                ShaderParams? Params,
                UShaderProgram? Program,
                bool Unsupported)>();
            Dictionary<string, Dictionary<int, FallbackResourceKind>> fallbackResourcesByKeywordSet = new(StringComparer.Ordinal);
            Dictionary<string, HashSet<int>> fallbackSamplersByKeywordSet = new(StringComparer.Ordinal);
            foreach (var basket in basketsInfo)
            {
                var subProg = basket.subProg;
                ShaderGpuProgramType programType = subProg.GetProgramType(_engVer);
                string[] keywords = subProg.GlobalKeywords
                    .Concat(subProg.LocalKeywords)
                    .Where(k => !string.IsNullOrWhiteSpace(k))
                    .Distinct(StringComparer.Ordinal)
                    .OrderBy(k => k, StringComparer.Ordinal)
                    .ToArray();
                Logger.Debug($"Variant: pass='{passName}', programType={programType}, keywords=[{string.Join(", ", keywords)}]");

                ShaderParams? param = basket.index >= 0
                    ? blobManager.GetShaderParams(basket.index)
                    : subProg.ShaderParams;
                string keywordSet = string.Join("-", keywords);
                if (param is null)
                {
                    Logger.Debug($"Variant has null ShaderParams: pass='{passName}', programType={programType}, paramIndex={basket.index}");
                }
                else
                {
                    param.CombineCommon(basket.progInfo);
                }

                USCShaderConverter converter = new USCShaderConverter();
                if (programType.IsDirectX())
                {
                    Logger.Debug($"Loading DX shader bytecode: pass='{passName}', programType={programType}, dataLength={subProg.ProgramData.Length}");
                    converter.LoadDirectXCompiledShader(new MemoryStream(subProg.ProgramData), _platformId, _engVer);
                    converter.ConvertDxShaderToUShaderProgram();
                }
                else if (programType.ToGPUPlatform() == GPUPlatform.Switch)
                {
                    Logger.Debug($"Loading NVN shader bytecode: pass='{passName}', programType={programType}, dataLength={subProg.ProgramData.Length}");
                    converter.LoadUnityNvnShader(new MemoryStream(subProg.ProgramData), _platformId, _engVer);
                    converter.ConvertNvnShaderToUShaderProgram(programType);
                }
                else
                {
                    Logger.Warning($"Unsupported program type skipped: pass='{passName}', programType={programType}");
                    preparedVariants.Add((programType, keywords, keywordSet, param, null, true));
                    continue;
                }

                if (param is not null)
                {
                    converter.ApplyMetadataToProgram(subProg, param, _engVer);
                }

                Dictionary<int, FallbackResourceKind> variantResourceKinds = new();
                HashSet<int> variantSamplerRegisters = new();
                CollectFallbackDeclarationHints(converter.ShaderProgram!, variantResourceKinds, variantSamplerRegisters);
                MergeFallbackDeclarationHints(
                    keywordSet,
                    variantResourceKinds,
                    variantSamplerRegisters,
                    fallbackResourcesByKeywordSet,
                    fallbackSamplersByKeywordSet);

                preparedVariants.Add((programType, keywords, keywordSet, param, converter.ShaderProgram, false));
            }

            Dictionary<string, HashSet<string>> declaredCBufs = new(StringComparer.Ordinal);
            foreach (var variant in preparedVariants)
            {
                bool hasKeywords = variant.Keywords.Length > 0;
                if (hasKeywords)
                {
                    _sb.AppendNoIndent($"{indent}#if {string.Join(" &&", variant.Keywords)} // {passName}:{variant.ProgramType}\n");
                }

                if (!declaredCBufs.TryGetValue(variant.KeywordSet, out HashSet<string>? declaredSet))
                {
                    declaredSet = new HashSet<string>(StringComparer.Ordinal);
                    declaredCBufs.Add(variant.KeywordSet, declaredSet);
                }

                if (variant.Params is not null)
                {
                    _sb.AppendNoIndent($"{indent}// CBs for {variant.ProgramType}\n");
                    foreach (ConstantBuffer cbuffer in variant.Params.ConstantBuffers)
                    {
                        _sb.AppendNoIndent(WritePassCBuffer(variant.Params, declaredSet, cbuffer, depth));
                    }

                    _sb.AppendNoIndent($"{indent}// Textures for {variant.ProgramType}\n");
                    _sb.AppendNoIndent(WritePassTextures(variant.Params, declaredSet, depth));

                    _sb.AppendNoIndent($"{indent}// Buffers for {variant.ProgramType}\n");
                    _sb.AppendNoIndent(WritePassBuffers(variant.Params, declaredSet, depth));
                    Logger.Debug($"Applied metadata: cbufferCount={variant.Params.ConstantBuffers.Count}, textureCount={variant.Params.TextureParameters.Count}");
                }

                if (variant.Unsupported || variant.Program is null)
                {
                    _sb.AppendNoIndent($"{indent}// Unsupported program type {variant.ProgramType}\n");
                    if (hasKeywords)
                    {
                        _sb.AppendNoIndent($"{indent}#endif\n");
                    }
                    _sb.AppendNoIndent("\n");
                    continue;
                }

                _sb.AppendNoIndent($"{indent}// Fallback resources for {variant.ProgramType}\n");
                Dictionary<int, FallbackResourceKind> resourceKinds = fallbackResourcesByKeywordSet.TryGetValue(variant.KeywordSet, out Dictionary<int, FallbackResourceKind>? mergedKinds)
                    ? mergedKinds
                    : new Dictionary<int, FallbackResourceKind>();
                HashSet<int> samplerRegisters = fallbackSamplersByKeywordSet.TryGetValue(variant.KeywordSet, out HashSet<int>? mergedSamplers)
                    ? mergedSamplers
                    : new HashSet<int>();
                _sb.AppendNoIndent(WriteFallbackResourceDeclarations(resourceKinds, samplerRegisters, declaredSet, depth));

                UShaderFunctionToHLSL hlslConverter = new UShaderFunctionToHLSL(variant.Program, depth);
                if (IsVertexProgramType(variant.ProgramType))
                {
                    _sb.AppendNoIndent(hlslConverter.WriteStruct());
                    _sb.AppendNoIndent("\n");
                    _sb.AppendNoIndent(hlslConverter.WriteFunction());
                }
                else if (IsFragmentProgramType(variant.ProgramType))
                {
                    _sb.AppendNoIndent(hlslConverter.WriteFunction());
                }
                else
                {
                    _sb.AppendNoIndent(hlslConverter.WriteFunction());
                }

                if (hasKeywords)
                {
                    _sb.AppendNoIndent($"{indent}#endif\n");
                }
                _sb.AppendNoIndent("\n");
                Logger.Debug($"Variant emitted successfully: pass='{passName}', programType={variant.ProgramType}");
            }

            _sb.AppendLine("ENDCG");
            _sb.AppendLine("");
        }

        private string WritePassCBuffer(
            ShaderParams shaderParams, HashSet<string> declaredCBufs,
            ConstantBuffer? cbuffer, int depth)
        {
            StringBuilder sb = new StringBuilder();
            if (cbuffer != null)
            {
                bool nonGlobalCbuffer = cbuffer.Name != "$Globals";
                int cbufferIndex = shaderParams.ConstantBuffers.IndexOf(cbuffer);

                bool wroteCbufferHeaderYet = false;
                
                char[] chars = new char[] { 'x', 'y', 'z', 'w' };
                List<ConstantBufferParameter> allParams = cbuffer.CBParams;
                foreach (ConstantBufferParameter param in allParams)
                {
                    string typeName = DXShaderNamingUtils.GetConstantBufferParamTypeName(param);
                    string name = param.ParamName;

                    // skip things like unity_MatrixVP if they show up in $Globals
                    if (UnityShaderConstants.INCLUDED_UNITY_PROP_NAMES.Contains(name))
                    {
                        continue;
                    }
                    
                    if (!wroteCbufferHeaderYet && nonGlobalCbuffer)
                    {
                        sb.Append(new string(' ', depth * 4));
                        sb.AppendLine($"// CBUFFER_START({cbuffer.Name}) // {cbufferIndex}");
                        depth++;
                    }

                    if (!declaredCBufs.Contains(name))
                    {
                        if (param.ArraySize > 0)
                        {
                            sb.Append(new string(' ', depth * 4));
                            if (nonGlobalCbuffer)
                                sb.Append("// ");
                            sb.AppendLine($"{typeName} {name}[{param.ArraySize}]; // {param.Index} (starting at cb{cbufferIndex}[{param.Index / 16}].{chars[param.Index % 16 / 4]})");
                        }
                        else
                        {
                            sb.Append(new string(' ', depth * 4));
                            if (nonGlobalCbuffer && !cbuffer.Name.StartsWith("UnityPerDrawSprite"))
                                sb.Append("// ");
                            sb.AppendLine($"{typeName} {name}; // {param.Index} (starting at cb{cbufferIndex}[{param.Index / 16}].{chars[param.Index % 16 / 4]})");
                        }
                        declaredCBufs.Add(name);
                    }

                    if (!wroteCbufferHeaderYet && nonGlobalCbuffer)
                    {
                        depth--;
                        sb.Append(new string(' ', depth * 4));
                        sb.AppendLine("// CBUFFER_END");
                        wroteCbufferHeaderYet = true;
                    }
                }
            }
            return sb.ToString();
        }

        private string WritePassTextures(
            ShaderParams shaderParams, HashSet<string> declaredCBufs, int depth)
        {
            StringBuilder sb = new StringBuilder();
            foreach (TextureParameter param in shaderParams.TextureParameters)
            {
                string name = param.Name;
                if (!declaredCBufs.Contains(name) && !UnityShaderConstants.BUILTIN_TEXTURE_NAMES.Contains(name))
                {
                    sb.Append(new string(' ', depth * 4));
                    switch (param.Dim)
                    {
                        case 2:
                            sb.AppendLine($"sampler2D {name}; // {param.Index}");
                            break;
                        case 3:
                            sb.AppendLine($"sampler3D {name}; // {param.Index}");
                            break;
                        case 4:
                            sb.AppendLine($"samplerCUBE {name}; // {param.Index}");
                            break;
                        case 5:
                            sb.AppendLine($"UNITY_DECLARE_TEX2DARRAY({name}); // {param.Index}");
                            break;
                        case 6:
                            sb.AppendLine($"UNITY_DECLARE_TEXCUBEARRAY({name}); // {param.Index}");
                            break;
                        default:
                            sb.AppendLine($"sampler2D {name}; // {param.Index} // Unsure of real type ({param.Dim})");
                            break;
                    }
                    declaredCBufs.Add(name);
                }
            }
            return sb.ToString();
        }

        private string WritePassBuffers(
            ShaderParams shaderParams, HashSet<string> declaredNames, int depth)
        {
            StringBuilder sb = new StringBuilder();

            foreach (BufferBinding buffer in shaderParams.Buffers)
            {
                string name = buffer.Name;
                if (!declaredNames.Contains(name))
                {
                    sb.Append(new string(' ', depth * 4));
                    sb.AppendLine($"ByteAddressBuffer {name}; // {buffer.Index}");
                    declaredNames.Add(name);
                }
            }

            foreach (UAVParameter uav in shaderParams.UAVs)
            {
                string name = uav.Name;
                if (!declaredNames.Contains(name))
                {
                    sb.Append(new string(' ', depth * 4));
                    sb.AppendLine($"RWByteAddressBuffer {name}; // {uav.Index}");
                    declaredNames.Add(name);
                }
            }

            return sb.ToString();
        }

        private string WriteFallbackResourceDeclarations(
            Dictionary<int, FallbackResourceKind> resourceKinds,
            HashSet<int> unresolvedSamplerRegisters,
            HashSet<string> declaredNames,
            int depth)
        {
            StringBuilder sb = new StringBuilder();

            foreach (KeyValuePair<int, FallbackResourceKind> fallback in resourceKinds.OrderBy(p => p.Key))
            {
                string name = $"{USILOperand.GetTypeShortForm(USILOperandType.ResourceRegister)}{fallback.Key}";
                if (!declaredNames.Contains(name))
                {
                    sb.Append(new string(' ', depth * 4));
                    string declaration = fallback.Value switch
                    {
                        FallbackResourceKind.Texture => "Texture2D<float4>",
                        FallbackResourceKind.Structured => "StructuredBuffer<float4>",
                        _ => "ByteAddressBuffer",
                    };
                    string kindComment = fallback.Value switch
                    {
                        FallbackResourceKind.Texture => "texture",
                        FallbackResourceKind.Structured => "structured",
                        _ => "raw",
                    };
                    sb.AppendLine($"{declaration} {name}; // unresolved {kindComment} fallback t{fallback.Key}");
                    declaredNames.Add(name);
                }
            }

            foreach (int samplerRegister in unresolvedSamplerRegisters.OrderBy(i => i))
            {
                string name = $"{USILOperand.GetTypeShortForm(USILOperandType.SamplerRegister)}{samplerRegister}";
                if (!declaredNames.Contains(name))
                {
                    sb.Append(new string(' ', depth * 4));
                    sb.AppendLine($"sampler2D {name}; // unresolved sampler fallback s{samplerRegister}");
                    declaredNames.Add(name);
                }
            }

            return sb.ToString();
        }

        private enum FallbackResourceKind
        {
            Texture,
            Structured,
            Raw,
        }

        private static void CollectFallbackDeclarationHints(
            UShaderProgram shaderProgram,
            Dictionary<int, FallbackResourceKind> resourceKinds,
            HashSet<int> unresolvedSamplerRegisters)
        {
            foreach (USILInstruction instruction in shaderProgram.instructions)
            {
                CollectFallbackOperandHints(instruction, resourceKinds, unresolvedSamplerRegisters);
            }
        }

        private static void MergeFallbackDeclarationHints(
            string keywordSet,
            Dictionary<int, FallbackResourceKind> incomingResourceKinds,
            HashSet<int> incomingSamplerRegisters,
            Dictionary<string, Dictionary<int, FallbackResourceKind>> fallbackResourcesByKeywordSet,
            Dictionary<string, HashSet<int>> fallbackSamplersByKeywordSet)
        {
            if (!fallbackResourcesByKeywordSet.TryGetValue(keywordSet, out Dictionary<int, FallbackResourceKind>? mergedKinds))
            {
                mergedKinds = new Dictionary<int, FallbackResourceKind>();
                fallbackResourcesByKeywordSet.Add(keywordSet, mergedKinds);
            }

            foreach (KeyValuePair<int, FallbackResourceKind> incoming in incomingResourceKinds)
            {
                if (mergedKinds.TryGetValue(incoming.Key, out FallbackResourceKind existingKind))
                {
                    mergedKinds[incoming.Key] = MergeFallbackResourceKinds(existingKind, incoming.Value);
                }
                else
                {
                    mergedKinds.Add(incoming.Key, incoming.Value);
                }
            }

            if (!fallbackSamplersByKeywordSet.TryGetValue(keywordSet, out HashSet<int>? mergedSamplers))
            {
                mergedSamplers = new HashSet<int>();
                fallbackSamplersByKeywordSet.Add(keywordSet, mergedSamplers);
            }

            mergedSamplers.UnionWith(incomingSamplerRegisters);
        }

        private static void CollectFallbackOperandHints(
            USILInstruction instruction,
            Dictionary<int, FallbackResourceKind> resourceKinds,
            HashSet<int> unresolvedSamplerRegisters)
        {
            if (instruction.destOperand is not null)
            {
                CollectFallbackOperandHints(
                    instruction.destOperand,
                    instruction.instructionType,
                    resourceKinds,
                    unresolvedSamplerRegisters);
            }

            foreach (USILOperand srcOperand in instruction.srcOperands)
            {
                CollectFallbackOperandHints(
                    srcOperand,
                    instruction.instructionType,
                    resourceKinds,
                    unresolvedSamplerRegisters);
            }
        }

        private static void CollectFallbackOperandHints(
            USILOperand operand,
            USILInstructionType instructionType,
            Dictionary<int, FallbackResourceKind> resourceKinds,
            HashSet<int> unresolvedSamplerRegisters)
        {
            if (operand.operandType == USILOperandType.ResourceRegister && !operand.metadataNameAssigned)
            {
                FallbackResourceKind inferredKind = InferFallbackResourceKind(instructionType);
                if (resourceKinds.TryGetValue(operand.registerIndex, out FallbackResourceKind existingKind))
                {
                    resourceKinds[operand.registerIndex] = MergeFallbackResourceKinds(existingKind, inferredKind);
                }
                else
                {
                    resourceKinds.Add(operand.registerIndex, inferredKind);
                }
            }
            else if (operand.operandType == USILOperandType.SamplerRegister && !operand.metadataNameAssigned)
            {
                unresolvedSamplerRegisters.Add(operand.registerIndex);
            }

            if (operand.arrayRelative is not null)
            {
                CollectFallbackOperandHints(
                    operand.arrayRelative,
                    instructionType,
                    resourceKinds,
                    unresolvedSamplerRegisters);
            }

            foreach (USILOperand child in operand.children)
            {
                CollectFallbackOperandHints(
                    child,
                    instructionType,
                    resourceKinds,
                    unresolvedSamplerRegisters);
            }
        }

        private static FallbackResourceKind InferFallbackResourceKind(USILInstructionType instructionType)
        {
            return instructionType switch
            {
                USILInstructionType.ResourceDimensionInfo or
                USILInstructionType.GetDimensions => FallbackResourceKind.Texture,
                USILInstructionType.LoadResourceStructured => FallbackResourceKind.Structured,
                _ => FallbackResourceKind.Raw,
            };
        }

        private static FallbackResourceKind MergeFallbackResourceKinds(
            FallbackResourceKind existingKind,
            FallbackResourceKind incomingKind)
        {
            if (existingKind == incomingKind)
            {
                return existingKind;
            }

            if (existingKind == FallbackResourceKind.Raw || incomingKind == FallbackResourceKind.Raw)
            {
                return FallbackResourceKind.Raw;
            }

            if (existingKind == FallbackResourceKind.Structured || incomingKind == FallbackResourceKind.Structured)
            {
                return FallbackResourceKind.Structured;
            }

            return FallbackResourceKind.Texture;
        }

        private void WriteProperties(AssetTypeValueField propInfo)
        {
            _sb.AppendLine("Properties {");
            _sb.Indent();
            var props = propInfo["m_Props.Array"];
            foreach (var prop in props)
            {
                _sb.Append("");

                var attributes = prop["m_Attributes.Array"];
                foreach (var attribute in attributes)
                {
                    _sb.AppendNoIndent($"[{attribute.AsString}] ");
                }

                var flags = (SerializedPropertyFlag)prop["m_Flags"].AsUInt;
                if (flags.HasFlag(SerializedPropertyFlag.HideInInspector))
                    _sb.AppendNoIndent("[HideInInspector] ");
                if (flags.HasFlag(SerializedPropertyFlag.PerRendererData))
                    _sb.AppendNoIndent("[PerRendererData] ");
                if (flags.HasFlag(SerializedPropertyFlag.NoScaleOffset))
                    _sb.AppendNoIndent("[NoScaleOffset] ");
                if (flags.HasFlag(SerializedPropertyFlag.Normal))
                    _sb.AppendNoIndent("[Normal] ");
                if (flags.HasFlag(SerializedPropertyFlag.HDR))
                    _sb.AppendNoIndent("[HDR] ");
                if (flags.HasFlag(SerializedPropertyFlag.Gamma))
                    _sb.AppendNoIndent("[Gamma] ");

                var name = prop["m_Name"].AsString;
                var description = prop["m_Description"].AsString;
                var type = (SerializedPropertyType)prop["m_Type"].AsInt;
                var defValues = new string[]
                {
                    prop["m_DefValue[0]"].AsFloat.ToString(CultureInfo.InvariantCulture),
                    prop["m_DefValue[1]"].AsFloat.ToString(CultureInfo.InvariantCulture),
                    prop["m_DefValue[2]"].AsFloat.ToString(CultureInfo.InvariantCulture),
                    prop["m_DefValue[3]"].AsFloat.ToString(CultureInfo.InvariantCulture)
                };
                var defTextureName = prop["m_DefTexture.m_DefaultName"].AsString;
                var defTextureDim = prop["m_DefTexture.m_TexDim"].AsInt;

                var typeName = type switch
                {
                    SerializedPropertyType.Color => "Color",
                    SerializedPropertyType.Vector => "Vector",
                    SerializedPropertyType.Float => "Float",
                    SerializedPropertyType.Range => $"Range({defValues[1]}, {defValues[2]})",
                    SerializedPropertyType.Texture => defTextureDim switch
                    {
                        1 => "any",
                        2 => "2D",
                        3 => "3D",
                        4 => "Cube",
                        5 => "2DArray",
                        6 => "CubeArray",
                        _ => throw new NotSupportedException("Bad texture dim")
                    },
                    SerializedPropertyType.Int => "Int",
                    _ => throw new NotSupportedException("Bad property type")
                };

                var value = type switch
                {
                    SerializedPropertyType.Color or
                    SerializedPropertyType.Vector => $"({defValues[0]}, {defValues[1]}, {defValues[2]}, {defValues[3]})",
                    SerializedPropertyType.Float or
                    SerializedPropertyType.Range or
                    SerializedPropertyType.Int => defValues[0],
                    SerializedPropertyType.Texture => $"\"{defTextureName}\" {{}}",
                    _ => throw new NotSupportedException("Bad property type")
                };

                _sb.AppendNoIndent($"{name} (\"{description}\", {typeName}) = {value}\n");
            }
            _sb.Unindent();
            _sb.AppendLine("}");
        }

        private void WriteSubShaders(BlobManager blobManager, AssetTypeValueField parsedForm)
        {
            var subshaders = parsedForm["m_SubShaders.Array"];
            Logger.Info($"SubShader count: {subshaders.Children.Count}");
            foreach (var subshader in subshaders)
            {
                _sb.AppendLine("SubShader {");
                _sb.Indent();
                {
                    var tags = subshader["m_Tags"]["tags.Array"];
                    if (tags.Children.Count > 0)
                    {
                        _sb.AppendLine("Tags {");
                        _sb.Indent();
                        {
                            foreach (var tag in tags)
                            {
                                _sb.AppendLine($"\"{tag["first"].AsString}\"=\"{tag["second"].AsString}\"");
                            }
                        }
                        _sb.Unindent();
                        _sb.AppendLine("}");
                    }

                    var lod = subshader["m_LOD"].AsInt;
                    if (lod != 0)
                    {
                        _sb.AppendLine($"LOD {lod}");
                    }

                    WritePasses(blobManager, subshader);
                }
                _sb.Unindent();
                _sb.AppendLine("}");
            }
        }

        private void WritePasses(BlobManager blobManager, AssetTypeValueField subshader)
        {
            var passes = subshader["m_Passes.Array"];
            Logger.Debug($"Pass count in subshader: {passes.Children.Count}");
            foreach (var pass in passes)
            {
                var usePassName = pass["m_UseName"].AsString;
                if (!string.IsNullOrEmpty(usePassName))
                {
                    _sb.AppendLine($"UsePass \"{usePassName}\"");
                    Logger.Debug($"UsePass emitted: {usePassName}");
                    continue;
                }
                
                _sb.AppendLine("Pass {");
                _sb.Indent();
                {
                    var passName = pass["m_State"]["m_Name"].AsString;
                    Logger.Info($"Processing pass: {passName}");
                    
                    WritePassState(pass["m_State"]);

                    var nameTable = pass["m_NameIndices.Array"]
                        .ToDictionary(ni => ni["second"].AsInt, ni => ni["first"].AsString);

                    var vertInfo = new SerializedProgramInfo(pass["progVertex"], nameTable);
                    var fragInfo = new SerializedProgramInfo(pass["progFragment"], nameTable);

                    List<ShaderProgramBasket> baskets =[];
                    AddMatchingBaskets(vertInfo, baskets);
                    AddMatchingBaskets(fragInfo, baskets);
                    Logger.Debug($"Pass '{passName}' matched program baskets: {baskets.Count}");

                    if (baskets.Count > 0)
                        WritePassBody(blobManager, baskets, _sb.GetIndent(), passName);
                }
                _sb.Unindent();
                _sb.AppendLine("}");
            }
        }

        private void WritePassState(AssetTypeValueField state)
        {
            var name = state["m_Name"].AsString;
            _sb.AppendLine($"Name \"{name}\"");

            var lod = state["m_LOD"].AsInt;
            if (lod != 0)
            {
                _sb.AppendLine($"LOD {lod}");
            }

            var rtSeparateBlend = state["rtSeparateBlend"].AsBool;
            if (rtSeparateBlend)
            {
                for (var i = 0; i < 8; i++)
                {
                    WritePassRtBlend(state[$"rtBlend{i}"], i);
                }
            }
            else
            {
                WritePassRtBlend(state["rtBlend0"], -1);
            }

            var alphaToMask = state["alphaToMask.val"].AsFloat;
            var zClip = (ZClip)(int)state["zClip.val"].AsFloat;
            var zTest = (ZTest)(int)state["zTest.val"].AsFloat;
            var zWrite = (ZWrite)(int)state["zWrite.val"].AsFloat;
            var culling = (CullMode)(int)state["culling.val"].AsFloat;
            var offsetFactor = state["offsetFactor.val"].AsFloat;
            var offsetUnits = state["offsetUnits.val"].AsFloat;
            var stencilRef = state["stencilRef.val"].AsFloat;
            var stencilReadMask = state["stencilReadMask.val"].AsFloat;
            var stencilWriteMask = state["stencilWriteMask.val"].AsFloat;
            var stencilOpPass = (StencilOp)(int)state["stencilOp.pass.val"].AsFloat;
            var stencilOpFail = (StencilOp)(int)state["stencilOp.fail.val"].AsFloat;
            var stencilOpZfail = (StencilOp)(int)state["stencilOp.zFail.val"].AsFloat;
            var stencilOpComp = (StencilComp)(int)state["stencilOp.comp.val"].AsFloat;
            var stencilOpFrontPass = (StencilOp)(int)state["stencilOpFront.pass.val"].AsFloat;
            var stencilOpFrontFail = (StencilOp)(int)state["stencilOpFront.fail.val"].AsFloat;
            var stencilOpFrontZfail = (StencilOp)(int)state["stencilOpFront.zFail.val"].AsFloat;
            var stencilOpFrontComp = (StencilComp)(int)state["stencilOpFront.comp.val"].AsFloat;
            var stencilOpBackPass = (StencilOp)(int)state["stencilOpBack.pass.val"].AsFloat;
            var stencilOpBackFail = (StencilOp)(int)state["stencilOpBack.fail.val"].AsFloat;
            var stencilOpBackZfail = (StencilOp)(int)state["stencilOpBack.zFail.val"].AsFloat;
            var stencilOpBackComp = (StencilComp)(int)state["stencilOpBack.comp.val"].AsFloat;
            var fogMode = (FogMode)(int)state["fogMode"].AsFloat;
            var fogColorX = state["fogColor.x.val"].AsFloat;
            var fogColorY = state["fogColor.y.val"].AsFloat;
            var fogColorZ = state["fogColor.z.val"].AsFloat;
            var fogColorW = state["fogColor.w.val"].AsFloat;
            var fogDensity = state["fogDensity.val"].AsFloat;
            var fogStart = state["fogStart.val"].AsFloat;
            var fogEnd = state["fogEnd.val"].AsFloat;

            var lighting = state["lighting"].AsBool;

            if (alphaToMask > 0f)
            {
                _sb.AppendLine("AlphaToMask On");
            }
            if (zClip == ZClip.On)
            {
                _sb.AppendLine("ZClip On");
            }
            if (zTest != ZTest.None && zTest != ZTest.LEqual)
            {
                _sb.AppendLine($"ZTest {zTest}");
            }
            if (zWrite != ZWrite.On)
            {
                _sb.AppendLine($"ZWrite {zWrite}");
            }
            if (culling != CullMode.Back)
            {
                _sb.AppendLine($"Cull {culling}");
            }
            if (offsetFactor != 0f || offsetUnits != 0f)
            {
                _sb.AppendLine($"Offset {offsetFactor}, {offsetUnits}");
            }
            
            if (stencilRef != 0.0 || stencilReadMask != 255.0 || stencilWriteMask != 255.0
                || !(stencilOpPass == StencilOp.Keep && stencilOpFail == StencilOp.Keep && stencilOpZfail == StencilOp.Keep && stencilOpComp == StencilComp.Always)
                || !(stencilOpFrontPass == StencilOp.Keep && stencilOpFrontFail == StencilOp.Keep && stencilOpFrontZfail == StencilOp.Keep && stencilOpFrontComp == StencilComp.Always)
                || !(stencilOpBackPass == StencilOp.Keep && stencilOpBackFail == StencilOp.Keep && stencilOpBackZfail == StencilOp.Keep && stencilOpBackComp == StencilComp.Always))
			{
				_sb.AppendLine("Stencil {");
                _sb.Indent();
				if (stencilRef != 0.0)
				{
                    _sb.AppendLine($"Ref {stencilRef}");
				}
				if (stencilReadMask != 255.0)
				{
                    _sb.AppendLine($"ReadMask {stencilReadMask}");
				}
				if (stencilWriteMask != 255.0)
				{
                    _sb.AppendLine($"WriteMask {stencilWriteMask}");
				}
				if (stencilOpPass != StencilOp.Keep
                    || stencilOpFail != StencilOp.Keep
                    || stencilOpZfail != StencilOp.Keep
                    || (stencilOpComp != StencilComp.Always && stencilOpComp != StencilComp.Disabled))
				{
                    _sb.AppendLine($"Comp {stencilOpComp}");
                    _sb.AppendLine($"Pass {stencilOpPass}");
                    _sb.AppendLine($"Fail {stencilOpFail}");
                    _sb.AppendLine($"ZFail {stencilOpZfail}");
				}
				if (stencilOpFrontPass != StencilOp.Keep
                    || stencilOpFrontFail != StencilOp.Keep
                    || stencilOpFrontZfail != StencilOp.Keep
                    || (stencilOpFrontComp != StencilComp.Always && stencilOpFrontComp != StencilComp.Disabled))
				{
                    _sb.AppendLine($"CompFront {stencilOpFrontComp}");
                    _sb.AppendLine($"PassFront {stencilOpFrontPass}");
                    _sb.AppendLine($"FailFront {stencilOpFrontFail}");
                    _sb.AppendLine($"ZFailFront {stencilOpFrontZfail}");
				}
				if (stencilOpBackPass != StencilOp.Keep
                    || stencilOpBackFail != StencilOp.Keep
                    || stencilOpBackZfail != StencilOp.Keep
                    || (stencilOpBackComp != StencilComp.Always && stencilOpBackComp != StencilComp.Disabled))
				{
                    _sb.AppendLine($"CompBack {stencilOpBackComp}");
                    _sb.AppendLine($"PassBack {stencilOpBackPass}");
                    _sb.AppendLine($"FailBack {stencilOpBackFail}");
                    _sb.AppendLine($"ZFailBack {stencilOpBackZfail}");
				}
				_sb.Unindent();
				_sb.AppendLine("}");
			}

			if (fogMode != FogMode.Unknown || fogDensity != 0.0 || fogStart != 0.0 || fogEnd != 0.0
                || !(fogColorX == 0.0 && fogColorY == 0.0 && fogColorZ == 0.0 && fogColorW == 0.0))
			{
                _sb.AppendLine("Fog {");
                _sb.Indent();
				if (fogMode != FogMode.Unknown)
				{
                    _sb.AppendLine($"Mode {fogMode}");
				}
				if (fogColorX != 0.0 || fogColorY != 0.0 || fogColorZ != 0.0 || fogColorW != 0.0)
				{
                    _sb.AppendLine($"Color ({fogColorX.ToString(CultureInfo.InvariantCulture)}," +
                                   $"{fogColorY.ToString(CultureInfo.InvariantCulture)}," +
                                   $"{fogColorZ.ToString(CultureInfo.InvariantCulture)}," +
                                   $"{fogColorW.ToString(CultureInfo.InvariantCulture)})");
				}
				if (fogDensity != 0.0)
				{
                    _sb.AppendLine($"Density {fogDensity.ToString(CultureInfo.InvariantCulture)}");
				}
				if (fogStart != 0.0 || fogEnd != 0.0)
				{
                    _sb.AppendLine($"Range {fogStart.ToString(CultureInfo.InvariantCulture)}, " +
                                   $"{fogEnd.ToString(CultureInfo.InvariantCulture)}");
				}
                _sb.Unindent();
                _sb.AppendLine("}");
			}

            if (lighting)
            {
                _sb.AppendLine("Lighting On");
            }

            var tags = state["m_Tags"]["tags.Array"];
            if (tags.Children.Count > 0)
            {
                _sb.AppendLine("Tags {");
                _sb.Indent();
                {
                    foreach (var tag in tags)
                    {
                        _sb.AppendLine($"\"{tag["first"].AsString}\"=\"{tag["second"].AsString}\"");
                    }
                }
                _sb.Unindent();
                _sb.AppendLine("}");
            }
        }

        private void WritePassRtBlend(AssetTypeValueField rtBlend, int index)
        {
            var srcBlend = (BlendMode)(int)rtBlend["srcBlend.val"].AsFloat;
            var destBlend = (BlendMode)(int)rtBlend["destBlend.val"].AsFloat;
            var srcBlendAlpha = (BlendMode)(int)rtBlend["srcBlendAlpha.val"].AsFloat;
            var destBlendAlpha = (BlendMode)(int)rtBlend["destBlendAlpha.val"].AsFloat;
            var blendOp = (BlendOp)(int)rtBlend["blendOp.val"].AsFloat;
            var blendOpAlpha = (BlendOp)(int)rtBlend["blendOpAlpha.val"].AsFloat;
            var colMask = (ColorWriteMask)(int)rtBlend["colMask.val"].AsFloat;

            if (srcBlend != BlendMode.One || destBlend != BlendMode.Zero || srcBlendAlpha != BlendMode.One || destBlendAlpha != BlendMode.Zero)
            {
                _sb.Append("");
                _sb.AppendNoIndent("Blend ");
                if (index != -1)
                {
                    _sb.AppendNoIndent($"{index} ");
                }
                _sb.AppendNoIndent($"{srcBlend} {destBlend}");
                if (srcBlendAlpha != BlendMode.One || destBlendAlpha != BlendMode.Zero)
                {
                    _sb.AppendNoIndent($", {srcBlendAlpha} {destBlendAlpha}");
                }
                _sb.AppendNoIndent("\n");
            }

            if (blendOp != BlendOp.Add || blendOpAlpha != BlendOp.Add)
            {
                _sb.Append("");
                _sb.AppendNoIndent("BlendOp ");
                if (index != -1)
                {
                    _sb.AppendNoIndent($"{index} ");
                }
                _sb.AppendNoIndent($"{blendOp}");
                if (blendOpAlpha != BlendOp.Add)
                {
                    _sb.AppendNoIndent($", {blendOpAlpha}");
                }
                _sb.AppendNoIndent("\n");
            }

            if (colMask != ColorWriteMask.All)
            {
                _sb.Append("");
                _sb.AppendNoIndent("ColorMask ");
                if (colMask == ColorWriteMask.None)
                {
                    _sb.AppendNoIndent("0");
                }
                else
                {
                    if ((colMask & ColorWriteMask.Red) == ColorWriteMask.Red)
                    {
                        _sb.AppendNoIndent("R");
                    }
                    if ((colMask & ColorWriteMask.Green) == ColorWriteMask.Green)
                    {
                        _sb.AppendNoIndent("G");
                    }
                    if ((colMask & ColorWriteMask.Blue) == ColorWriteMask.Blue)
                    {
                        _sb.AppendNoIndent("B");
                    }
                    if ((colMask & ColorWriteMask.Alpha) == ColorWriteMask.Alpha)
                    {
                        _sb.AppendNoIndent("A");
                    }
                }
                if (index != -1)
                {
                    _sb.AppendNoIndent($" {index}"); // -1 check needed?
                }
                _sb.AppendNoIndent("\n");
            }
        }

        private void AddMatchingBaskets(SerializedProgramInfo programInfo, List<ShaderProgramBasket> baskets)
        {
            int added = 0;
            for (int i = 0; i < programInfo.SubProgramInfos.Count; i++)
            {
                SerializedSubProgramInfo subProgramInfo = programInfo.SubProgramInfos[i];
                ShaderGpuProgramType type = ConvertSerializedType(subProgramInfo.GpuProgramType);
                if (type.ToGPUPlatform() == _platformId || (_platformId == GPUPlatform.d3d11 && type.IsDirectX()))
                {
                    int parameterBlobIndex = programInfo.ParameterBlobIndices.Count > i
                        ? (int)programInfo.ParameterBlobIndices[i]
                        : -1;
                    baskets.Add(new ShaderProgramBasket(programInfo, subProgramInfo, parameterBlobIndex));
                    added++;
                }
            }
            Logger.Debug($"AddMatchingBaskets: scanned={programInfo.SubProgramInfos.Count}, added={added}, platform={_platformId}");
        }

        private ShaderGpuProgramType ConvertSerializedType(int rawType)
        {
            if (_engVer.IsGreaterEqual(5, 5))
            {
                return ((ShaderGpuProgramType55)rawType).ToGpuProgramType();
            }
            else
            {
                return ((ShaderGpuProgramType53)rawType).ToGpuProgramType();
            }
        }

        private static bool IsVertexProgramType(ShaderGpuProgramType type)
        {
            return type == ShaderGpuProgramType.ConsoleVS
                || type.ToString().Contains("Vertex", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsFragmentProgramType(ShaderGpuProgramType type)
        {
            string name = type.ToString();
            return type == ShaderGpuProgramType.ConsoleFS
                || name.Contains("Pixel", StringComparison.OrdinalIgnoreCase)
                || name.Contains("Fragment", StringComparison.OrdinalIgnoreCase);
        }
    }
}
