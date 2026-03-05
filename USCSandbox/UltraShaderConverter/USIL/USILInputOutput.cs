namespace AssetRipper.Export.Modules.Shaders.UltraShaderConverter.USIL
{
    public class USILInputOutput
    {
        /// <summary>
        /// The HLSL data type (e.g., "float4", "int", "float2").
        /// </summary>
        public string format = "float4";

        /// <summary>
        /// The semantic type (e.g., "SV_POSITION", "TEXCOORD0").
        /// </summary>
        public string type;

        /// <summary>
        /// The variable name (e.g., "v0", "o1", "u_xlat0").
        /// </summary>
        public string name;

        /// <summary>
        /// The hardware register index.
        /// </summary>
        public int register;

        /// <summary>
        /// The component mask (e.g., 1 for .y, 15 for .xyzw).
        /// </summary>
        public int mask;

        /// <summary>
        /// Indicates if this is an output from the shader function (vs Input).
        /// </summary>
        public bool isOutput;
    }
}
