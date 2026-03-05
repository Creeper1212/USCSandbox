using System;

namespace USCSandbox.Processor
{
    // The modern, unified enumeration of all Unity GPU Program Types
    public enum ShaderGpuProgramType
    {
        Unknown = 0,
        GLLegacy = 1,
        GLES31AEP = 2,
        GLES31 = 3,
        GLES3 = 4,
        GLES = 5,
        GLCore32 = 6,
        GLCore41 = 7,
        GLCore43 = 8,
        DX9VertexSM20 = 9,
        DX9VertexSM30 = 10,
        DX9PixelSM20 = 11,
        DX9PixelSM30 = 12,
        DX10Level9Vertex = 13,
        DX10Level9Pixel = 14,
        DX11VertexSM40 = 15,
        DX11VertexSM50 = 16,
        DX11PixelSM40 = 17,
        DX11PixelSM50 = 18,
        DX11GeometrySM40 = 19,
        DX11GeometrySM50 = 20,
        DX11HullSM50 = 21,
        DX11DomainSM50 = 22,
        MetalVS = 23,
        MetalFS = 24,
        SPIRV = 25,
        Console = 26,
        ConsoleVS = 26,
        ConsoleFS = 27,
        ConsoleHS = 28,
        ConsoleDS = 29,
        ConsoleGS = 30,
        RayTracing = 31,
        PS5NGGC = 32
    }

    // Enum mapping for Unity 5.5 and newer
    public enum ShaderGpuProgramType55
    {
        Unknown = 0,
        GLLegacy = 1,
        GLES31AEP = 2,
        GLES31 = 3,
        GLES3 = 4,
        GLES = 5,
        GLCore32 = 6,
        GLCore41 = 7,
        GLCore43 = 8,
        DX9VertexSM20 = 9,
        DX9VertexSM30 = 10,
        DX9PixelSM20 = 11,
        DX9PixelSM30 = 12,
        DX10Level9Vertex = 13,
        DX10Level9Pixel = 14,
        DX11VertexSM40 = 15,
        DX11VertexSM50 = 16,
        DX11PixelSM40 = 17,
        DX11PixelSM50 = 18,
        DX11GeometrySM40 = 19,
        DX11GeometrySM50 = 20,
        DX11HullSM50 = 21,
        DX11DomainSM50 = 22,
        MetalVS = 23,
        MetalFS = 24,
        SPIRV = 25,
        Console = 26,
        ConsoleFS = 27,
        ConsoleHS = 28,
        ConsoleDS = 29,
        ConsoleGS = 30,
        RayTracing = 31,
    }

    // Enum mapping for Unity 5.3 to 5.4
    public enum ShaderGpuProgramType53
    {
        Unknown = 0,
        GLLegacy = 1,
        GLES31AEP = 2,
        GLES31 = 3,
        GLES3 = 4,
        GLES = 5,
        GLCore32 = 6,
        GLCore41 = 7,
        GLCore43 = 8,
        DX9VertexSM20 = 9,
        DX9VertexSM30 = 10,
        DX9PixelSM20 = 11,
        DX9PixelSM30 = 12,
        DX10Level9Vertex = 13,
        DX10Level9Pixel = 14,
        DX11VertexSM40 = 15,
        DX11VertexSM50 = 16,
        DX11PixelSM40 = 17,
        DX11PixelSM50 = 18,
        DX11GeometrySM40 = 19,
        DX11GeometrySM50 = 20,
        DX11HullSM50 = 21,
        DX11DomainSM50 = 22,
        MetalVS = 23,
        MetalFS = 24,
        ConsoleVS = 25,
        ConsoleFS = 26,
        ConsoleHS = 27,
        ConsoleDS = 28,
        ConsoleGS = 29,
    }

    public static class ShaderGpuProgramTypeExtensions
    {
        // Identifies if the target is Vulkan (SPIR-V)
        public static bool IsVulkan(this ShaderGpuProgramType type) => type == ShaderGpuProgramType.SPIRV;
        
        // Identifies if the target is any version of DirectX (DX9, DX10, DX11, SM4, SM5)
        public static bool IsDirectX(this ShaderGpuProgramType type)
        {
            return type >= ShaderGpuProgramType.DX9VertexSM20 && type <= ShaderGpuProgramType.DX11DomainSM50;
        }

        // Converts the 5.5+ format to the modern standard enum
        public static ShaderGpuProgramType ToGpuProgramType(this ShaderGpuProgramType55 _this)
        {
            return (ShaderGpuProgramType)_this;
        }

        // Converts the 5.3/5.4 format to the modern standard enum
        public static ShaderGpuProgramType ToGpuProgramType(this ShaderGpuProgramType53 _this)
        {
            // Unity 5.3 didn't have SPIRV at index 25, so we shift Console targets up by 1
            if ((int)_this >= 25) return (ShaderGpuProgramType)((int)_this + 1);
            return (ShaderGpuProgramType)_this;
        }

        // Maps the highly specific program type to the general backend target used by the decompiler
        public static GPUPlatform ToGPUPlatform(this ShaderGpuProgramType _this)
        {
            // Group all DX variants (SM4.0, SM5.0) to the single d3d11 bucket
            if (_this.IsDirectX()) return GPUPlatform.d3d11;
            
            if (_this.IsVulkan()) return GPUPlatform.vulkan;
            
            switch (_this)
            {
                case ShaderGpuProgramType.GLES:
                case ShaderGpuProgramType.GLES3:
                case ShaderGpuProgramType.GLES31:
                case ShaderGpuProgramType.GLES31AEP:
                    return GPUPlatform.gles3;
                
                case ShaderGpuProgramType.GLCore32:
                case ShaderGpuProgramType.GLCore41:
                case ShaderGpuProgramType.GLCore43:
                case ShaderGpuProgramType.GLLegacy:
                    return GPUPlatform.glcore;
                
                case ShaderGpuProgramType.MetalVS:
                case ShaderGpuProgramType.MetalFS:
                    return GPUPlatform.metal;
                
                case ShaderGpuProgramType.PS5NGGC:
                    return GPUPlatform.PS5NGGC;

                case ShaderGpuProgramType.Console:
                case ShaderGpuProgramType.ConsoleFS:
                case ShaderGpuProgramType.ConsoleHS:
                case ShaderGpuProgramType.ConsoleDS:
                case ShaderGpuProgramType.ConsoleGS:
                    return GPUPlatform.Switch;

                default:
                    return GPUPlatform.unknown;
            }
        }
    }
}