using AssetRipper.Export.Modules.Shaders.UltraShaderConverter.UShader.Function;
using USCSandbox.Processor;

namespace AssetRipper.Export.Modules.Shaders.UltraShaderConverter.USIL.Optimizers
{
    public class USILHighLevelMathOptimizer : IUSILOptimizer
    {
        public bool Run(UShaderProgram shader, ShaderParams shaderData)
        {
            bool changes = false;
            changes |= ReplaceLerp(shader);
            changes |= ReplaceNormalize(shader);
            changes |= ReplaceLength(shader);
            changes |= ReplaceUnityObjectToClipPos(shader);
            changes |= ReplaceUnityObjectToWorldNormal(shader);
            return changes;
        }

        private static bool ReplaceLerp(UShaderProgram shader)
        {
            bool changes = false;
            List<USILInstruction> insts = shader.instructions;
            for (int i = 0; i < insts.Count - 1; i++)
            {
                USILInstruction first = insts[i];
                USILInstruction second = insts[i + 1];
                if (first.destOperand == null || second.destOperand == null)
                {
                    continue;
                }
                if (second.instructionType != USILInstructionType.MultiplyAdd || second.srcOperands.Count < 3)
                {
                    continue;
                }
                if (!AreSameOperandBase(second.srcOperands[1], first.destOperand))
                {
                    continue;
                }
                if (!TryResolveLerpOperands(first, second, out USILOperand a, out USILOperand b, out USILOperand t))
                {
                    continue;
                }

                USILInstruction lerp = new USILInstruction
                {
                    instructionType = USILInstructionType.Lerp,
                    destOperand = new USILOperand(second.destOperand),
                    srcOperands = new List<USILOperand>
                    {
                        a,
                        b,
                        t,
                    },
                    saturate = second.saturate,
                    commented = second.commented,
                    isIntVariant = second.isIntVariant,
                    isIntUnsigned = second.isIntUnsigned,
                };

                insts.RemoveRange(i, 2);
                insts.Insert(i, lerp);
                changes = true;
            }
            return changes;
        }

        private static bool TryResolveLerpOperands(USILInstruction first, USILInstruction second, out USILOperand a, out USILOperand b, out USILOperand t)
        {
            a = default!;
            b = default!;
            t = default!;

            if (second.srcOperands.Count < 3)
            {
                return false;
            }

            // Pattern: (1 - t) * a + t * b represented as add temp, -a, b; mad dest, t, temp, a
            if (first.instructionType == USILInstructionType.Add && first.srcOperands.Count >= 2)
            {
                USILOperand negA = first.srcOperands[0];
                USILOperand bOperand = first.srcOperands[1];
                USILOperand aOperand = WithoutNegation(negA);
                if (negA.negative && AreSameOperandBase(second.srcOperands[2], aOperand))
                {
                    a = new USILOperand(aOperand);
                    b = new USILOperand(bOperand);
                    t = new USILOperand(second.srcOperands[0]);
                    return true;
                }
            }

            // Pattern: a + t * (b - a) represented as sub temp, b, a; mad dest, t, temp, a
            if (first.instructionType == USILInstructionType.Subtract && first.srcOperands.Count >= 2)
            {
                USILOperand bOperand = first.srcOperands[0];
                USILOperand aOperand = first.srcOperands[1];
                if (AreSameOperandBase(second.srcOperands[2], aOperand))
                {
                    a = new USILOperand(aOperand);
                    b = new USILOperand(bOperand);
                    t = new USILOperand(second.srcOperands[0]);
                    return true;
                }
            }

            return false;
        }

        private static bool ReplaceNormalize(UShaderProgram shader)
        {
            bool changes = false;
            List<USILInstruction> insts = shader.instructions;
            for (int i = 0; i < insts.Count - 2; i++)
            {
                USILInstruction dot = insts[i];
                USILInstruction rsq = insts[i + 1];
                USILInstruction mul = insts[i + 2];
                if (dot.destOperand == null || rsq.destOperand == null || mul.destOperand == null)
                {
                    continue;
                }
                if ((dot.instructionType != USILInstructionType.DotProduct3 && dot.instructionType != USILInstructionType.DotProduct4)
                    || rsq.instructionType != USILInstructionType.SquareRootReciprocal
                    || mul.instructionType != USILInstructionType.Multiply)
                {
                    continue;
                }
                if (dot.srcOperands.Count < 2 || rsq.srcOperands.Count < 1 || mul.srcOperands.Count < 2)
                {
                    continue;
                }
                if (!AreSameOperandBase(dot.srcOperands[0], dot.srcOperands[1]))
                {
                    continue;
                }
                if (!AreSameOperandBase(rsq.srcOperands[0], dot.destOperand))
                {
                    continue;
                }

                USILOperand? vectorOperand = null;
                USILOperand? rsqOperand = null;
                foreach (USILOperand source in mul.srcOperands)
                {
                    if (vectorOperand == null && AreSameOperandBase(source, dot.srcOperands[0]))
                    {
                        vectorOperand = source;
                        continue;
                    }
                    if (rsqOperand == null && AreSameOperandBase(source, rsq.destOperand))
                    {
                        rsqOperand = source;
                    }
                }
                if (vectorOperand == null || rsqOperand == null)
                {
                    continue;
                }

                USILInstruction normalize = new USILInstruction
                {
                    instructionType = USILInstructionType.Normalize,
                    destOperand = new USILOperand(mul.destOperand),
                    srcOperands = new List<USILOperand> { new USILOperand(vectorOperand) },
                    saturate = mul.saturate,
                    commented = mul.commented,
                    isIntVariant = mul.isIntVariant,
                    isIntUnsigned = mul.isIntUnsigned,
                };

                insts.RemoveRange(i, 3);
                insts.Insert(i, normalize);
                changes = true;
            }
            return changes;
        }

        private static bool ReplaceLength(UShaderProgram shader)
        {
            bool changes = false;
            List<USILInstruction> insts = shader.instructions;
            for (int i = 0; i < insts.Count - 1; i++)
            {
                USILInstruction dot = insts[i];
                USILInstruction sqrt = insts[i + 1];
                if (dot.destOperand == null || sqrt.destOperand == null)
                {
                    continue;
                }
                if ((dot.instructionType != USILInstructionType.DotProduct3 && dot.instructionType != USILInstructionType.DotProduct4)
                    || sqrt.instructionType != USILInstructionType.SquareRoot)
                {
                    continue;
                }
                if (dot.srcOperands.Count < 2 || sqrt.srcOperands.Count < 1)
                {
                    continue;
                }
                if (!AreSameOperandBase(dot.srcOperands[0], dot.srcOperands[1]))
                {
                    continue;
                }
                if (!AreSameOperandBase(sqrt.srcOperands[0], dot.destOperand))
                {
                    continue;
                }

                USILInstruction length = new USILInstruction
                {
                    instructionType = USILInstructionType.Length,
                    destOperand = new USILOperand(sqrt.destOperand),
                    srcOperands = new List<USILOperand> { new USILOperand(dot.srcOperands[0]) },
                    saturate = sqrt.saturate,
                    commented = sqrt.commented,
                    isIntVariant = sqrt.isIntVariant,
                    isIntUnsigned = sqrt.isIntUnsigned,
                };

                insts.RemoveRange(i, 2);
                insts.Insert(i, length);
                changes = true;
            }
            return changes;
        }

        private static bool ReplaceUnityObjectToClipPos(UShaderProgram shader)
        {
            bool changes = false;
            List<USILInstruction> insts = shader.instructions;
            for (int i = 0; i < insts.Count - 1; i++)
            {
                USILInstruction worldMul = insts[i];
                USILInstruction clipMul = insts[i + 1];
                if (worldMul.destOperand == null || clipMul.destOperand == null)
                {
                    continue;
                }
                if (worldMul.instructionType != USILInstructionType.MultiplyMatrixByVector
                    || clipMul.instructionType != USILInstructionType.MultiplyMatrixByVector)
                {
                    continue;
                }
                if (worldMul.srcOperands.Count < 2 || clipMul.srcOperands.Count < 2)
                {
                    continue;
                }

                if (!IsUnityMatrixOperand(worldMul.srcOperands[0], "unity_objecttoworld"))
                {
                    continue;
                }
                if (!IsUnityMatrixOperand(clipMul.srcOperands[0], "unity_matrixvp"))
                {
                    continue;
                }
                if (!AreSameOperandBase(clipMul.srcOperands[1], worldMul.destOperand))
                {
                    continue;
                }

                USILInstruction macro = new USILInstruction
                {
                    instructionType = USILInstructionType.UnityObjectToClipPos,
                    destOperand = new USILOperand(clipMul.destOperand),
                    srcOperands = new List<USILOperand> { new USILOperand(worldMul.srcOperands[1]) },
                    saturate = clipMul.saturate,
                    commented = clipMul.commented,
                    isIntVariant = clipMul.isIntVariant,
                    isIntUnsigned = clipMul.isIntUnsigned,
                };

                insts.RemoveRange(i, 2);
                insts.Insert(i, macro);
                changes = true;
            }
            return changes;
        }

        private static bool ReplaceUnityObjectToWorldNormal(UShaderProgram shader)
        {
            bool changes = false;
            List<USILInstruction> insts = shader.instructions;
            for (int i = 0; i < insts.Count; i++)
            {
                USILInstruction inst = insts[i];
                if (inst.destOperand == null || inst.instructionType != USILInstructionType.MultiplyMatrixByVector)
                {
                    continue;
                }
                if (inst.srcOperands.Count < 2)
                {
                    continue;
                }
                if (!IsUnityMatrixOperand(inst.srcOperands[0], "unity_worldtoobject"))
                {
                    continue;
                }

                USILInstruction macro = new USILInstruction
                {
                    instructionType = USILInstructionType.UnityObjectToWorldNormal,
                    destOperand = new USILOperand(inst.destOperand),
                    srcOperands = new List<USILOperand> { new USILOperand(inst.srcOperands[1]) },
                    saturate = inst.saturate,
                    commented = inst.commented,
                    isIntVariant = inst.isIntVariant,
                    isIntUnsigned = inst.isIntUnsigned,
                };

                insts[i] = macro;
                changes = true;
            }
            return changes;
        }

        private static USILOperand WithoutNegation(USILOperand operand)
        {
            USILOperand copy = new USILOperand(operand)
            {
                negative = false,
                absoluteValue = false,
            };
            return copy;
        }

        private static bool AreSameOperandBase(USILOperand left, USILOperand right)
        {
            USILOperand a = new USILOperand(left)
            {
                negative = false,
                absoluteValue = false,
                displayMask = false,
            };
            USILOperand b = new USILOperand(right)
            {
                negative = false,
                absoluteValue = false,
                displayMask = false,
            };
            return string.Equals(a.ToString(true), b.ToString(true), StringComparison.Ordinal);
        }

        private static bool IsUnityMatrixOperand(USILOperand operand, string token)
        {
            string text = operand.metadataName ?? operand.ToString(true);
            return text.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;
        }
    }
}
