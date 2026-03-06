using AssetRipper.Export.Modules.Shaders.UltraShaderConverter.UShader.Function;
using System;
using USCSandbox.Processor;
using static AssetRipper.Export.Modules.Shaders.UltraShaderConverter.USIL.USILOptimizerUtil;

namespace AssetRipper.Export.Modules.Shaders.UltraShaderConverter.USIL.Optimizers
{
    /// <summary>
    /// Converts multiple multiply operations into a single matrix one
    /// "instruction"
    /// </summary>
    /// <remarks>
    /// Note: cbuffers must be converted to matrix type by this point.
    /// It's a miracle when this works. There's so many issues with how this works fundamentally.
    /// </remarks>
    public class USILMatrixMulOptimizer : IUSILOptimizer
    {
        private static readonly int[] XYZW_MASK = new int[] { 0, 1, 2, 3 };
        private static readonly int[] XXXX_MASK = new int[] { 0, 0, 0, 0 };
        private static readonly int[] YYYY_MASK = new int[] { 1, 1, 1, 1 };
        private static readonly int[] ZZZZ_MASK = new int[] { 2, 2, 2, 2 };
        private static readonly int[] WWWW_MASK = new int[] { 3, 3, 3, 3 };

        private static readonly int[] XYZ_MASK = new int[] { 0, 1, 2 };
        private static readonly int[] XXX_MASK = new int[] { 0, 0, 0 };
        private static readonly int[] YYY_MASK = new int[] { 1, 1, 1 };
        private static readonly int[] ZZZ_MASK = new int[] { 2, 2, 2 };

        public bool Run(UShaderProgram shader, ShaderParams shaderParams)
        {
            bool changes = false;

            changes |= ReplaceDotProductMatrixVec4(shader);
            changes |= ReplaceMulMatrixVec4W1(shader);
            changes |= ReplaceMulMatrixVec4(shader);
            changes |= ReplaceMulMatrixVec3(shader);

            return changes;
        }

        // dp4 sequence -> mul(matrix, vector) / mul(vector, matrix)
        private static bool ReplaceDotProductMatrixVec4(UShaderProgram shader)
        {
            bool changes = false;
            List<USILInstruction> insts = shader.instructions;

            for (int i = 0; i < insts.Count - 3; i++)
            {
                if (!DoOpcodesMatch(insts, i, new[]
                {
                    USILInstructionType.DotProduct4,
                    USILInstructionType.DotProduct4,
                    USILInstructionType.DotProduct4,
                    USILInstructionType.DotProduct4
                }))
                {
                    continue;
                }

                USILInstruction inst0 = insts[i];
                USILInstruction inst1 = insts[i + 1];
                USILInstruction inst2 = insts[i + 2];
                USILInstruction inst3 = insts[i + 3];

                if (inst0.destOperand == null || inst1.destOperand == null || inst2.destOperand == null || inst3.destOperand == null)
                {
                    continue;
                }

                bool destinationChainMatches =
                    DoMasksMatch(inst0.destOperand, new[] { 0 }) &&
                    DoMasksMatch(inst1.destOperand, new[] { 1 }) &&
                    DoMasksMatch(inst2.destOperand, new[] { 2 }) &&
                    DoMasksMatch(inst3.destOperand, new[] { 3 }) &&
                    AreSameOperandBaseWithoutMask(inst0.destOperand, inst1.destOperand) &&
                    AreSameOperandBaseWithoutMask(inst0.destOperand, inst2.destOperand) &&
                    AreSameOperandBaseWithoutMask(inst0.destOperand, inst3.destOperand);

                if (!destinationChainMatches)
                {
                    continue;
                }

                if (TryBuildMatrixVectorOperands(inst0, inst1, inst2, inst3, 0, 1, out USILOperand matrix, out USILOperand vector) ||
                    TryBuildMatrixVectorOperands(inst0, inst1, inst2, inst3, 1, 0, out matrix, out vector))
                {
                    USILInstruction mulInstruction = new USILInstruction()
                    {
                        instructionType = USILInstructionType.MultiplyMatrixByVector,
                        destOperand = new USILOperand(inst0.destOperand),
                        srcOperands = new List<USILOperand> { matrix, vector },
                        saturate = inst3.saturate,
                        commented = inst0.commented || inst1.commented || inst2.commented || inst3.commented
                    };

                    insts.RemoveRange(i, 4);
                    insts.Insert(i, mulInstruction);
                    changes = true;
                }
            }

            return changes;
        }

        // mat4x4 * vec4(vec3, 1)
        private static bool ReplaceMulMatrixVec4W1(UShaderProgram shader)
        {
            bool changes = false;

            List<USILInstruction> insts = shader.instructions;
            for (int i = 0; i < insts.Count - 3; i++)
            {
                // do detection

                bool opcodesMatch = DoOpcodesMatch(insts, i, new[] {
                    USILInstructionType.Multiply,
                    USILInstructionType.MultiplyAdd,
                    USILInstructionType.MultiplyAdd,
                    USILInstructionType.Add
                });

                if (!opcodesMatch)
                {
                    continue;
                }

                USILInstruction inst0 = insts[i];
                USILInstruction inst1 = insts[i + 1];
                USILInstruction inst2 = insts[i + 2];
                USILInstruction inst3 = insts[i + 3];

                bool matricesCorrect =
                    inst0.srcOperands[1].operandType == USILOperandType.Matrix &&
                    inst0.srcOperands[1].arrayIndex == 1 &&
                    DoMasksMatch(inst0.srcOperands[1], XYZW_MASK) &&

                    inst1.srcOperands[0].operandType == USILOperandType.Matrix &&
                    inst1.srcOperands[0].arrayIndex == 0 &&
                    DoMasksMatch(inst1.srcOperands[0], XYZW_MASK) &&

                    inst2.srcOperands[0].operandType == USILOperandType.Matrix &&
                    inst2.srcOperands[0].arrayIndex == 2 &&
                    DoMasksMatch(inst2.srcOperands[0], XYZW_MASK) &&

                    inst3.srcOperands[1].operandType == USILOperandType.Matrix &&
                    inst3.srcOperands[1].arrayIndex == 3 &&
                    DoMasksMatch(inst3.srcOperands[1], XYZW_MASK);

                if (!matricesCorrect)
                {
                    continue;
                }

                int tmp0Index = inst0.destOperand.registerIndex;
                int tmp1Index = inst1.destOperand.registerIndex;
                int tmp2Index = inst2.destOperand.registerIndex;
                int tmp3Index = inst3.destOperand.registerIndex;

                // registers can swap halfway through to be used for something else
                // don't try to convert the matrix because we can't handle this yet
                if (tmp0Index != tmp1Index || tmp1Index != tmp2Index || tmp2Index != tmp3Index)
                {
                    continue;
                }

                bool tempRegisterCorrect =
                    inst0.destOperand.registerIndex == tmp0Index &&
                    inst1.destOperand.registerIndex == tmp0Index &&
                    inst1.srcOperands[2].registerIndex == tmp0Index &&
                    inst2.srcOperands[2].registerIndex == tmp0Index &&

                    inst2.destOperand.registerIndex == tmp1Index &&
                    inst3.srcOperands[0].registerIndex == tmp1Index;

                if (!tempRegisterCorrect)
                {
                    continue;
                }

                // todo: input isn't guaranteed temp
                // todo: is input guaranteed to start at x?
                int inpIndex = inst0.srcOperands[0].registerIndex;
                bool inputsCorrect =
                    inst0.srcOperands[0].registerIndex == inpIndex &&
                    DoMasksMatch(inst0.srcOperands[0], YYYY_MASK) &&

                    inst1.srcOperands[1].registerIndex == inpIndex &&
                    DoMasksMatch(inst1.srcOperands[1], XXXX_MASK) &&

                    inst2.srcOperands[1].registerIndex == inpIndex &&
                    DoMasksMatch(inst2.srcOperands[1], ZZZZ_MASK);

                if (!inputsCorrect)
                {
                    continue;
                }

                // make replacement

                USILOperand mulInputVec3Operand = new USILOperand(inst0.srcOperands[0]);
                USILOperand mulInputMat4x4Operand = new USILOperand(inst0.srcOperands[1]);
                USILOperand mulOutputOperand = new USILOperand(inst3.destOperand);

                mulInputMat4x4Operand.displayMask = false;
                mulInputVec3Operand.mask = new int[] { 0, 1, 2 };

                USILOperand mulInput1Operand = new USILOperand()
                {
                    operandType = USILOperandType.ImmediateFloat,
                    immValueFloat = new[] { 1f },
                };

                USILOperand mulInputVec4Operand = new USILOperand()
                {
                    operandType = USILOperandType.Multiple,
                    children = new[] { mulInputVec3Operand, mulInput1Operand }
                };

                USILInstruction mulInstruction = new USILInstruction()
                {
                    instructionType = USILInstructionType.MultiplyMatrixByVector,
                    destOperand = mulOutputOperand,
                    srcOperands = new List<USILOperand> { mulInputMat4x4Operand, mulInputVec4Operand }
                };

                insts.RemoveRange(i, 4);
                insts.Insert(i, mulInstruction);

                changes = true;
            }
            return changes;
        }

        // mat4x4 * vec4
        private static bool ReplaceMulMatrixVec4(UShaderProgram shader)
        {
            bool changes = false;

            List<USILInstruction> insts = shader.instructions;
            for (int i = 0; i < insts.Count - 3; i++)
            {
                // do detection

                bool opcodesMatch = DoOpcodesMatch(insts, i, new[] {
                    USILInstructionType.Multiply,
                    USILInstructionType.MultiplyAdd,
                    USILInstructionType.MultiplyAdd,
                    USILInstructionType.MultiplyAdd
                });

                if (!opcodesMatch)
                {
                    continue;
                }

                USILInstruction inst0 = insts[i];
                USILInstruction inst1 = insts[i + 1];
                USILInstruction inst2 = insts[i + 2];
                USILInstruction inst3 = insts[i + 3];

                bool matricesCorrect =
                    inst0.srcOperands[1].operandType == USILOperandType.Matrix &&
                    inst0.srcOperands[1].arrayIndex == 1 &&
                    DoMasksMatch(inst0.srcOperands[1], XYZW_MASK) &&

                    inst1.srcOperands[0].operandType == USILOperandType.Matrix &&
                    inst1.srcOperands[0].arrayIndex == 0 &&
                    DoMasksMatch(inst1.srcOperands[0], XYZW_MASK) &&

                    inst2.srcOperands[0].operandType == USILOperandType.Matrix &&
                    inst2.srcOperands[0].arrayIndex == 2 &&
                    DoMasksMatch(inst2.srcOperands[0], XYZW_MASK) &&

                    inst3.srcOperands[0].operandType == USILOperandType.Matrix &&
                    inst3.srcOperands[0].arrayIndex == 3 &&
                    DoMasksMatch(inst3.srcOperands[0], XYZW_MASK);

                if (!matricesCorrect)
                {
                    continue;
                }

                int tmp0Index = inst0.destOperand.registerIndex;
                int tmp1Index = inst1.destOperand.registerIndex;
                int tmp2Index = inst2.destOperand.registerIndex;
                int tmp3Index = inst3.destOperand.registerIndex;

                // registers can swap halfway through to be used for something else
                // don't try to convert the matrix because we can't handle this yet
                if (tmp0Index != tmp1Index || tmp1Index != tmp2Index || tmp2Index != tmp3Index)
                {
                    continue;
                }

                int tmpIndex = inst0.destOperand.registerIndex;
                bool tempRegisterCorrect =
                    inst0.destOperand.registerIndex == tmpIndex &&

                    inst1.destOperand.registerIndex == tmpIndex &&
                    inst1.srcOperands[2].registerIndex == tmpIndex &&

                    inst2.destOperand.registerIndex == tmpIndex &&
                    inst2.srcOperands[2].registerIndex == tmpIndex &&

                    inst3.srcOperands[2].registerIndex == tmpIndex;

                if (!tempRegisterCorrect)
                {
                    continue;
                }

                // todo: input isn't guaranteed temp
                int inpIndex = inst0.srcOperands[0].registerIndex;
                bool inputsCorrect =
                    inst0.srcOperands[0].registerIndex == inpIndex &&
                    DoMasksMatch(inst0.srcOperands[0], YYYY_MASK) &&

                    inst1.srcOperands[1].registerIndex == inpIndex &&
                    DoMasksMatch(inst1.srcOperands[1], XXXX_MASK) &&

                    inst2.srcOperands[1].registerIndex == inpIndex &&
                    DoMasksMatch(inst2.srcOperands[1], ZZZZ_MASK) &&

                    inst3.srcOperands[1].registerIndex == inpIndex &&
                    DoMasksMatch(inst3.srcOperands[1], WWWW_MASK);

                if (!inputsCorrect)
                {
                    continue;
                }

                // make replacement

                USILOperand mulInputVec4Operand = new USILOperand(inst0.srcOperands[0]);
                USILOperand mulInputMat4x4Operand = new USILOperand(inst0.srcOperands[1]);
                USILOperand mulOutputOperand = new USILOperand(inst3.destOperand);

                mulInputMat4x4Operand.displayMask = false;
                mulInputVec4Operand.mask = new int[] { 0, 1, 2, 3 };

                USILInstruction mulInstruction = new USILInstruction()
                {
                    instructionType = USILInstructionType.MultiplyMatrixByVector,
                    destOperand = mulOutputOperand,
                    srcOperands = new List<USILOperand> { mulInputMat4x4Operand, mulInputVec4Operand }
                };

                insts.RemoveRange(i, 4);
                insts.Insert(i, mulInstruction);

                changes = true;
            }
            return changes;
        }

        // mat3x3 * vec3
        private static bool ReplaceMulMatrixVec3(UShaderProgram shader)
        {

            bool changes = false;

            List<USILInstruction> insts = shader.instructions;
            for (int i = 0; i < insts.Count - 3; i++)
            {
                // do detection

                bool opcodesMatch = DoOpcodesMatch(insts, i, new[] {
                    USILInstructionType.Multiply,
                    USILInstructionType.MultiplyAdd,
                    USILInstructionType.MultiplyAdd,
                    USILInstructionType.Add
                });

                if (!opcodesMatch)
                {
                    continue;
                }

                USILInstruction inst0 = insts[i];
                USILInstruction inst1 = insts[i + 1];
                USILInstruction inst2 = insts[i + 2];
                USILInstruction inst3 = insts[i + 3];

                bool matricesCorrect =
                    inst0.srcOperands[1].operandType == USILOperandType.Matrix &&
                    inst0.srcOperands[1].arrayIndex == 1 &&
                    DoMasksMatch(inst0.srcOperands[1], XYZ_MASK) &&

                    inst1.srcOperands[0].operandType == USILOperandType.Matrix &&
                    inst1.srcOperands[0].arrayIndex == 0 &&
                    DoMasksMatch(inst1.srcOperands[0], XYZ_MASK) &&

                    inst2.srcOperands[0].operandType == USILOperandType.Matrix &&
                    inst2.srcOperands[0].arrayIndex == 2 &&
                    DoMasksMatch(inst2.srcOperands[0], XYZ_MASK) &&

                    inst3.srcOperands[1].operandType == USILOperandType.Matrix &&
                    inst3.srcOperands[1].arrayIndex == 3 &&
                    DoMasksMatch(inst3.srcOperands[1], XYZ_MASK);

                if (!matricesCorrect)
                {
                    continue;
                }

                int tmp0Index = inst0.destOperand.registerIndex;
                int tmp1Index = inst1.destOperand.registerIndex;
                int tmp2Index = inst2.destOperand.registerIndex;
                int tmp3Index = inst3.destOperand.registerIndex;

                // registers can swap halfway through to be used for something else
                // don't try to convert the matrix because we can't handle this yet
                if (tmp0Index != tmp1Index || tmp1Index != tmp2Index || tmp2Index != tmp3Index)
                {
                    continue;
                }

                bool tempRegisterCorrect =
                    inst0.destOperand.registerIndex == tmp0Index &&
                    inst1.destOperand.registerIndex == tmp0Index &&
                    inst1.srcOperands[2].registerIndex == tmp0Index &&
                    inst2.srcOperands[2].registerIndex == tmp0Index &&

                    inst2.destOperand.registerIndex == tmp1Index &&
                    inst3.srcOperands[0].registerIndex == tmp1Index;

                if (!tempRegisterCorrect)
                {
                    continue;
                }

                // todo: input isn't guaranteed temp
                // todo: is input guaranteed to start at x?
                int inpIndex = inst0.srcOperands[0].registerIndex;
                bool inputsCorrect =
                    inst0.srcOperands[0].registerIndex == inpIndex &&
                    DoMasksMatch(inst0.srcOperands[0], YYY_MASK) &&

                    inst1.srcOperands[1].registerIndex == inpIndex &&
                    DoMasksMatch(inst1.srcOperands[1], XXX_MASK) &&

                    inst2.srcOperands[1].registerIndex == inpIndex &&
                    DoMasksMatch(inst2.srcOperands[1], ZZZ_MASK);

                if (!inputsCorrect)
                {
                    continue;
                }

                // make replacement

                USILOperand mulInputVec3Operand = new USILOperand(inst0.srcOperands[0]);
                USILOperand mulInputMat3x3Operand = new USILOperand(inst0.srcOperands[1]);
                USILOperand mulOutputOperand = new USILOperand(inst3.destOperand);

                mulInputMat3x3Operand.displayMask = false;
                mulInputVec3Operand.mask = new int[] { 0, 1, 2 };

                USILInstruction mulInstruction = new USILInstruction()
                {
                    instructionType = USILInstructionType.MultiplyMatrixByVector,
                    destOperand = mulOutputOperand,
                    srcOperands = new List<USILOperand> { mulInputMat3x3Operand, mulInputVec3Operand }
                };

                insts.RemoveRange(i, 4);
                insts.Insert(i, mulInstruction);

                changes = true;
            }
            return changes;
        }

        private static bool TryBuildMatrixVectorOperands(
            USILInstruction inst0,
            USILInstruction inst1,
            USILInstruction inst2,
            USILInstruction inst3,
            int matrixOperandIndex,
            int vectorOperandIndex,
            out USILOperand matrixOperand,
            out USILOperand vectorOperand)
        {
            matrixOperand = default!;
            vectorOperand = default!;

            USILInstruction[] chain = { inst0, inst1, inst2, inst3 };
            int maxOperandIndex = Math.Max(matrixOperandIndex, vectorOperandIndex);
            foreach (USILInstruction inst in chain)
            {
                if (inst.srcOperands.Count <= maxOperandIndex)
                {
                    return false;
                }
            }

            USILOperand firstMatrix = chain[0].srcOperands[matrixOperandIndex];
            USILOperand firstVector = chain[0].srcOperands[vectorOperandIndex];
            if (firstMatrix.operandType != USILOperandType.Matrix || !DoMasksMatch(firstMatrix, XYZW_MASK))
            {
                return false;
            }
            if (!DoMasksMatch(firstVector, XYZW_MASK))
            {
                return false;
            }

            int baseMatrixIndex = firstMatrix.arrayIndex;
            for (int i = 0; i < chain.Length; i++)
            {
                USILOperand matrix = chain[i].srcOperands[matrixOperandIndex];
                USILOperand vector = chain[i].srcOperands[vectorOperandIndex];

                bool matrixMatches =
                    matrix.operandType == USILOperandType.Matrix &&
                    matrix.registerIndex == firstMatrix.registerIndex &&
                    matrix.arrayIndex == baseMatrixIndex + i &&
                    matrix.transposeMatrix == firstMatrix.transposeMatrix &&
                    matrix.metadataNameAssigned == firstMatrix.metadataNameAssigned &&
                    string.Equals(matrix.metadataName, firstMatrix.metadataName, StringComparison.Ordinal) &&
                    DoMasksMatch(matrix, XYZW_MASK);

                if (!matrixMatches)
                {
                    return false;
                }

                if (!DoMasksMatch(vector, XYZW_MASK) ||
                    !AreSameOperandBaseWithoutMask(firstVector, vector))
                {
                    return false;
                }
            }

            matrixOperand = new USILOperand(firstMatrix);
            matrixOperand.displayMask = false;
            matrixOperand.arrayIndex = baseMatrixIndex;
            matrixOperand.mask = XYZW_MASK;

            vectorOperand = new USILOperand(firstVector);
            vectorOperand.mask = XYZW_MASK;
            return true;
        }

        private static bool AreSameOperandBaseWithoutMask(USILOperand left, USILOperand right)
        {
            USILOperand a = new USILOperand(left)
            {
                displayMask = false,
                negative = false,
                absoluteValue = false
            };
            USILOperand b = new USILOperand(right)
            {
                displayMask = false,
                negative = false,
                absoluteValue = false
            };
            return string.Equals(a.ToString(true), b.ToString(true), StringComparison.Ordinal);
        }
    }
}
