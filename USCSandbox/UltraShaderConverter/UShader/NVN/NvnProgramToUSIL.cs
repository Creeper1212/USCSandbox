using AssetRipper.Export.Modules.Shaders.UltraShaderConverter.UShader.Function;
using AssetRipper.Export.Modules.Shaders.UltraShaderConverter.USIL;
using Ryujinx.Graphics.Shader.Decoders;
using Ryujinx.Graphics.Shader.IntermediateRepresentation;
using Ryujinx.Graphics.Shader.Translation;
using System;
using System.Collections.Generic;
using System.Linq;
using RyuOperandType = Ryujinx.Graphics.Shader.IntermediateRepresentation.OperandType;

namespace USCSandbox.UltraShaderConverter.UShader.NVN
{
    public class NvnProgramToUSIL
    {
        private TranslatorContext _nvnShader;
        private DecodedProgram _prog;
        private INode[] _ryuIl;

        public UShaderProgram shader;

        private List<USILLocal> Locals => shader.locals;
        private List<USILInstruction> Instructions => shader.instructions;
        private List<USILInputOutput> Inputs => shader.inputs;
        private List<USILInputOutput> Outputs => shader.outputs;

        private delegate void InstHandler(Operation inst);
        private Dictionary<Instruction, InstHandler> _instructionHandlers;
        private Dictionary<Operand, int> _ryuLabels;
        private Dictionary<Operand, int> _ryuLocals;
        private Dictionary<BasicBlock, int> _blockIdxMap;

        public NvnProgramToUSIL(TranslatorContext nvnShader)
        {
            _nvnShader = nvnShader;
            _prog = nvnShader.Program;

            shader = new UShaderProgram();
            _instructionHandlers = new Dictionary<Instruction, InstHandler>()
            {
                { Instruction.Add, HandleAdd },
                { Instruction.Clamp, HandleClamp },
                { Instruction.ClampU32, HandleClamp },
                { Instruction.Comment, HandleComment },
                { Instruction.Copy, HandleCopy },
                { Instruction.Divide, HandleDivide },
                { Instruction.FusedMultiplyAdd, HandleMad },
                { Instruction.Load, HandleLoadStore },
                { Instruction.Maximum, HandleMaximum },
                { Instruction.MaximumU32, HandleMaximum },
                { Instruction.Minimum, HandleMinimum },
                { Instruction.MinimumU32, HandleMinimum },
                { Instruction.Multiply, HandleMul },
                { Instruction.Negate, HandleNegate },
                { Instruction.SquareRoot, HandleSquareRoot },
                { Instruction.Store, HandleLoadStore },
                { Instruction.Subtract, HandleAdd },
                { Instruction.ReciprocalSquareRoot, HandleRSquareRoot },
                { Instruction.Return, HandleReturn }
            };

            _ryuLocals = new Dictionary<Operand, int>();
            _ryuLabels = new Dictionary<Operand, int>();
            _blockIdxMap = new Dictionary<BasicBlock, int>();
        }

        public void Convert()
        {
            GenerateRyujinxIl();
            ConvertInstructions();
        }

        private void GenerateRyujinxIl()
        {
            var funcs = _nvnShader.TranslateToFunctions();
            var func = funcs[0];
            var insts = new List<INode>();

            var blockIdx = 0;
            foreach (var block in func.Blocks)
            {
                _blockIdxMap[block] = blockIdx++;
            }

            foreach (var block in func.Blocks)
            {
                insts.Add(new CommentNode($"Block {_blockIdxMap[block]}"));
                insts.AddRange(block.Operations);
                
                if (block.HasBranch)
                {
                    if (block.Branch != null)
                        insts.Add(new CommentNode($"  Block {_blockIdxMap[block]} BT -> Block {_blockIdxMap[block.Branch]}"));
                    if (block.Next != null)
                        insts.Add(new CommentNode($"  Block {_blockIdxMap[block]} BF -> Block {_blockIdxMap[block.Next]}"));
                }
                else
                {
                    if (block.Next != null)
                        insts.Add(new CommentNode($"  Block {_blockIdxMap[block]} BU -> Block {_blockIdxMap[block.Next]}"));
                }
            }
            _ryuIl = insts.ToArray();
        }

        private void ConvertInstructions()
        {
            foreach (INode node in _ryuIl)
            {
                string disasm = "???";
                if (node is Operation inst)
                {
                    disasm = RyuOperationToString(inst);
                    Instruction maskedInst = inst.Inst & Instruction.Mask;
                    if (_instructionHandlers.ContainsKey(maskedInst))
                    {
                        _instructionHandlers[maskedInst](inst);
                        continue;
                    }
                }
                else if (node is PhiNode phiNode)
                {
                    disasm = RyuPhiNodeToString(phiNode);
                }
                else if (node is CommentNode commentNode)
                {
                    disasm = commentNode.Comment;
                }

                Instructions.Add(new USILInstruction
                {
                    instructionType = USILInstructionType.Comment,
                    destOperand = new USILOperand
                    {
                        comment = $"{disasm}",
                        operandType = USILOperandType.Comment
                    },
                    srcOperands = new List<USILOperand>()
                });
            }
        }

        private string RyuOperationToString(Operation operation)
        {
            string maskedInst = (operation.Inst & Instruction.Mask).ToString();
            if ((operation.Inst & Instruction.FP32) != 0) maskedInst += ".FP32";
            if ((operation.Inst & Instruction.FP64) != 0) maskedInst += ".FP64";

            string storeKind = operation.StorageKind.ToString();

            List<string> destStrs = Enumerable.Range(0, operation.DestsCount)
                .Select(i => RyuOperandToString(operation.GetDest(i)))
                .ToList();

            List<string> srcStrs = Enumerable.Range(0, operation.SourcesCount)
                .Select(i => RyuOperandToString(operation.GetSource(i)))
                .ToList();

            return $"{maskedInst}({storeKind}) {string.Join(",", destStrs)} <= {string.Join(",", srcStrs)}";
        }

        private string RyuPhiNodeToString(PhiNode operation)
        {
            List<string> destStrs = Enumerable.Range(0, operation.DestsCount)
                .Select(i => RyuOperandToString(operation.GetDest(i)))
                .ToList();

            List<string> srcStrs = Enumerable.Range(0, operation.SourcesCount)
                .Select(i => $"{RyuOperandToString(operation.GetSource(i))}:Block{_blockIdxMap[operation.GetBlock(i)]}")
                .ToList();

            return $"$phi {string.Join(",", destStrs)} <= {string.Join(",", srcStrs)}";
        }

        private string RyuOperandToString(Operand operand)
        {
            if (operand == null) return "[null]";
            
            switch (operand.Type)
            {
                case RyuOperandType.Argument:
                    return $"[arg:{operand.Value}]";
                case RyuOperandType.Constant:
                    return $"[con:{operand.Value}]";
                case RyuOperandType.ConstantBuffer:
                    return $"[cbf:{operand.GetCbufSlot()}:{operand.GetCbufOffset()}]";
                case RyuOperandType.Label:
                    if (!_ryuLabels.ContainsKey(operand))
                    {
                        _ryuLabels.Add(operand, _ryuLabels.Count);
                    }
                    return $"[lbl:{_ryuLabels[operand]}]";
                case RyuOperandType.LocalVariable:
                    if (!_ryuLocals.ContainsKey(operand))
                    {
                        _ryuLocals.Add(operand, _ryuLocals.Count);
                    }
                    return $"[var:L{_ryuLocals[operand]}]";
                case RyuOperandType.Register:
                    return $"[reg:{RyuRegisterToString(operand.GetRegister())}]";
                case RyuOperandType.Undefined:
                    return "[undef]";
                default:
                    return "[unk]";
            }
        }

        private string RyuRegisterToString(Register register)
        {
            string typePrefix = register.Type switch
            {
                RegisterType.Flag => "F",
                RegisterType.Gpr => "R",
                RegisterType.Predicate => "P",
                _ => "?"
            };

            return $"{typePrefix}{register.Index}{(register.IsPT ? "P" : "")}{(register.IsRZ ? "R" : "")}";
        }

        private void FillUSILOperand(Operand mxOperand, USILOperand usilOperand, bool immIsInt)
        {
            if (mxOperand == null)
            {
                usilOperand.operandType = USILOperandType.Null;
                return;
            }

            switch (mxOperand.Type)
            {
                case RyuOperandType.Constant:
                {
                    SetUsilOperandImmediate(usilOperand, mxOperand.Value, mxOperand.AsFloat(), immIsInt);
                    break;
                }
                case RyuOperandType.ConstantBuffer:
                {
                    int cbufSlot = mxOperand.GetCbufSlot();
                    int cbufOffset = mxOperand.GetCbufOffset();
                    int vecIndex = cbufOffset >> 2;
                    int elemIndex = cbufOffset & 3;

                    usilOperand.operandType = USILOperandType.ConstantBuffer;
                    usilOperand.registerIndex = 3 - cbufSlot; // Slot remap logic
                    usilOperand.arrayIndex = vecIndex;
                    usilOperand.mask = new int[] { elemIndex };
                    break;
                }
                case RyuOperandType.Register:
                case RyuOperandType.LocalVariable:
                {
                    Register reg = mxOperand.GetRegister();

                    if (reg.IsRZ)
                    {
                        SetUsilOperandImmediate(usilOperand, 0, 0f, immIsInt);
                    }
                    else if (reg.Type == RegisterType.Gpr || reg.Type == RegisterType.Flag)
                    {
                        usilOperand.operandType = USILOperandType.TempRegister;
                        if (mxOperand.Type == RyuOperandType.LocalVariable)
                        {
                            if (!_ryuLocals.ContainsKey(mxOperand))
                            {
                                _ryuLocals.Add(mxOperand, _ryuLocals.Count);
                            }
                            usilOperand.registerIndex = _ryuLocals[mxOperand] + 1000; // +1000 to differentiate from normal registers
                        }
                        else
                        {
                            usilOperand.registerIndex = reg.Index;
                        }
                    }
                    else
                    {
                        // unsupported
                        usilOperand.operandType = USILOperandType.Comment;
                        usilOperand.comment = $"/*{mxOperand.Type}/{mxOperand.Value}/{reg.Type}/1*/";
                    }
                    break;
                }
                default:
                {
                    usilOperand.operandType = USILOperandType.Comment;
                    usilOperand.comment = $"/*{mxOperand.Type}/{mxOperand.Value}/2*/";
                    break;
                }
            }
        }

        private void SetUsilOperandImmediate(USILOperand usilOperand, int intValue, float floatValue, bool immIsInt)
        {
            usilOperand.operandType = immIsInt ? USILOperandType.ImmediateInt : USILOperandType.ImmediateFloat;
            if (immIsInt)
                usilOperand.immValueInt = new int[] { intValue };
            else
                usilOperand.immValueFloat = new float[] { floatValue };
        }

        private void HandleAdd(Operation inst)
        {
            Operand dest = inst.GetDest(0);
            Operand src0 = inst.GetSource(0);
            Operand src1 = inst.GetSource(1);

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();
            USILOperand usilSrc1 = new USILOperand();

            FillUSILOperand(dest, usilDest, false);
            FillUSILOperand(src0, usilSrc0, false);
            FillUSILOperand(src1, usilSrc1, false);

            if ((inst.Inst & Instruction.Mask) == Instruction.Add)
            {
                usilInst.instructionType = USILInstructionType.Add;
            }
            else
            {
                usilInst.instructionType = USILInstructionType.Subtract;
            }

            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand> { usilSrc0, usilSrc1 };
            usilInst.saturate = false;

            Instructions.Add(usilInst);
        }

        private void HandleClamp(Operation inst)
        {
            Operand dest = inst.GetDest(0);
            Operand src0 = inst.GetSource(0);
            Operand src1 = inst.GetSource(1);
            Operand src2 = inst.GetSource(2);

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();
            USILOperand usilSrc1 = new USILOperand();
            USILOperand usilSrc2 = new USILOperand();

            bool isInt = (inst.Inst & Instruction.Mask) == Instruction.ClampU32;

            FillUSILOperand(dest, usilDest, isInt);
            FillUSILOperand(src0, usilSrc0, isInt);
            FillUSILOperand(src1, usilSrc1, isInt);
            FillUSILOperand(src2, usilSrc2, isInt);

            if (isInt)
            {
                usilInst.instructionType = USILInstructionType.ClampUInt;
            }
            else
            {
                usilInst.instructionType = USILInstructionType.Clamp;
            }

            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand> { usilSrc0, usilSrc1, usilSrc2 };
            usilInst.saturate = false;

            Instructions.Add(usilInst);
        }

        private void HandleComment(Operation inst)
        {
            var commentInst = (CommentNode)inst;
            Instructions.Add(new USILInstruction
            {
                instructionType = USILInstructionType.Comment,
                destOperand = new USILOperand
                {
                    comment = commentInst.Comment,
                    operandType = USILOperandType.Comment
                },
                srcOperands = new List<USILOperand>()
            });
        }

        private void HandleCopy(Operation inst)
        {
            Operand dest = inst.GetDest(0);
            Operand src0 = inst.GetSource(0);

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();

            FillUSILOperand(dest, usilDest, false);
            FillUSILOperand(src0, usilSrc0, false);

            usilInst.instructionType = USILInstructionType.Move;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand> { usilSrc0 };
            usilInst.saturate = false;

            Instructions.Add(usilInst);
        }

        private void HandleDivide(Operation inst)
        {
            Operand dest = inst.GetDest(0);
            Operand src0 = inst.GetSource(0);
            Operand src1 = inst.GetSource(1);

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();
            USILOperand usilSrc1 = new USILOperand();

            FillUSILOperand(dest, usilDest, false);
            FillUSILOperand(src0, usilSrc0, false);
            FillUSILOperand(src1, usilSrc1, false);

            usilInst.instructionType = USILInstructionType.Divide;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand> { usilSrc0, usilSrc1 };
            usilInst.saturate = false;

            Instructions.Add(usilInst);
        }

        private void HandleLoadStore(Operation inst)
        {
            bool isStore = (inst.Inst & Instruction.Mask) == Instruction.Store;
            int srcIdx = 0;
            StorageKind storKind = inst.StorageKind;

            if (storKind.IsInputOrOutput())
            {
                Operand dest = !isStore ? inst.GetDest(0) : null;

                Operand srcVarId = inst.GetSource(srcIdx++);
                IoVariable io = (IoVariable)srcVarId.Value;

                USILInstruction usilInst = new USILInstruction();
                USILOperand usilDest = new USILOperand();
                USILOperand usilSrc0 = new USILOperand();

                USILOperand specialOp = !isStore ? usilSrc0 : usilDest;
                switch (io)
                {
                    case IoVariable.UserDefined:
                    case IoVariable.FragmentOutputColor:
                    {
                        Operand srcRegIndex = inst.GetSource(srcIdx++);
                        Operand srcMaskIndex = inst.GetSource(srcIdx++);

                        specialOp.operandType = storKind switch
                        {
                            StorageKind.Input or StorageKind.InputPerPatch => USILOperandType.InputRegister,
                            StorageKind.Output or StorageKind.OutputPerPatch => USILOperandType.OutputRegister,
                            _ => throw new Exception("invalid storage kind")
                        };
                        specialOp.registerIndex = srcRegIndex.Value;
                        specialOp.mask = new int break;
                    }
                    default:
                    {
                        goto unsupported;
                    }
                }

                if (!isStore && dest != null)
                {
                    FillUSILOperand(dest, usilDest, false);
                }
                else
                {
                    Operand storeVal = inst.GetSource(srcIdx++);
                    FillUSILOperand(storeVal, usilSrc0, false);
                }

                usilInst.instructionType = USILInstructionType.Move;
                usilInst.destOperand = usilDest;
                usilInst.srcOperands = new List<USILOperand> { usilSrc0 };
                usilInst.saturate = false;

                Instructions.Add(usilInst);
                return;
            }

        unsupported:
            string disasm = inst.Inst.ToString();
            Instructions.Add(new USILInstruction
            {
                instructionType = USILInstructionType.Comment,
                destOperand = new USILOperand
                {
                    comment = $"{disasm} // Unsupported",
                    operandType = USILOperandType.Comment
                },
                srcOperands = new List<USILOperand>()
            });
        }

        private void HandleMad(Operation inst)
        {
            Operand dest = inst.GetDest(0);
            Operand src0 = inst.GetSource(0);
            Operand src1 = inst.GetSource(1);
            Operand src2 = inst.GetSource(2);

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();
            USILOperand usilSrc1 = new USILOperand();
            USILOperand usilSrc2 = new USILOperand();

            FillUSILOperand(dest, usilDest, false);
            FillUSILOperand(src0, usilSrc0, false);
            FillUSILOperand(src1, usilSrc1, false);
            FillUSILOperand(src2, usilSrc2, false);

            usilInst.instructionType = USILInstructionType.MultiplyAdd;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand> { usilSrc0, usilSrc1, usilSrc2 };
            usilInst.saturate = false;

            Instructions.Add(usilInst);
        }

        private void HandleMaximum(Operation inst)
        {
            Operand dest = inst.GetDest(0);
            Operand src0 = inst.GetSource(0);
            Operand src1 = inst.GetSource(1);

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();
            USILOperand usilSrc1 = new USILOperand();

            bool isInt = (inst.Inst & Instruction.Mask) == Instruction.MaximumU32;

            FillUSILOperand(dest, usilDest, isInt);
            FillUSILOperand(src0, usilSrc0, isInt);
            FillUSILOperand(src1, usilSrc1, isInt);

            usilInst.instructionType = USILInstructionType.Maximum;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand> { usilSrc0, usilSrc1 };
            usilInst.saturate = false;

            Instructions.Add(usilInst);
        }

        private void HandleMinimum(Operation inst)
        {
            Operand dest = inst.GetDest(0);
            Operand src0 = inst.GetSource(0);
            Operand src1 = inst.GetSource(1);

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();
            USILOperand usilSrc1 = new USILOperand();

            bool isInt = (inst.Inst & Instruction.Mask) == Instruction.MinimumU32;

            FillUSILOperand(dest, usilDest, isInt);
            FillUSILOperand(src0, usilSrc0, isInt);
            FillUSILOperand(src1, usilSrc1, isInt);

            usilInst.instructionType = USILInstructionType.Minimum;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand> { usilSrc0, usilSrc1 };
            usilInst.saturate = false;

            Instructions.Add(usilInst);
        }

        private void HandleMul(Operation inst)
        {
            Operand dest = inst.GetDest(0);
            Operand src0 = inst.GetSource(0);
            Operand src1 = inst.GetSource(1);

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();
            USILOperand usilSrc1 = new USILOperand();

            FillUSILOperand(dest, usilDest, false);
            FillUSILOperand(src0, usilSrc0, false);
            FillUSILOperand(src1, usilSrc1, false);

            usilInst.instructionType = USILInstructionType.Multiply;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand> { usilSrc0, usilSrc1 };
            usilInst.saturate = false;

            Instructions.Add(usilInst);
        }

        private void HandleNegate(Operation inst)
        {
            Operand dest = inst.GetDest(0);
            Operand src0 = inst.GetSource(0);

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();

            FillUSILOperand(dest, usilDest, false);
            FillUSILOperand(src0, usilSrc0, false);

            usilInst.instructionType = USILInstructionType.Negate;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand> { usilSrc0 };
            usilInst.saturate = false;

            Instructions.Add(usilInst);
        }

        private void HandleSquareRoot(Operation inst)
        {
            Operand dest = inst.GetDest(0);
            Operand src0 = inst.GetSource(0);

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();

            FillUSILOperand(dest, usilDest, false);
            FillUSILOperand(src0, usilSrc0, false);

            usilInst.instructionType = USILInstructionType.SquareRoot;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand> { usilSrc0 };
            usilInst.saturate = false;

            Instructions.Add(usilInst);
        }

        private void HandleRSquareRoot(Operation inst)
        {
            Operand dest = inst.GetDest(0);
            Operand src0 = inst.GetSource(0);

            USILInstruction usilInst = new USILInstruction();
            USILOperand usilDest = new USILOperand();
            USILOperand usilSrc0 = new USILOperand();

            FillUSILOperand(dest, usilDest, false);
            FillUSILOperand(src0, usilSrc0, false);

            usilInst.instructionType = USILInstructionType.SquareRootReciprocal;
            usilInst.destOperand = usilDest;
            usilInst.srcOperands = new List<USILOperand> { usilSrc0 };
            usilInst.saturate = false;

            Instructions.Add(usilInst);
        }

        private void HandleReturn(Operation inst)
        {
            USILInstruction usilInst = new USILInstruction();
            usilInst.instructionType = USILInstructionType.Return;
            usilInst.srcOperands = new List<USILOperand>();
            Instructions.Add(usilInst);
        }
    }
}
