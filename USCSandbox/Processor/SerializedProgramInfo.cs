using AssetsTools.NET;
using System.Collections.Generic;
using System.Linq;

namespace USCSandbox.Processor
{
    public class SerializedProgramInfo
    {
        public List<uint> ParameterBlobIndices;
        public List<SerializedSubProgramInfo> SubProgramInfos;
        
        // Common parameters (Unity 2021.2+)
        public List<TextureParameter> CommonTextureParameters;
        public List<ConstantBuffer> CommonCBuffers;
        public List<BufferBinding> CommonCBBindings;

        public SerializedProgramInfo(AssetTypeValueField field, Dictionary<int, string> nameTable)
        {
            // Parse SubPrograms
            if (!field["m_PlayerSubPrograms"].IsDummy)
            {
                // Newer Unity versions use PlayerSubPrograms
                var parameterBlobIndices = field["m_ParameterBlobIndices.Array"];
                if (parameterBlobIndices.Children.Count > 0)
                {
                    // Handle nested array structure (vector) in newer versions
                    var actualIndices = GetActualArray(parameterBlobIndices);
                    ParameterBlobIndices = actualIndices.Select(i => i.AsUInt).ToList();
                }
                else
                {
                    ParameterBlobIndices = new List<uint>(0);
                }

                var subProgramInfos = field["m_PlayerSubPrograms.Array"];
                if (subProgramInfos.Children.Count > 0)
                {
                    var actualInfos = GetActualArray(subProgramInfos);
                    SubProgramInfos = actualInfos.Select(i => new SerializedSubProgramInfo(i)).ToList();
                }
                else
                {
                    SubProgramInfos = new List<SerializedSubProgramInfo>(0);
                }
            }
            else
            {
                // Older Unity versions use m_SubPrograms
                ParameterBlobIndices = new List<uint>();
                
                var subProgramInfos = field["m_SubPrograms.Array"];
                SubProgramInfos = subProgramInfos
                    .Select(i => new SerializedSubProgramInfo(i))
                    .ToList();
            }

            // Parse Common Parameters (Introduced in Unity 2021.2)
            if (!field["m_CommonParameters"].IsDummy)
            {
                var commonParams = field["m_CommonParameters"];
                CommonTextureParameters = GetCommonTextureParams(commonParams["m_TextureParams.Array"], nameTable);
                CommonCBuffers = GetCommonCBuffers(commonParams["m_ConstantBuffers.Array"], nameTable);
                CommonCBBindings = GetCommonCBBindings(commonParams["m_ConstantBufferBindings.Array"], nameTable);
            }
            else
            {
                CommonTextureParameters = new List<TextureParameter>();
                CommonCBuffers = new List<ConstantBuffer>();
                CommonCBBindings = new List<BufferBinding>();
            }
        }

        // Helper to handle the "Array inside Array" structure found in some newer Unity versions
        // where a vector might be serialized as Array[0].data[...]
        private AssetTypeValueField GetActualArray(AssetTypeValueField field)
        {
            if (field.Children.Count > 0 && field.Last().FieldName == "data")
            {
                 // Return the inner array inside the 'data' field
                 return field.Last()["Array"];
            }
            return field;
        }

        private List<TextureParameter> GetCommonTextureParams(AssetTypeValueField field, Dictionary<int, string> nameTable)
        {
            var textureParams = new List<TextureParameter>();
            var actualArray = GetActualArray(field);
            foreach (var param in actualArray)
            {
                textureParams.Add(new TextureParameter(param, nameTable));
            }
            return textureParams;
        }

        private List<ConstantBuffer> GetCommonCBuffers(AssetTypeValueField field, Dictionary<int, string> nameTable)
        {
            var cbuffers = new List<ConstantBuffer>();
            var actualArray = GetActualArray(field);
            foreach (var cbuf in actualArray)
            {
                cbuffers.Add(new ConstantBuffer(cbuf, nameTable));
            }
            return cbuffers;
        }

        private List<BufferBinding> GetCommonCBBindings(AssetTypeValueField field, Dictionary<int, string> nameTable)
        {
            var bindings = new List<BufferBinding>();
            var actualArray = GetActualArray(field);
            foreach (var binding in actualArray)
            {
                bindings.Add(new BufferBinding(binding, nameTable));
            }
            return bindings;
        }

        public List<SerializedSubProgramInfo> GetForPlatform(int gpuProgramType)
        {
            return SubProgramInfos
                .Where(spi => spi.GpuProgramType == gpuProgramType)
                .ToList();
        }
    }
}
