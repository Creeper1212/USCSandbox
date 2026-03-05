using AssetRipper.Export.Modules.Shaders.UltraShaderConverter.USIL;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AssetRipper.Export.Modules.Shaders.UltraShaderConverter.UShader.Function
{
    public class UShaderFunctionToHLSL
    {
        private UShaderProgram _shader;
        private readonly StringBuilder _stringBuilder = new StringBuilder();
        private string _baseIndent;
        private string _indent;
        private int _indentLevel;

        private delegate void InstHandler(USILInstruction inst);
        private Dictionary<USILInstructionType, InstHandler> _instructionHandlers;

        public UShaderFunctionToHLSL(UShaderProgram shader, int indentDepth)
        {
            _shader = shader;

            _baseIndent = new string(' ', indentDepth * 4);
            _indent = new string(' ', 4);

            _instructionHandlers = new Dictionary<USILInstructionType, InstHandler>
            {
                { USILInstructionType.Move, HandleMove },
                { USILInstructionType.MoveConditional, HandleMoveConditional },
                { USILInstructionType.Add, HandleAdd },
                { USILInstructionType.Subtract, HandleSubtract },
                { USILInstructionType.Multiply, HandleMultiply },
                { USILInstructionType.Divide, HandleDivide },
                { USILInstructionType.MultiplyAdd, HandleMultiplyAdd },
                { USILInstructionType.And, HandleAnd },
                { USILInstructionType.Or, HandleOr },
                { USILInstructionType.Xor, HandleXor },
                { USILInstructionType.Not, HandleNot },
                { USILInstructionType.BitFieldInsert, HandleBitFieldInsert },
                { USILInstructionType.BitFieldExtractUnsigned, HandleBitFieldExtractUnsigned },
                { USILInstructionType.BitFieldExtractSigned, HandleBitFieldExtractSigned },
                { USILInstructionType.Minimum, HandleMinimum },
                { USILInstructionType.Maximum, HandleMaximum },
                { USILInstructionType.SquareRoot, HandleSquareRoot },
                { USILInstructionType.SquareRootReciprocal, HandleSquareRootReciprocal },
                { USILInstructionType.Logarithm2, HandleLogarithm2 },
                { USILInstructionType.ToThePower, HandleToThePower },
                { USILInstructionType.Reciprocal, HandleReciprocal },
                { USILInstructionType.Fractional, HandleFractional },
                { USILInstructionType.Floor, HandleFloor },
                { USILInstructionType.Ceiling, HandleCeiling },
                { USILInstructionType.Round, HandleRound },
                { USILInstructionType.Truncate, HandleTruncate },
                { USILInstructionType.IntToFloat, HandleIntToFloat },
                { USILInstructionType.UIntToFloat, HandleUIntToFloat },
                { USILInstructionType.FloatToInt, HandleFloatToInt },
                { USILInstructionType.FloatToUInt, HandleFloatToUInt },
                { USILInstructionType.Negate, HandleNegate },
                { USILInstructionType.Clamp, HandleClamp },
                { USILInstructionType.ClampUInt, HandleClamp },
                { USILInstructionType.Sine, HandleSine },
                { USILInstructionType.Cosine, HandleCosine },
                { USILInstructionType.ShiftLeft, HandleShiftLeft },
                { USILInstructionType.ShiftRight, HandleShiftRight },
                { USILInstructionType.DotProduct2, HandleDotProduct },
                { USILInstructionType.DotProduct3, HandleDotProduct },
                { USILInstructionType.DotProduct4, HandleDotProduct },
                { USILInstructionType.Lerp, HandleLerp },
                { USILInstructionType.Normalize, HandleNormalize },
                { USILInstructionType.Length, HandleLength },
                { USILInstructionType.Sample, HandleSample },
                { USILInstructionType.SampleComparison, HandleSample },
                { USILInstructionType.SampleComparisonLODZero, HandleSample },
                { USILInstructionType.SampleLOD, HandleSampleLOD },
                { USILInstructionType.SampleLODBias, HandleSampleLODBias },
                { USILInstructionType.SampleDerivative, HandleSampleDerivative },
                { USILInstructionType.LoadResource, HandleLoadResource },
                { USILInstructionType.LoadResourceMultisampled, HandleLoadResource },
                { USILInstructionType.LoadResourceRaw, HandleLoadResourceRaw },
                { USILInstructionType.LoadResourceStructured, HandleLoadResourceStructured },
                { USILInstructionType.Discard, HandleDiscard },
                { USILInstructionType.ResourceDimensionInfo, HandleResourceDimensionInfo },
                { USILInstructionType.SampleCountInfo, HandleSampleCountInfo },
                { USILInstructionType.GetDimensions, HandleResourceDimensionInfo },
                { USILInstructionType.DerivativeRenderTargetX, HandleDerivativeRenderTarget },
                { USILInstructionType.DerivativeRenderTargetY, HandleDerivativeRenderTarget },
                { USILInstructionType.DerivativeRenderTargetXCoarse, HandleDerivativeRenderTarget },
                { USILInstructionType.DerivativeRenderTargetYCoarse, HandleDerivativeRenderTarget },
                { USILInstructionType.DerivativeRenderTargetXFine, HandleDerivativeRenderTarget },
                { USILInstructionType.DerivativeRenderTargetYFine, HandleDerivativeRenderTarget },
                { USILInstructionType.IfFalse, HandleIf },
                { USILInstructionType.IfTrue, HandleIf },
                { USILInstructionType.Else, HandleElse },
                { USILInstructionType.EndIf, HandleEndIf },
                { USILInstructionType.Loop, HandleLoop },
                { USILInstructionType.EndLoop, HandleEndLoop },
                { USILInstructionType.Break, HandleBreak },
                { USILInstructionType.Continue, HandleContinue },
                { USILInstructionType.ForLoop, HandleForLoop },
                { USILInstructionType.Switch, HandleSwitch },
                { USILInstructionType.Case, HandleCase },
                { USILInstructionType.Default, HandleDefault },
                { USILInstructionType.EndSwitch, HandleEndSwitch },
                { USILInstructionType.Equal, HandleEqual },
                { USILInstructionType.NotEqual, HandleNotEqual },
                { USILInstructionType.LessThan, HandleLessThan },
                { USILInstructionType.LessThanOrEqual, HandleLessThanOrEqual },
                { USILInstructionType.GreaterThan, HandleGreaterThan },
                { USILInstructionType.GreaterThanOrEqual, HandleGreaterThanOrEqual },
                { USILInstructionType.Return, HandleReturn },
                { USILInstructionType.MultiplyMatrixByVector, MultiplyMatrixByVector },
                { USILInstructionType.UnityObjectToClipPos, HandleUnityObjectToClipPos },
                { USILInstructionType.UnityObjectToWorldNormal, HandleUnityObjectToWorldNormal },
                { USILInstructionType.Comment, HandleComment }
            };
        }

        public string WriteStruct()
        {
            _stringBuilder.Clear();

            if (_shader.shaderFunctionType == UShaderFunctionType.Vertex)
            {
                AppendLine("struct appdata");
                AppendLine("{");
                _indentLevel++;
                foreach (USILInputOutput input in _shader.inputs)
                {
                    string format = string.IsNullOrWhiteSpace(input.format) ? "float4" : input.format;
                    AppendLine($"{format} {input.name} : {input.type};");
                }
                _indentLevel--;
                AppendLine("};");

                AppendLine("struct v2f");
                AppendLine("{");
                _indentLevel++;
                foreach (USILInputOutput output in _shader.outputs)
                {
                    string format = string.IsNullOrWhiteSpace(output.format) ? "float4" : output.format;
                    AppendLine($"{format} {output.name} : {output.type};");
                }
                _indentLevel--;
                AppendLine("};");
            }
            else if (_shader.shaderFunctionType == UShaderFunctionType.Fragment)
            {
                AppendLine("struct fout");
                AppendLine("{");
                _indentLevel++;
                foreach (USILInputOutput output in _shader.outputs)
                {
                    string format = string.IsNullOrWhiteSpace(output.format) ? "float4" : output.format;
                    AppendLine($"{format} {output.name} : {output.type};");
                }
                _indentLevel--;
                AppendLine("};");
            }

            return _stringBuilder.ToString();
        }

        public string WriteFunction()
        {
            _stringBuilder.Clear();

            WriteFunctionDefinition();
            {
                WriteLocals();
                foreach (USILInstruction inst in _shader.instructions)
                {
                    if (_instructionHandlers.TryGetValue(inst.instructionType, out InstHandler? handler))
                    {
                        handler(inst);
                    }
                    else
                    {
                        string comment = CommentString(inst);
                        AppendLine($"{comment}// Unsupported USIL instruction: {inst.instructionType}");
                    }
                }
            }
            _indentLevel--;
            AppendLine("}");

            return _stringBuilder.ToString();
        }

        private void WriteFunctionDefinition()
        {
            if (_shader.shaderFunctionType == UShaderFunctionType.Vertex)
            {
                AppendLine($"{USILConstants.VERT_TO_FRAG_STRUCT_NAME} vert(appdata {USILConstants.VERT_INPUT_NAME})");
            }
            else
            {
                var frontFace = _shader.inputs.FirstOrDefault(i => i.type == "SV_IsFrontFace");
                string args = $"{USILConstants.VERT_TO_FRAG_STRUCT_NAME} {USILConstants.FRAG_INPUT_NAME}";
                if (frontFace != null)
                {
                    string format = string.IsNullOrWhiteSpace(frontFace.format) ? "float" : frontFace.format;
                    args += $", {format} {frontFace.name}: VFACE";
                }
                AppendLine($"{USILConstants.FRAG_OUTPUT_STRUCT_NAME} frag({args})");
            }
            AppendLine("{");
            _indentLevel++;
        }

        private void WriteLocals()
        {
            foreach (USILLocal local in _shader.locals)
            {
                if (local.defaultValues.Count > 0 && local.isArray)
                {
                    AppendLine($"{local.type} {local.name}[{local.defaultValues.Count}] = {{");
                    if (local.defaultValues.Count > 0)
                    {
                        _indentLevel++;
                        for (int i = 0; i < local.defaultValues.Count; i++)
                        {
                            USILOperand operand = local.defaultValues[i];
                            string comma = i != local.defaultValues.Count - 1 ? "," : "";
                            AppendLine($"{operand}{comma}");
                        }
                        _indentLevel--;
                    }
                    AppendLine("};");
                }
                else
                {
                    AppendLine($"{local.type} {local.name};");
                }
            }
        }

        private void HandleMove(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = WrapSaturate(inst, $"{srcOps[0]}");
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleMoveConditional(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = WrapSaturate(inst, $"{srcOps[0]} ? {srcOps[1]} : {srcOps[2]}");
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleAdd(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = WrapSaturate(inst, $"{srcOps[0]} + {srcOps[1]}");
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleSubtract(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = WrapSaturate(inst, $"{srcOps[0]} - {srcOps[1]}");
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleMultiply(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = WrapSaturate(inst, $"{srcOps[0]} * {srcOps[1]}");
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleDivide(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = WrapSaturate(inst, $"{srcOps[0]} / {srcOps[1]}");
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleMultiplyAdd(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = WrapSaturate(inst, $"{srcOps[0]} * {srcOps[1]} + {srcOps[2]}");
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleAnd(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            int op0UintSize = srcOps[0].GetValueCount();
            int op1UintSize = srcOps[1].GetValueCount();
            string value = $"uint{op0UintSize}({srcOps[0]}) & uint{op1UintSize}({srcOps[1]})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleOr(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            int op0UintSize = srcOps[0].GetValueCount();
            int op1UintSize = srcOps[1].GetValueCount();
            string value = $"uint{op0UintSize}({srcOps[0]}) | uint{op1UintSize}({srcOps[1]})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleXor(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            int op0UintSize = srcOps[0].GetValueCount();
            int op1UintSize = srcOps[1].GetValueCount();
            string value = $"uint{op0UintSize}({srcOps[0]}) ^ uint{op1UintSize}({srcOps[1]})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleNot(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            int op0UintSize = srcOps[0].GetValueCount();
            string value = $"~uint{op0UintSize}({srcOps[0]})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleBitFieldInsert(USILInstruction inst)
        {
            // bfi(width, offset, src, base)
            List<USILOperand> srcOps = inst.srcOperands;
            string width = BuildUIntCast(srcOps[0]);
            string offset = BuildUIntCast(srcOps[1]);
            string src = BuildUIntCast(srcOps[2]);
            string baseValue = BuildUIntCast(srcOps[3]);
            string mask = $"((1u << {width}) - 1u)";
            string value = $"(({baseValue} & ~({mask} << {offset})) | (({src} & {mask}) << {offset}))";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleBitFieldExtractUnsigned(USILInstruction inst)
        {
            // ubfe(width, offset, value)
            List<USILOperand> srcOps = inst.srcOperands;
            string width = BuildUIntCast(srcOps[0]);
            string offset = BuildUIntCast(srcOps[1]);
            string valueOperand = BuildUIntCast(srcOps[2]);
            string mask = $"((1u << {width}) - 1u)";
            string value = $"(({valueOperand} >> {offset}) & {mask})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleBitFieldExtractSigned(USILInstruction inst)
        {
            // ibfe(width, offset, value)
            List<USILOperand> srcOps = inst.srcOperands;
            string width = BuildUIntCast(srcOps[0]);
            string offset = BuildUIntCast(srcOps[1]);
            string valueOperand = BuildUIntCast(srcOps[2]);
            string mask = $"((1u << {width}) - 1u)";
            string extracted = $"(({valueOperand} >> {offset}) & {mask})";
            string signShift = $"(32u - {width})";
            string value = $"(asint(({extracted}) << {signShift}) >> {signShift})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleMinimum(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"min({srcOps[0]}, {srcOps[1]})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleMaximum(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"max({srcOps[0]}, {srcOps[1]})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleSquareRoot(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"sqrt({srcOps[0]})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleSquareRootReciprocal(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"rsqrt({srcOps[0]})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleLogarithm2(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"log({srcOps[0]})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleToThePower(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"pow({srcOps[0]}, {srcOps[1]})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleReciprocal(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"rcp({srcOps[0]})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleFractional(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"frac({srcOps[0]})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleFloor(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"floor({srcOps[0]})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleCeiling(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"ceil({srcOps[0]})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleRound(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"round({srcOps[0]})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleTruncate(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"trunc({srcOps[0]})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleIntToFloat(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = BuildNumericCast("float", srcOps[0]);
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleUIntToFloat(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = BuildNumericCast("float", srcOps[0]);
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleFloatToInt(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = BuildNumericCast("int", srcOps[0]);
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleFloatToUInt(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = BuildNumericCast("uint", srcOps[0]);
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleNegate(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"-{srcOps[0]}";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleClamp(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = inst.instructionType == USILInstructionType.ClampUInt
                ? $"clamp(uint({srcOps[0]}), uint({srcOps[1]}), uint({srcOps[2]}))"
                : $"clamp({srcOps[0]}, {srcOps[1]}, {srcOps[2]})";

            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleSine(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"sin({srcOps[0]})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleCosine(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"cos({srcOps[0]})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleShiftLeft(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            USILOperand srcOp0 = srcOps[0];
            USILOperand srcOp1 = srcOps[1];

            int op0IntSize = srcOp0.GetValueCount();
            int op1IntSize = srcOp1.GetValueCount();

            string op0Text, op1Text;

            if (srcOp0.operandType == USILOperandType.ImmediateInt) op0Text = $"{srcOp0}";
            else op0Text = $"int{op0IntSize}({srcOp0})";

            if (srcOp1.operandType == USILOperandType.ImmediateInt) op1Text = $"{srcOp1}";
            else op1Text = $"int{op1IntSize}({srcOp1})";

            string value = $"float{op0IntSize}({op0Text} << {op1Text})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleShiftRight(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            USILOperand srcOp0 = srcOps[0];
            USILOperand srcOp1 = srcOps[1];

            int op0IntSize = srcOp0.GetValueCount();
            int op1IntSize = srcOp1.GetValueCount();

            string op0Text, op1Text;

            if (srcOp0.operandType == USILOperandType.ImmediateInt) op0Text = $"{srcOp0}";
            else op0Text = $"int{op0IntSize}({srcOp0})";

            if (srcOp1.operandType == USILOperandType.ImmediateInt) op1Text = $"{srcOp1}";
            else op1Text = $"int{op1IntSize}({srcOp1})";

            string value = $"float{op0IntSize}({op0Text} >> {op1Text})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleDotProduct(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = WrapSaturate(inst, $"dot({srcOps[0]}, {srcOps[1]})");
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleLerp(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = WrapSaturate(inst, $"lerp({srcOps[0]}, {srcOps[1]}, {srcOps[2]})");
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleNormalize(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = WrapSaturate(inst, $"normalize({srcOps[0]})");
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleLength(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = WrapSaturate(inst, $"length({srcOps[0]})");
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleSample(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            USILOperand textureOperand = srcOps[2];
            int samplerTypeIdx = inst.instructionType == USILInstructionType.Sample ? 3 : 4;
            bool samplerType = srcOps.Count > samplerTypeIdx && srcOps[samplerTypeIdx].immValueInt != null && srcOps[samplerTypeIdx].immValueInt.Length > 0 && srcOps[samplerTypeIdx].immValueInt[0] == 1;
            
            string args = $"{srcOps[2]}, {srcOps[0]}";
            string value;
            if (!samplerType)
            {
                value = textureOperand.operandType switch
                {
                    USILOperandType.Sampler2D => $"tex2D({args})",
                    USILOperandType.Sampler3D => $"tex3D({args})",
                    USILOperandType.SamplerCube => $"texCUBE({args})",
                    USILOperandType.Sampler2DArray => $"UNITY_SAMPLE_TEX2DARRAY({args})",
                    USILOperandType.SamplerCubeArray => $"UNITY_SAMPLE_TEXCUBEARRAY({args})",
                    _ => $"tex2D({args})"
                };
            }
            else
            {
                args = $"{srcOps[2]}, {args}";
                value = textureOperand.operandType switch
                {
                    USILOperandType.Sampler2D => $"UNITY_SAMPLE_TEX2D_SAMPLER({args})",
                    USILOperandType.Sampler3D => $"UNITY_SAMPLE_TEX3D_SAMPLER({args})",
                    USILOperandType.SamplerCube => $"UNITY_SAMPLE_TEXCUBE_SAMPLER({args})",
                    USILOperandType.Sampler2DArray => $"UNITY_SAMPLE_TEX2DARRAY_SAMPLER({args})",
                    USILOperandType.SamplerCubeArray => $"UNITY_SAMPLE_TEXCUBEARRAY_SAMPLER({args})",
                    _ => $"tex2D({srcOps[2]}, {srcOps[0]})"
                };
            }
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleSampleLOD(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            USILOperand textureOperand = srcOps[2];
            bool samplerType = srcOps.Count > 4 && srcOps[4].immValueInt != null && srcOps[4].immValueInt.Length > 0 && srcOps[4].immValueInt[0] == 1;
            
            string args;
            if (srcOps[0].mask.Length == 2)
                args = $"{srcOps[2]}, float4({srcOps[0]}, 0, {srcOps[3]})";
            else
                args = $"{srcOps[2]}, float4({srcOps[0]}, {srcOps[3]})";

            string value;
            if (!samplerType)
            {
                value = textureOperand.operandType switch
                {
                    USILOperandType.Sampler2D => $"tex2Dlod({args})",
                    USILOperandType.Sampler3D => $"tex3Dlod({args})",
                    USILOperandType.SamplerCube => $"texCUBElod({args})",
                    USILOperandType.Sampler2DArray => $"UNITY_SAMPLE_TEX2DARRAY_LOD({args})",
                    USILOperandType.SamplerCubeArray => $"UNITY_SAMPLE_TEXCUBEARRAY_LOD({args})",
                    _ => $"tex2Dlod({args})"
                };
            }
            else
            {
                args = $"{srcOps[2]}, {args}";
                value = textureOperand.operandType switch
                {
                    USILOperandType.Sampler2D => $"UNITY_SAMPLE_TEX2D_SAMPLER({args})",
                    USILOperandType.Sampler3D => $"UNITY_SAMPLE_TEX3D_SAMPLER({args})",
                    USILOperandType.SamplerCube => $"UNITY_SAMPLE_TEXCUBE_SAMPLER({args})",
                    USILOperandType.Sampler2DArray => $"UNITY_SAMPLE_TEX2DARRAY_SAMPLER({args})",
                    USILOperandType.SamplerCubeArray => $"UNITY_SAMPLE_TEXCUBEARRAY_SAMPLER({args})",
                    _ => srcOps[0].mask.Length == 2
                        ? $"tex2Dlod({srcOps[2]}, float4({srcOps[0]}, 0, {srcOps[3]}))"
                        : $"tex2Dlod({srcOps[2]}, float4({srcOps[0]}, {srcOps[3]}))"
                };
            }
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleSampleDerivative(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            USILOperand textureOperand = srcOps[2];
            string args = $"{srcOps[2]}, {srcOps[0]}, {srcOps[3]}, {srcOps[4]}";
            string value = textureOperand.operandType switch
            {
                USILOperandType.Sampler2D => $"tex2Dgrad({args})",
                USILOperandType.Sampler3D => $"tex3Dgrad({args})",
                USILOperandType.SamplerCube => $"texCUBEgrad({args})",
                _ => $"tex2Dgrad({args})"
            };
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleSampleLODBias(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            USILOperand textureOperand = srcOps[2];
            string bias = srcOps.Count > 3 ? srcOps[3].ToString() : "0";

            string args;
            if (srcOps[0].mask.Length == 2)
            {
                args = $"{srcOps[2]}, float4({srcOps[0]}, 0, {bias})";
            }
            else
            {
                args = $"{srcOps[2]}, float4({srcOps[0]}, {bias})";
            }

            string value = textureOperand.operandType switch
            {
                USILOperandType.Sampler2D => $"tex2Dbias({args})",
                USILOperandType.Sampler3D => $"tex3Dbias({args})",
                USILOperandType.SamplerCube => $"texCUBEbias({args})",
                _ => $"tex2Dbias({args})"
            };
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleLoadResource(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string resource = srcOps[1].ToString(true);
            string value = $"Load({resource}, {srcOps[0]})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleLoadResourceRaw(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            int width = Math.Clamp(inst.destOperand?.GetValueCount() ?? 1, 1, 4);
            string loadFn = width switch
            {
                1 => "Load",
                2 => "Load2",
                3 => "Load3",
                _ => "Load4"
            };
            string resource = srcOps[1].ToString(true);
            string byteAddress = BuildUIntCast(srcOps[0]);
            string value = $"{resource}.{loadFn}({byteAddress})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleLoadResourceStructured(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string resource = srcOps[2].ToString(true);
            USILOperand structureIndexOperand = srcOps[0];
            USILOperand byteOffsetOperand = srcOps[1];

            bool hasImmediateByteOffset = byteOffsetOperand.immValueInt != null && byteOffsetOperand.immValueInt.Length > 0;
            int byteOffset = hasImmediateByteOffset ? byteOffsetOperand.immValueInt[0] : 0;
            int structureAdd = byteOffset / 16;
            int componentOffset = (byteOffset % 16) / 4;

            string structureIndexExpr = structureAdd == 0
                ? $"{structureIndexOperand}"
                : $"({structureIndexOperand} + {structureAdd})";
            string loadExpr = $"{resource}.Load({structureIndexExpr})";

            int destWidth = Math.Clamp(inst.destOperand?.GetValueCount() ?? 1, 1, 4);
            string value;
            if (destWidth == 1)
            {
                int component = hasImmediateByteOffset ? Math.Clamp(componentOffset, 0, 3) : 0;
                value = $"{loadExpr}.{USILConstants.MASK_CHARS[component]}";
            }
            else if (destWidth < 4)
            {
                int swizzleStart = hasImmediateByteOffset ? Math.Clamp(componentOffset, 0, 3) : 0;
                int swizzleCount = Math.Min(destWidth, 4 - swizzleStart);
                value = $"{loadExpr}.{BuildMaskText(swizzleStart, swizzleCount)}";
            }
            else
            {
                value = loadExpr;
            }

            if (!hasImmediateByteOffset)
            {
                value += $" /* dynamic structured byte offset {byteOffsetOperand} not fully resolved */";
            }
            else if (destWidth == 4 && componentOffset != 0)
            {
                value += $" /* structured byte offset {byteOffset} alignment fallback */";
            }

            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleDiscard(USILInstruction inst)
        {
            string comment = CommentString(inst);
            AppendLine($"{comment}discard;");
        }

        private void HandleResourceDimensionInfo(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;

            USILOperand usilResource = srcOps[0];
            USILOperand usilMipLevel = srcOps[1];
            USILOperand usilWidth = srcOps[2];
            USILOperand usilHeight = srcOps[3];
            USILOperand usilDepthOrArraySize = srcOps[4];
            USILOperand usilMipCount = srcOps[5];

            List<string> args = new List<string>();

            if (usilMipLevel.immValueFloat != null && usilMipLevel.immValueFloat.Length > 0 && usilMipLevel.immValueFloat[0] == 0 && usilMipCount.operandType == USILOperandType.Null)
            {
                args.Add(usilWidth.ToString());
                if (usilHeight.operandType != USILOperandType.Null) args.Add(usilHeight.ToString());
                if (usilDepthOrArraySize.operandType != USILOperandType.Null) args.Add(usilDepthOrArraySize.ToString());
            }
            else
            {
                args.Add(usilMipLevel.ToString());
                args.Add(usilWidth.ToString());
                if (usilHeight.operandType != USILOperandType.Null) args.Add(usilHeight.ToString());
                if (usilDepthOrArraySize.operandType != USILOperandType.Null) args.Add(usilDepthOrArraySize.ToString());
                
                if (usilMipCount.operandType != USILOperandType.Null) args.Add(usilMipCount.ToString());
                else args.Add("resinfo_extra");
            }

            string call = $"GetDimensions({string.Join(", ", args)})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{usilResource}.{call};");
        }

        private void HandleSampleCountInfo(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"{srcOps[0]} = GetRenderTargetSampleCount()";
            string comment = CommentString(inst);
            AppendLine($"{comment}{value};");
        }

        private void HandleDerivativeRenderTarget(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string fun = inst.instructionType switch
            {
                USILInstructionType.DerivativeRenderTargetX => "ddx",
                USILInstructionType.DerivativeRenderTargetY => "ddy",
                USILInstructionType.DerivativeRenderTargetXCoarse => "ddx_coarse",
                USILInstructionType.DerivativeRenderTargetYCoarse => "ddy_coarse",
                USILInstructionType.DerivativeRenderTargetXFine => "ddx_fine",
                USILInstructionType.DerivativeRenderTargetYFine => "ddy_fine",
                _ => "dd?"
            };
            string value = $"{fun}({srcOps[0]})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleIf(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string comment = CommentString(inst);
            if (inst.instructionType == USILInstructionType.IfTrue)
                AppendLine($"{comment}if ({srcOps[0]}) {{");
            else
                AppendLine($"{comment}if (!({srcOps[0]})) {{");
            _indentLevel++;
        }

        private void HandleElse(USILInstruction inst)
        {
            _indentLevel--;
            string comment = CommentString(inst);
            AppendLine($"{comment}}} else {{");
            _indentLevel++;
        }

        private void HandleEndIf(USILInstruction inst)
        {
            _indentLevel--;
            string comment = CommentString(inst);
            AppendLine($"{comment}}}");
        }

        private void HandleLoop(USILInstruction inst)
        {
            string comment = CommentString(inst);
            AppendLine($"{comment}while (true) {{");
            _indentLevel++;
        }

        private void HandleEndLoop(USILInstruction inst)
        {
            _indentLevel--;
            string comment = CommentString(inst);
            AppendLine($"{comment}}}");
        }

        private void HandleBreak(USILInstruction inst)
        {
            string comment = CommentString(inst);
            AppendLine($"{comment}break;");
        }

        private void HandleContinue(USILInstruction inst)
        {
            string comment = CommentString(inst);
            AppendLine($"{comment}continue;");
        }

        private void HandleForLoop(USILInstruction inst)
        {
            string comment = CommentString(inst);

            USILOperand iterRegOp = inst.srcOperands[0];
            USILOperand compOp = inst.srcOperands[1];
            USILInstructionType compType = (USILInstructionType)inst.srcOperands[2].immValueInt[0];
            USILNumberType numberType = (USILNumberType)inst.srcOperands[3].immValueInt[0];
            float addCount = inst.srcOperands[4].immValueFloat[0];
            int depth = inst.srcOperands[5].immValueInt[0];

            string numberTypeName = numberType switch
            {
                USILNumberType.Float => "float",
                USILNumberType.Int => "int",
                USILNumberType.UnsignedInt => "unsigned int",
                _ => "?"
            };

            string iterName = depth < USILConstants.ITER_CHARS.Length
                ? USILConstants.ITER_CHARS[depth].ToString()
                : $"iter{depth}";

            string compText = compType switch
            {
                USILInstructionType.Equal => "==",
                USILInstructionType.NotEqual => "!=",
                USILInstructionType.GreaterThan => ">",
                USILInstructionType.GreaterThanOrEqual => ">=",
                USILInstructionType.LessThan => "<",
                USILInstructionType.LessThanOrEqual => "<=",
                _ => "?"
            };

            AppendLine(
                $"{comment}for ({numberTypeName} {iterName} = {iterRegOp}; " +
                $"{iterName} {compText} {compOp}; " +
                $"{iterName} += {addCount.ToString(CultureInfo.InvariantCulture)}) {{"
            );

            _indentLevel++;
        }

        private void HandleSwitch(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string comment = CommentString(inst);
            AppendLine($"{comment}switch ({srcOps[0]}) {{");
            _indentLevel++;
        }

        private void HandleCase(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string comment = CommentString(inst);
            AppendLine($"{comment}case {srcOps[0]}:");
        }

        private void HandleDefault(USILInstruction inst)
        {
            string comment = CommentString(inst);
            AppendLine($"{comment}default:");
        }

        private void HandleEndSwitch(USILInstruction inst)
        {
            _indentLevel--;
            string comment = CommentString(inst);
            AppendLine($"{comment}}}");
        }

        private void HandleEqual(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"{srcOps[0]} == {srcOps[1]}";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleNotEqual(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"{srcOps[0]} != {srcOps[1]}";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleLessThan(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"{srcOps[0]} < {srcOps[1]}";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleLessThanOrEqual(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"{srcOps[0]} <= {srcOps[1]}";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleGreaterThan(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"{srcOps[0]} > {srcOps[1]}";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleGreaterThanOrEqual(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = $"{srcOps[0]} >= {srcOps[1]}";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleReturn(USILInstruction inst)
        {
            string outputName = _shader.shaderFunctionType switch
            {
                UShaderFunctionType.Vertex => USILConstants.VERT_OUTPUT_LOCAL_NAME,
                UShaderFunctionType.Fragment => USILConstants.FRAG_OUTPUT_LOCAL_NAME,
                _ => "o"
            };

            string value = $"return {outputName}";
            string comment = CommentString(inst);
            AppendLine($"{comment}{value};");
        }

        private void MultiplyMatrixByVector(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string value = srcOps[0].transposeMatrix
                ? $"mul({srcOps[1]}, {srcOps[0]})"
                : $"mul({srcOps[0]}, {srcOps[1]})";
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = {value};");
        }

        private void HandleUnityObjectToClipPos(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = UnityObjectToClipPos({srcOps[0]});");
        }

        private void HandleUnityObjectToWorldNormal(USILInstruction inst)
        {
            List<USILOperand> srcOps = inst.srcOperands;
            string comment = CommentString(inst);
            AppendLine($"{comment}{inst.destOperand} = UnityObjectToWorldNormal({srcOps[0]});");
        }

        private void HandleComment(USILInstruction inst)
        {
            AppendLine($"//{inst.destOperand?.comment};");
        }

        private string WrapSaturate(USILInstruction inst, string str)
        {
            if (inst.saturate)
            {
                str = $"saturate({str})";
            }
            return str;
        }

        private static string BuildNumericCast(string typeName, USILOperand operand)
        {
            int width = Math.Max(1, operand.GetValueCount());
            if (width == 1)
            {
                return $"{typeName}({operand})";
            }
            return $"{typeName}{width}({operand})";
        }

        private static string BuildUIntCast(USILOperand operand)
        {
            int width = Math.Max(1, operand.GetValueCount());
            if (width == 1)
            {
                return $"uint({operand})";
            }
            return $"uint{width}({operand})";
        }

        private static string BuildMaskText(int start, int count)
        {
            StringBuilder sb = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                int maskIndex = Math.Clamp(start + i, 0, 3);
                sb.Append(USILConstants.MASK_CHARS[maskIndex]);
            }
            return sb.ToString();
        }

        private void AppendLine(string line)
        {
            _stringBuilder.Append(_baseIndent);

            for (int i = 0; i < _indentLevel; i++)
            {
                _stringBuilder.Append(_indent);
            }

            _stringBuilder.AppendLine(line);
        }

        private string CommentString(USILInstruction inst)
        {
            return inst.commented ? "//" : "";
        }
    }
}
