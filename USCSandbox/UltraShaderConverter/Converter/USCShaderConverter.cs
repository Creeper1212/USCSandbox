using AssetRipper.Export.Modules.Shaders.UltraShaderConverter.DirectXDisassembler;
using AssetRipper.Export.Modules.Shaders.UltraShaderConverter.UShader.DirectX;
using AssetRipper.Export.Modules.Shaders.UltraShaderConverter.UShader.Function;
using AssetRipper.Export.Modules.Shaders.UltraShaderConverter.USIL;
using AssetRipper.Primitives;
using Ryujinx.Graphics.Shader.Translation;
using System;
using System.Buffers.Binary;
using System.IO;
using USCSandbox.Processor;
using USCSandbox.UltraShaderConverter.NVN;
using USCSandbox.UltraShaderConverter.UShader.NVN;

namespace AssetRipper.Export.Modules.Shaders.UltraShaderConverter.Converter
{
    public class USCShaderConverter
    {
        public byte[] dbgData1 = new byte[0];
        public byte[] dbgData2 = new byte[0];
        public DirectXCompiledShader? DxShader { get; set; }
        public NvnUnityShader? NvnShader { get; set; }
        public UShaderProgram? ShaderProgram { get; set; }

        public void LoadDirectXCompiledShader(Stream data, GPUPlatform graphicApi, UnityVersion version)
        {
            int offset = GetDirectXDataOffset(version, graphicApi, data.ReadByte());
            data.Position = offset;
            
            // Read the remainder into a clean stream for DX decompilation
            MemoryStream trimmedData = new MemoryStream();
            data.CopyTo(trimmedData);
            trimmedData.Position = 0;
            
            DxShader = new DirectXCompiledShader(trimmedData);
        }

        private static int GetDirectXDataOffset(UnityVersion version, GPUPlatform graphicApi, int headerVersion)
        {
            bool hasHeader = graphicApi != GPUPlatform.d3d9;
            if (hasHeader)
            {
                bool hasGSInputPrimitive = version.IsGreaterEqual(5, 4);
                int offset = hasGSInputPrimitive ? 6 : 5;
                if (headerVersion >= 2)
                {
                    offset += 0x20;
                }
                return offset;
            }
            return 0;
        }

        public void LoadUnityNvnShader(Stream data, GPUPlatform graphicApi, UnityVersion version)
        {
            Span<byte> tmpBuf = stackalloc byte[8];
            data.Position = 8;
            data.Read(tmpBuf);

            var opt = new TranslationOptions(TargetLanguage.Glsl, TargetApi.OpenGL, TranslationFlags.None);

            if (BinaryPrimitives.ReadInt64LittleEndian(tmpBuf) == -1)
            {
                // newer merged version
                const int MAX_STAGE_COUNT = 6;
                const int FIELD_COUNT = 4;
                const int ROW_LEN = MAX_STAGE_COUNT * sizeof(int);
                const int START_OF_SHADER_DATA = ROW_LEN * FIELD_COUNT;
                const int SWITCH_DATA_OFFSET = 0x30;

                Span<byte> mergedHeader = new byte[ROW_LEN * FIELD_COUNT];
                data.Position = 0;
                data.Read(mergedHeader);

                TranslatorContext? vertCtx = null;
                TranslatorContext? fragCtx = null;

                for (int i = 0; i < MAX_STAGE_COUNT; i++)
                {
                    int baseOff = i * sizeof(int);
                    int dataStartPos = baseOff + ROW_LEN * 1;
                    int dataStart = BinaryPrimitives.ReadInt32LittleEndian(mergedHeader.Slice(dataStartPos, sizeof(int)));
                    
                    if (dataStart == -1) continue;

                    int headerLenPos = baseOff + ROW_LEN * 2;
                    int headerLen = BinaryPrimitives.ReadInt32LittleEndian(mergedHeader.Slice(headerLenPos, sizeof(int)));

                    int storageFlagsPos = baseOff + ROW_LEN * 3;
                    uint shaderBodyLen = BinaryPrimitives.ReadUInt32LittleEndian(mergedHeader.Slice(storageFlagsPos, sizeof(uint)));

                    byte[] stageBody = new byte[shaderBodyLen - SWITCH_DATA_OFFSET];
                    data.Position = START_OF_SHADER_DATA + dataStart + headerLen + SWITCH_DATA_OFFSET;
                    data.Read(stageBody, 0, stageBody.Length);

                    var ctx = Translator.CreateContext(0, new GpuAccessor(stageBody), opt);
                    
                    if (i == 0)
                    {
                        vertCtx = ctx;
                        dbgData1 = stageBody;
                    }
                    else if (i == 1)
                    {
                        fragCtx = ctx;
                        dbgData2 = stageBody;
                    }
                }

                NvnShader = new NvnUnityShader(vertCtx, fragCtx);
            }
            else
            {
                // older separated version
                const int HEADER_SIZE = 0x10;
                const int START_OF_SHADER_DATA = HEADER_SIZE;
                const int SWITCH_DATA_OFFSET = 0x30;

                Span<byte> singleHeader = new byte[HEADER_SIZE];
                data.Position = 0;
                data.Read(singleHeader);

                int kind = BinaryPrimitives.ReadInt32LittleEndian(singleHeader.Slice(0, sizeof(int)));
                int headerLen = BinaryPrimitives.ReadInt32LittleEndian(singleHeader.Slice(8, sizeof(int)));
                uint shaderBodyLen = BinaryPrimitives.ReadUInt32LittleEndian(singleHeader.Slice(12, sizeof(uint)));

                byte[] stageBody = new byte[shaderBodyLen - SWITCH_DATA_OFFSET];
                data.Position = START_OF_SHADER_DATA + headerLen + SWITCH_DATA_OFFSET;
                data.Read(stageBody, 0, stageBody.Length);

                var ctx = Translator.CreateContext(0, new GpuAccessor(stageBody), opt);
                
                if (kind == 0) NvnShader = new NvnUnityShader(ctx);
                else if (kind == 1) NvnShader = new NvnUnityShader(null, ctx);
                else NvnShader = new NvnUnityShader(ctx); // fallback
            }
        }

        public void ConvertDxShaderToUShaderProgram()
        {
            if (DxShader == null) throw new Exception($"You need to call {nameof(LoadDirectXCompiledShader)} first!");

            DirectXProgramToUSIL dx2UsilConverter = new DirectXProgramToUSIL(DxShader);
            dx2UsilConverter.Convert();
            ShaderProgram = dx2UsilConverter.shader;
        }

        public void ConvertNvnShaderToUShaderProgram(ShaderGpuProgramType type)
        {
            if (NvnShader == null) throw new Exception($"You need to call {nameof(LoadUnityNvnShader)} first!");

            TranslatorContext? ctx = null;
            if (NvnShader.CombinedShader)
            {
                if (type == ShaderGpuProgramType.ConsoleVS) ctx = NvnShader.VertShader;
                else if (type == ShaderGpuProgramType.ConsoleFS) ctx = NvnShader.FragShader;
                else throw new NotSupportedException("Only vertex and fragment shaders are supported at the moment!");
            }
            else
            {
                ctx = NvnShader.OnlyShader ?? NvnShader.VertShader ?? NvnShader.FragShader;
            }

            if (ctx == null) throw new Exception("Shader type not found!");

            NvnProgramToUSIL nvn2UsilConverter = new NvnProgramToUSIL(ctx);
            nvn2UsilConverter.Convert();
            ShaderProgram = nvn2UsilConverter.shader;
        }

        public void ApplyMetadataToProgram(ShaderSubProgram subProgram, ShaderParams shaderParams, UnityVersion version)
        {
            if (ShaderProgram == null) throw new Exception("You need to convert the shader first!");

            ShaderGpuProgramType shaderProgramType = subProgram.GetProgramType(version);

            bool isVertex = shaderProgramType == ShaderGpuProgramType.DX11VertexSM40 || 
                            shaderProgramType == ShaderGpuProgramType.DX11VertexSM50 || 
                            shaderProgramType == ShaderGpuProgramType.ConsoleVS;
            bool isFragment = shaderProgramType == ShaderGpuProgramType.DX11PixelSM40 || 
                              shaderProgramType == ShaderGpuProgramType.DX11PixelSM50 || 
                              shaderProgramType == ShaderGpuProgramType.ConsoleFS;

            if (!isVertex && !isFragment) throw new NotSupportedException("Only vertex and fragment shaders are supported at the moment!");

            ShaderProgram.shaderFunctionType = isVertex ? UShaderFunctionType.Vertex : UShaderFunctionType.Fragment;

            USILOptimizerApplier.Apply(ShaderProgram, shaderParams);
        }
    }
}
