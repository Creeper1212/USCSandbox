using AssetRipper.Export.Modules.Shaders.UltraShaderConverter.UShader.Function;
using AssetRipper.Export.Modules.Shaders.UltraShaderConverter.USIL;
using Ryujinx.Graphics.Shader.Translation;

namespace USCSandbox.UltraShaderConverter.UShader.NVN
{
    public class NvnProgramToUSIL
    {
        private readonly TranslatorContext _nvnShader;

        public UShaderProgram shader;

        public NvnProgramToUSIL(TranslatorContext nvnShader)
        {
            _nvnShader = nvnShader;
            shader = new UShaderProgram();
        }

        public void Convert()
        {
            shader.instructions.Add(new USILInstruction
            {
                instructionType = USILInstructionType.Comment,
                destOperand = new USILOperand
                {
                    operandType = USILOperandType.Comment,
                    comment = "NVN translation is unavailable in this build."
                },
                srcOperands = new List<USILOperand>()
            });
        }
    }
}
