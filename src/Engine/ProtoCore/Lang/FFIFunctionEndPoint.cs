﻿

using System;
using System.Collections.Generic;
using ProtoCore.DSASM;
using ProtoCore.Lang.Replication;
using ProtoFFI;
using ProtoCore.Utils;
using ProtoCore.Properties;

namespace ProtoCore.Lang
{
    public class FFIActivationRecord
    {
        public string ModuleType;
        public string ModuleName;
        public bool IsDNI;
        public string FunctionName;
        public List<ProtoCore.Type> ParameterTypes;
        public ProtoCore.Type ReturnType;
        public JILActivationRecord JILRecord;
    }

    public class FFIFunctionEndPoint : FunctionEndPoint
    {
        public static Dictionary<string, FFIHandler> FFIHandlers = new Dictionary<string,FFIHandler>();
        public FFIActivationRecord activation;
        private FFIFunctionPointer mFunctionPointer;

        public FFIFunctionEndPoint(FFIActivationRecord record)
        {
            activation = record;
        }
        public FFIFunctionEndPoint()
        {
            activation = new FFIActivationRecord();
        }

        private bool Validate(FFIFunctionPointer p){ return true; /*todo implement arg type checking*/}
        private bool ValidateReturn(Object p){ return true; /*TODO implement returyn type checking*/}


        public override bool DoesPredicateMatch(ProtoCore.Runtime.Context c, List<StackValue> formalParameters, List<ReplicationInstruction> replicationInstructions)
        {
            return true;
        }

        public override StackValue Execute(ProtoCore.Runtime.Context c, List<StackValue> formalParameters, ProtoCore.DSASM.StackFrame stackFrame, RuntimeCore runtimeCore)
        {   //  ensure there is no data race, function resolution and execution happens in parallel
            //  but for FFI we want it to be serial cause the code we are calling into may not cope
            //  with parallelism.
            //
            //  we are always looking and putting our function pointers in handler with each lang
            //  so better lock for FFIHandler (being static) it  will be good object to lock
            //  
            lock (FFIHandlers)
            {
                Interpreter interpreter = new Interpreter(runtimeCore, true);

                // Setup the stack frame data
                StackValue svThisPtr = stackFrame.ThisPtr;
                int ci = activation.JILRecord.classIndex;
                int fi = activation.JILRecord.funcIndex;
                int returnAddr = stackFrame.ReturnPC;
                int blockDecl = stackFrame.FunctionBlock;
                int blockCaller = stackFrame.FunctionCallerBlock;
                int framePointer = runtimeCore.RuntimeMemory.FramePointer;
                int locals = activation.JILRecord.locals;

                
                FFIHandler handler = FFIHandlers[activation.ModuleType];
                FFIActivationRecord r = activation;
                string className = "";
                if (activation.JILRecord.classIndex > 0)
                {
                    className = runtimeCore.DSExecutable.classTable.ClassNodes[activation.JILRecord.classIndex].Name;
                }

                List<ProtoCore.Type> argTypes = new List<Type>(r.ParameterTypes);

                ProcedureNode fNode = null;
                if (ProtoCore.DSASM.Constants.kInvalidIndex != ci)
                {
                    fNode = interpreter.runtime.exe.classTable.ClassNodes[ci].ProcTable.Procedures[fi];
                }

                // Check if this is a 'this' pointer function overload that was generated by the compiler
                if (null != fNode && fNode.IsAutoGeneratedThisProc)
                {
                    int thisPtrIndex = 0;
                    bool isStaticCall = svThisPtr.IsPointer && Constants.kInvalidPointer == svThisPtr.Pointer;
                    if (isStaticCall)
                    {
                        stackFrame.ThisPtr = formalParameters[thisPtrIndex]; 
                    }
                    argTypes.RemoveAt(thisPtrIndex);

                    // Comment Jun: Execute() can handle a null this pointer. 
                    // But since we dont even need to to reach there if we dont have a valid this pointer, then just return null
                    if (formalParameters[thisPtrIndex].IsNull)
                    {
                        runtimeCore.RuntimeStatus.LogWarning(ProtoCore.Runtime.WarningID.kDereferencingNonPointer, Resources.kDeferencingNonPointer);
                        return StackValue.Null;
                    }

                    // These are the op types allowed. 
                    Validity.Assert(formalParameters[thisPtrIndex].IsPointer || 
                                    formalParameters[thisPtrIndex].IsDefaultArgument);
                    
                    svThisPtr = formalParameters[thisPtrIndex];
                    
                    formalParameters.RemoveAt(thisPtrIndex);
                }

                FFIFunctionPointer functionPointer = handler.GetFunctionPointer(r.ModuleName, className, r.FunctionName, argTypes, r.ReturnType);
                mFunctionPointer = Validate(functionPointer) ? functionPointer : null;
                mFunctionPointer.IsDNI = activation.IsDNI;
                

                if (mFunctionPointer == null)
                {
                    return ProtoCore.DSASM.StackValue.Null;
                }

                {

                    interpreter.runtime.executingBlock = runtimeCore.RunningBlock;
                    activation.JILRecord.globs = runtimeCore.DSExecutable.runtimeSymbols[runtimeCore.RunningBlock].GetGlobalSize();

                    // Params
                    formalParameters.Reverse();
                    for (int i = 0; i < formalParameters.Count; i++)
                    {
                        interpreter.Push(formalParameters[i]);
                    }

                    List<StackValue> registers = new List<DSASM.StackValue>();
                    interpreter.runtime.SaveRegisters(registers);

                    // Comment Jun: the depth is always 0 for a function call as we are reseting this for each function call
                    // This is only incremented for every language block bounce
                    int depth = 0;
                    StackFrameType callerType = stackFrame.CallerStackFrameType;

                    // FFI calls do not have execution states
                    runtimeCore.RuntimeMemory.PushStackFrame(svThisPtr, ci, fi, returnAddr, blockDecl, blockCaller, callerType, ProtoCore.DSASM.StackFrameType.kTypeFunction, depth, framePointer, registers, locals, 0);

                    //is there a way the current stack be passed across and back into the managed runtime by FFI calling back into the language?
                    //e.g. DCEnv* carrying all the stack information? look at how vmkit does this.
                    // = jilMain.Run(ActivationRecord.JILRecord.pc, null, true);

                    //double[] tempArray = GetUnderlyingArray<double>(jilMain.runtime.rmem.stack);
                    Object ret = mFunctionPointer.Execute(c, interpreter);
                    StackValue op;
                    if (ret == null)
                    {
                        op = StackValue.Null;
                    }
                    else if (ret is StackValue)
                    {
                        op = (StackValue)ret;
                    }
                    else if (ret is Int64 || ret is int)
                    {
                        op = StackValue.BuildInt((Int64)ret);
                    }
                    else if (ret is double)
                    {
                        op = StackValue.BuildDouble((double)ret);
                    }
                    else
                    {
                        throw new ArgumentException(string.Format("FFI: incorrect return type {0} from external function {1}:{2}", activation.ReturnType.Name,
                                                    activation.ModuleName, activation.FunctionName));
                    }

                    // Clear the FFI stack frame 
                    // FFI stack frames have no local variables
                    interpreter.runtime.rmem.FramePointer = (int)interpreter.runtime.rmem.GetAtRelative(StackFrame.kFrameIndexFramePointer).IntegerValue;
                    interpreter.runtime.rmem.PopFrame(StackFrame.kStackFrameSize + formalParameters.Count);

                    return op; 
                }
            }
        }
    }
}
