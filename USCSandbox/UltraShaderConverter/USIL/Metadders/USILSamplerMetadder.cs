using AssetRipper.Export.Modules.Shaders.UltraShaderConverter.UShader.Function;
using USCSandbox.Processor;

namespace AssetRipper.Export.Modules.Shaders.UltraShaderConverter.USIL.Metadders
{
    public class USILSamplerMetadder : IUSILOptimizer
    {
        public bool Run(UShaderProgram shader, ShaderParams shaderData)
        {
            List<USILInstruction> instructions = shader.instructions;
            foreach (USILInstruction instruction in instructions)
            {
                if (instruction.destOperand != null)
                {
                    HandleOperand(instruction.destOperand, shaderData);
                }

                foreach (USILOperand operand in instruction.srcOperands)
                {
                    HandleOperand(operand, shaderData);
                }
            }

            return true;
        }

        private static void HandleOperand(USILOperand operand, ShaderParams shaderData)
        {
            if (operand.operandType == USILOperandType.SamplerRegister)
            {
                TextureParameter? texParam = shaderData.TextureParameters.FirstOrDefault(
                    p => p.SamplerIndex == operand.registerIndex
                );

                if (texParam == null)
                {
                    texParam = shaderData.TextureParameters.FirstOrDefault(
                        p => p.SamplerIndex == -1 && p.Index == operand.registerIndex
                    );
                }

                if (texParam == null)
                {
                    return;
                }

                operand.metadataName = texParam.Name;
                operand.metadataNameAssigned = true;

                switch (texParam.Dim)
                {
                    case 2:
                        operand.operandType = USILOperandType.Sampler2D;
                        break;
                    case 3:
                        operand.operandType = USILOperandType.Sampler3D;
                        break;
                    case 4:
                        operand.operandType = USILOperandType.SamplerCube;
                        break;
                    case 5:
                        operand.operandType = USILOperandType.Sampler2DArray;
                        break;
                    case 6:
                        operand.operandType = USILOperandType.SamplerCubeArray;
                        break;
                    default:
                        operand.operandType = USILOperandType.Sampler2D;
                        break;
                }
            }
            else if (operand.operandType == USILOperandType.ResourceRegister)
            {
                TextureParameter? texParam = shaderData.TextureParameters.FirstOrDefault(
                    p => p.Index == operand.registerIndex
                );
                if (texParam != null)
                {
                    operand.metadataName = texParam.Name;
                    operand.metadataNameAssigned = true;
                    return;
                }

                BufferBinding? buffer = shaderData.Buffers.FirstOrDefault(
                    p => p.Index == operand.registerIndex
                );
                if (buffer != null)
                {
                    operand.metadataName = buffer.Name;
                    operand.metadataNameAssigned = true;
                    return;
                }

                UAVParameter? uav = shaderData.UAVs.FirstOrDefault(
                    p => p.Index == operand.registerIndex || p.OriginalIndex == operand.registerIndex
                );
                if (uav != null)
                {
                    operand.metadataName = uav.Name;
                    operand.metadataNameAssigned = true;
                }
            }
        }
    }
}
