using AssetRipper.Primitives;
using AssetsTools.NET;

namespace USCSandbox.Processor
{
    public class ShaderSubProgram
    {
        public int ProgramType;
        public int StatsALU;
        public int StatsTEX;
        public int StatsFlow;
        public int StatsTempRegister;

        public List<string> GlobalKeywords = new List<string>();
        public List<string> LocalKeywords = new List<string>();

        public byte[] ProgramData;
        public ParserBindChannels BindChannels;

        public ShaderParams ShaderParams;

        // Version constants based on reference AssetRipper logic
        private static bool HasStatsTempRegister(UnityVersion version) => version.IsGreaterEqual(5, 5);
        private static bool HasLocalKeywords(UnityVersion version) => version.IsGreaterEqual(2019);
        private static bool HasMergedKeywords(UnityVersion version) => version.IsGreaterEqual(2021, 2);

        public ShaderSubProgram(AssetsFileReader r, UnityVersion version)
        {
            // Unity writes a version/date header at the start of every subprogram block
            var blobVersion = r.ReadInt32();

            ProgramType = r.ReadInt32();
            StatsALU = r.ReadInt32();
            StatsTEX = r.ReadInt32();
            StatsFlow = r.ReadInt32();

            if (HasStatsTempRegister(version))
            {
                StatsTempRegister = r.ReadInt32();
            }

            if (HasMergedKeywords(version))
            {
                // In 2021.2+, Global and Local keywords are one single array
                int keywordCount = r.ReadInt32();
                for (int i = 0; i < keywordCount; i++)
                {
                    GlobalKeywords.Add(r.ReadCountStringInt32());
                    r.Align();
                }
            }
            else
            {
                // Older versions: Global keywords first
                int globalKeywordCount = r.ReadInt32();
                for (int i = 0; i < globalKeywordCount; i++)
                {
                    GlobalKeywords.Add(r.ReadCountStringInt32());
                    r.Align();
                }

                // Then Local keywords (introduced in 2019.1)
                if (HasLocalKeywords(version))
                {
                    int localKeywordCount = r.ReadInt32();
                    for (int i = 0; i < localKeywordCount; i++)
                    {
                        LocalKeywords.Add(r.ReadCountStringInt32());
                        r.Align();
                    }
                }
            }

            // Read the actual shader bytecode
            var programDataSize = r.ReadInt32();
            ProgramData = r.ReadBytes(programDataSize);
            r.Align();

            // Read hardware binding channels
            BindChannels = new ParserBindChannels(r);

            // Read parameters (Constant Buffers, Textures, Samplers)
            // Note: In very new Unity versions (2021+), this might be in a separate ParameterBlob,
            // but we check if there is data remaining in this subprogram block.
            if (r.BaseStream.Position < r.BaseStream.Length)
            {
                ShaderParams = new ShaderParams(r, version, false);
            }
        }

        public ShaderGpuProgramType GetProgramType(UnityVersion version)
        {
            if (version.IsGreaterEqual(5, 5))
            {
                return ((ShaderGpuProgramType55)ProgramType).ToGpuProgramType();
            }
            else
            {
                return ((ShaderGpuProgramType53)ProgramType).ToGpuProgramType();
            }
        }
    }
}
