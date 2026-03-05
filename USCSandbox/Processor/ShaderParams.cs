using AssetRipper.Primitives;
using AssetsTools.NET;
using System.Collections.Generic;
using System.Linq;

namespace USCSandbox.Processor
{
    public class ShaderParams
    {
        public ConstantBuffer? BaseConstantBuffer;
        public List<ConstantBuffer> ConstantBuffers;

        public List<TextureParameter> TextureParameters;
        public List<BufferBinding> ConstBindings;
        public List<BufferBinding> Buffers;
        public List<UAVParameter> UAVs;
        public List<SamplerParameter> Samplers;

        public ShaderParams(AssetsFileReader r, UnityVersion engVer, bool readBlobVersion)
        {
            if (readBlobVersion)
            {
                var blobVersion = r.ReadInt32();
            }

            // Read Constant Buffer groups
            var firstParamsCount = r.ReadInt32();
            if (firstParamsCount > 0)
            {
                // The first buffer is usually the "Globals" or base buffer
                BaseConstantBuffer = new ConstantBuffer(r, engVer);
                
                // The subsequent buffers are standard named Constant Buffers
                ConstantBuffers = new List<ConstantBuffer>(firstParamsCount - 1);
                for (var i = 1; i < firstParamsCount; i++)
                {
                    ConstantBuffers.Add(new ConstantBuffer(r, engVer));
                }
            }
            else
            {
                ConstantBuffers = new List<ConstantBuffer>(0);
            }

            // Initialize lists for other resources
            TextureParameters = new List<TextureParameter>();
            ConstBindings = new List<BufferBinding>();
            Buffers = new List<BufferBinding>();
            UAVs = new List<UAVParameter>();
            Samplers = new List<SamplerParameter>();

            // Read Resources (Textures, Buffers, UAVs, Samplers)
            var secondParamsCount = r.ReadInt32();
            for (var i = 0; i < secondParamsCount; i++)
            {
                var name = r.ReadCountStringInt32();
                r.Align();

                var type = r.ReadInt32();

                if (type == 0) // Texture
                {
                    TextureParameters.Add(new TextureParameter(r, engVer, name));
                }
                else if (type == 1) // Constant Buffer Binding
                {
                    ConstBindings.Add(new BufferBinding(r, name));
                }
                else if (type == 2) // Buffer
                {
                    Buffers.Add(new BufferBinding(r, name));
                }
                else if (type == 3) // UAV
                {
                    UAVs.Add(new UAVParameter(r, name));
                }
                else if (type == 4) // Sampler
                {
                    Samplers.Add(new SamplerParameter(r));
                }
            }
        }

        public void CombineCommon(SerializedProgramInfo progInfo)
        {
            // Merge Common Constant Buffers (e.g., UnityPerDraw, UnityPerFrame)
            foreach (var commonCBuf in progInfo.CommonCBuffers)
            {
                var existing = ConstantBuffers.FirstOrDefault(c => c.Name == commonCBuf.Name);
                if (existing != null)
                {
                    // If the local buffer is marked partial, and the common one has data, 
                    // we prefer the common one as it often contains the full struct definition.
                    if (existing.Partial && commonCBuf.CBParams.Count > existing.CBParams.Count)
                    {
                        existing.CBParams = commonCBuf.CBParams;
                    }
                }
                else
                {
                    ConstantBuffers.Add(commonCBuf);
                }
            }

            // Merge Common Texture Parameters
            foreach (var commonTex in progInfo.CommonTextureParameters)
            {
                if (!TextureParameters.Any(t => t.Name == commonTex.Name))
                {
                    TextureParameters.Add(commonTex);
                }
            }

            // Merge Common Buffer Bindings
            foreach (var commonBind in progInfo.CommonCBBindings)
            {
                if (!ConstBindings.Any(b => b.Name == commonBind.Name))
                {
                    ConstBindings.Add(commonBind);
                }
            }
        }
    }
}
