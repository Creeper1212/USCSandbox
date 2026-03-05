using AssetRipper.Primitives;
using AssetsTools.NET;
using System.Collections.Generic;

namespace USCSandbox.Processor
{
    public class TextureParameter
    {
        public string Name;
        public int Index;
        public int SamplerIndex;
        public bool MultiSampled;
        public byte Dim;

        // Constructor for reading from the Shader Blob (Compiled Data)
        public TextureParameter(AssetsFileReader r, UnityVersion engVer, string name)
        {
            var index = r.ReadInt32();
            var extraValue = r.ReadInt32();

            Name = name;
            Index = index;

            var hasNewTextureParams = engVer.IsGreaterEqual(2018, 2);
            var hasMultiSampled = engVer.IsGreaterEqual(2017, 3);

            if (hasNewTextureParams)
            {
                // In 2018.2+, metadata is packed into a uint:
                // Bit 0: MultiSampled
                // Bits 1+: Dimension
                var textureExtraValue = r.ReadUInt32();
                MultiSampled = (textureExtraValue & 1) == 1;
                Dim = (byte)(textureExtraValue >> 1);
                SamplerIndex = extraValue;
            }
            else if (hasMultiSampled)
            {
                // In 2017.3+, MultiSampled is a separate boolean read as uint
                var textureExtraValue = r.ReadUInt32();
                MultiSampled = textureExtraValue == 1;
                
                Dim = unchecked((byte)extraValue);
                SamplerIndex = extraValue >> 8;
                if (SamplerIndex == 0xFFFFFF)
                {
                    SamplerIndex = -1;
                }
            }
            else
            {
                // Older versions
                MultiSampled = false;
                Dim = unchecked((byte)extraValue);
                SamplerIndex = extraValue >> 8;
                if (SamplerIndex == 0xFFFFFF)
                {
                    SamplerIndex = -1;
                }
            }
        }

        // Constructor for reading from Asset Metadata (Serialized Common Parameters)
        public TextureParameter(AssetTypeValueField field, Dictionary<int, string> nameTable)
        {
            Name = nameTable[field["m_NameIndex"].AsInt];
            Index = field["m_Index"].AsInt;
            SamplerIndex = field["m_SamplerIndex"].AsInt;
            MultiSampled = field["m_MultiSampled"].AsBool;
            Dim = (byte)field["m_Dim"].AsSByte;
        }
    }
}
