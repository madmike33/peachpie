﻿using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeGen;
using Microsoft.CodeAnalysis.Emit;
using Pchp.CodeAnalysis.Emit;
using Pchp.CodeAnalysis.Semantics;
using Pchp.CodeAnalysis.Symbols;
using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;
using Cci = Microsoft.Cci;

namespace Pchp.CodeAnalysis.CodeGen
{
    internal sealed class MethodGenerator
    {
        private static Cci.DebugSourceDocument CreateDebugDocumentForFile(string normalizedPath)
        {
            return new Cci.DebugSourceDocument(normalizedPath, Constants.CorSymLanguageTypePeachpie);
        }

        internal static MethodBody GenerateMethodBody(
            PEModuleBuilder moduleBuilder,
            SourceRoutineSymbol routine,
            int methodOrdinal,
            //ImmutableArray<LambdaDebugInfo> lambdaDebugInfo,
            //ImmutableArray<ClosureDebugInfo> closureDebugInfo,
            //StateMachineTypeSymbol stateMachineTypeOpt,
            VariableSlotAllocator variableSlotAllocatorOpt,
            DiagnosticBag diagnostics,
            //ImportChain importChainOpt,
            bool emittingPdb)
        {
            return GenerateMethodBody(moduleBuilder, routine, (builder) =>
            {
                DiagnosticBag diagnosticsForThisMethod = DiagnosticBag.GetInstance();
                var optimization = moduleBuilder.Compilation.Options.OptimizationLevel;
                var codeGen = new CodeGenerator(routine, builder, moduleBuilder, diagnosticsForThisMethod, optimization, emittingPdb);

                //if (diagnosticsForThisMethod.HasAnyErrors())
                //{
                //    // we are done here. Since there were errors we should not emit anything.
                //    return null;
                //}

                // We need to save additional debugging information for MoveNext of an async state machine.
                //var stateMachineMethod = method as SynthesizedStateMachineMethod;
                //bool isStateMachineMoveNextMethod = stateMachineMethod != null && method.Name == WellKnownMemberNames.MoveNextMethodName;

                //if (isStateMachineMoveNextMethod && stateMachineMethod.StateMachineType.KickoffMethod.IsAsync)
                //{
                //    int asyncCatchHandlerOffset;
                //    ImmutableArray<int> asyncYieldPoints;
                //    ImmutableArray<int> asyncResumePoints;
                //    codeGen.Generate(out asyncCatchHandlerOffset, out asyncYieldPoints, out asyncResumePoints);

                //    var kickoffMethod = stateMachineMethod.StateMachineType.KickoffMethod;

                //    // The exception handler IL offset is used by the debugger to treat exceptions caught by the marked catch block as "user unhandled".
                //    // This is important for async void because async void exceptions generally result in the process being terminated,
                //    // but without anything useful on the call stack. Async Task methods on the other hand return exceptions as the result of the Task.
                //    // So it is undesirable to consider these exceptions "user unhandled" since there may well be user code that is awaiting the task.
                //    // This is a heuristic since it's possible that there is no user code awaiting the task.
                //    asyncDebugInfo = new Cci.AsyncMethodBodyDebugInfo(kickoffMethod, kickoffMethod.ReturnsVoid ? asyncCatchHandlerOffset : -1, asyncYieldPoints, asyncResumePoints);
                //}
                //else
                {
                    codeGen.Generate();
                }
            }, variableSlotAllocatorOpt, diagnostics, emittingPdb);
        }

        /// <summary>
        /// Generates method body that calls another method.
        /// Used for wrapping a method call into a method, e.g. an entry point.
        /// </summary>
        internal static MethodBody GenerateMethodBody(
            PEModuleBuilder moduleBuilder,
            MethodSymbol routine,
            Action<ILBuilder> builder,
            VariableSlotAllocator variableSlotAllocatorOpt,
            DiagnosticBag diagnostics,
            bool emittingPdb)
        {
            var compilation = moduleBuilder.Compilation;
            var localSlotManager = new LocalSlotManager(variableSlotAllocatorOpt);
            var optimizations = compilation.Options.OptimizationLevel;

            DebugDocumentProvider debugDocumentProvider = null;

            if (emittingPdb)
            {
                debugDocumentProvider = (path, basePath) => moduleBuilder.GetOrAddDebugDocument(path, basePath, CreateDebugDocumentForFile);
            }

            ILBuilder il = new ILBuilder(moduleBuilder, localSlotManager, optimizations);
            try
            {
                Cci.AsyncMethodBodyDebugInfo asyncDebugInfo = null;

                builder(il);

                //
                il.Realize();

                //
                var localVariables = il.LocalSlotManager.LocalsInOrder();

                if (localVariables.Length > 0xFFFE)
                {
                    //diagnosticsForThisMethod.Add(ErrorCode.ERR_TooManyLocals, method.Locations.First());
                }

                //if (diagnosticsForThisMethod.HasAnyErrors())
                //{
                //    // we are done here. Since there were errors we should not emit anything.
                //    return null;
                //}

                //// We will only save the IL builders when running tests.
                //if (moduleBuilder.SaveTestData)
                //{
                //    moduleBuilder.SetMethodTestData(method, builder.GetSnapshot());
                //}

                // Only compiler-generated MoveNext methods have iterator scopes.  See if this is one.
                var stateMachineHoistedLocalScopes = default(ImmutableArray<Cci.StateMachineHoistedLocalScope>);
                //if (isStateMachineMoveNextMethod)
                //{
                //    stateMachineHoistedLocalScopes = builder.GetHoistedLocalScopes();
                //}

                var stateMachineHoistedLocalSlots = default(ImmutableArray<EncHoistedLocalInfo>);
                var stateMachineAwaiterSlots = default(ImmutableArray<Cci.ITypeReference>);
                //if (optimizations == OptimizationLevel.Debug && stateMachineTypeOpt != null)
                //{
                //    Debug.Assert(method.IsAsync || method.IsIterator);
                //    GetStateMachineSlotDebugInfo(moduleBuilder, moduleBuilder.GetSynthesizedFields(stateMachineTypeOpt), variableSlotAllocatorOpt, diagnosticsForThisMethod, out stateMachineHoistedLocalSlots, out stateMachineAwaiterSlots);
                //    Debug.Assert(!diagnostics.HasAnyErrors());
                //}

                return new MethodBody(
                    il.RealizedIL,
                    il.MaxStack,
                    (Cci.IMethodDefinition)routine.PartialDefinitionPart ?? routine,
                    variableSlotAllocatorOpt?.MethodId ?? new DebugId(0, moduleBuilder.CurrentGenerationOrdinal),
                    localVariables,
                    il.RealizedSequencePoints,
                    debugDocumentProvider,
                    il.RealizedExceptionHandlers,
                    il.GetAllScopes(),
                    il.HasDynamicLocal,
                    null, // importScopeOpt,
                    ImmutableArray<LambdaDebugInfo>.Empty, // lambdaDebugInfo,
                    ImmutableArray<ClosureDebugInfo>.Empty, // closureDebugInfo,
                    null, //stateMachineTypeOpt?.Name,
                    stateMachineHoistedLocalScopes,
                    stateMachineHoistedLocalSlots,
                    stateMachineAwaiterSlots,
                    asyncDebugInfo);
            }
            finally
            {
                // Basic blocks contain poolable builders for IL and sequence points. Free those back
                // to their pools.
                il.FreeBasicBlocks();

                //// Remember diagnostics.
                //diagnostics.AddRange(diagnosticsForThisMethod);
                //diagnosticsForThisMethod.Free();
            }
        }

        static TypeSymbol EmitForwardMethodCall(ILBuilder il, CodeGenerator cg, ImmutableArray<ParameterSymbol> parameters, MethodSymbol forwardedmethod)
        {
            // TODO: move to CodeGenerator

            if (forwardedmethod == null)
                return cg.CoreTypes.Void;

            if (forwardedmethod.HasThis)
            {
                cg.EmitThis();
            }

            var source = 1;
            var calledparams = forwardedmethod.Parameters;
            for (int i = 0; i < calledparams.Length; i++)
            {
                var calledp = calledparams[i];
                if (SpecialParameterSymbol.IsContextParameter(calledp))
                {
                    cg.EmitLoadContext();
                }
                else if (source < parameters.Length)
                {
                    var srcp = parameters[source++];
                    if (srcp.CanBePassedTo(calledp))
                    {
                        new ParamPlace(srcp).EmitLoad(il);
                    }
                    else
                    {
                        throw new InvalidOperationException("Signature does not match.");
                    }
                }
                else
                {
                    throw new InvalidOperationException("Signature does not match.");
                }
            }

            //
            return cg.EmitCall(ILOpCode.Call, forwardedmethod);
        }

        internal static void EmitCtorBody(
            PEModuleBuilder moduleBuilder,
            SynthesizedCtorWrapperSymbol routine,
            DiagnosticBag diagnostics,
            bool emittingPdb)
        {
            Debug.Assert(!routine.IsStatic);

            var compilation = moduleBuilder.Compilation;
            var parameters = routine.Parameters;

            Debug.Assert(SpecialParameterSymbol.IsContextParameter(parameters[0]));    // first parameter always <ctx>
            Debug.Assert(routine.ReturnsVoid);

            var type = (SourceNamedTypeSymbol)routine.ContainingType;
            var ctxField = type.ContextField;       // .<ctx>
            var basector = routine.BaseCtor;  // base..ctor to be called

            // __construct to be called
            var phpctor = routine.PhpCtor;

            for (var t = type.BaseType; phpctor == null && t != null; t = t.BaseType)
            {
                phpctor = t.ResolvePhpCtor();   // lookups PHP constructor in base class if not implemented in this one
            }

            Debug.Assert(!type.IsStatic);

            var il = new ILBuilder(moduleBuilder, new LocalSlotManager(null), compilation.Options.OptimizationLevel);
            var cg = new CodeGenerator(il, moduleBuilder, diagnostics, OptimizationLevel.Release, emittingPdb, type, new ParamPlace(parameters[0]), new ArgPlace(type, 0));

            // initialize <ctx> field
            if (ctxField != null)
            {
                // if field is declared within this type
                if (object.ReferenceEquals(ctxField.ContainingType, type) || basector == null)
                {
                    var ctxFieldPlace = new FieldPlace(cg.ThisPlaceOpt, ctxField);

                    // Debug.Assert(<ctx> != null)
                    cg.EmitDebugAssertNotNull(cg.ContextPlaceOpt, "Context cannot be null.");

                    // <this>.<ctx> = <ctx>
                    ctxFieldPlace.EmitStorePrepare(il);
                    cg.EmitLoadContext();
                    ctxFieldPlace.EmitStore(il);
                }
            }

            // call base .ctor
            cg.EmitPop(EmitForwardMethodCall(il, cg, parameters, basector));

            // initialize class fields,
            // default(PhpValue) is not a valid value, its TypeTable must not be null
            foreach (var fld in type.GetFieldsToEmit().OfType<SourceFieldSymbol>().Where(fld => !fld.IsStatic && !fld.IsConst))
            {
                fld.EmitInit(cg);
            }

            // if (this.GetType() == typeof(type)) call phpctor
            if (phpctor != null)
            {
                object ifendLbl = new object();
                cg.EmitThis();
                cg.EmitCall(ILOpCode.Callvirt, cg.CoreTypes.Object.Method("GetType"));
                cg.EmitLoadToken(type, null);
                cg.EmitCall(ILOpCode.Call, (MethodSymbol)cg.DeclaringCompilation.GetWellKnownTypeMember(WellKnownMember.System_Type__GetTypeFromHandle));
                il.EmitBranch(ILOpCode.Bne_un, ifendLbl);  // value1 != value2
                if (true)
                {
                    cg.EmitPop(EmitForwardMethodCall(il, cg, parameters, phpctor));
                }

                il.MarkLabel(ifendLbl);
            }

            //
            il.EmitRet(true);

            //
            var body = CreateSynthesizedBody(moduleBuilder, routine, il);
            moduleBuilder.SetMethodBody(routine, body);
        }

        internal static MethodBody CreateSynthesizedBody(PEModuleBuilder moduleBuilder, IMethodSymbol routine, ILBuilder il)
        {
            var compilation = moduleBuilder.Compilation;
            var localSlotManager = il.LocalSlotManager;
            var optimizations = compilation.Options.OptimizationLevel;

            try
            {
                il.Realize();

                //
                var localVariables = il.LocalSlotManager.LocalsInOrder();

                if (localVariables.Length > 0xFFFE)
                {
                    //diagnosticsForThisMethod.Add(ErrorCode.ERR_TooManyLocals, method.Locations.First());
                }

                //if (diagnosticsForThisMethod.HasAnyErrors())
                //{
                //    // we are done here. Since there were errors we should not emit anything.
                //    return null;
                //}

                //// We will only save the IL builders when running tests.
                //if (moduleBuilder.SaveTestData)
                //{
                //    moduleBuilder.SetMethodTestData(method, builder.GetSnapshot());
                //}

                // Only compiler-generated MoveNext methods have iterator scopes.  See if this is one.
                var stateMachineHoistedLocalScopes = default(ImmutableArray<Cci.StateMachineHoistedLocalScope>);
                //if (isStateMachineMoveNextMethod)
                //{
                //    stateMachineHoistedLocalScopes = builder.GetHoistedLocalScopes();
                //}

                var stateMachineHoistedLocalSlots = default(ImmutableArray<EncHoistedLocalInfo>);
                var stateMachineAwaiterSlots = default(ImmutableArray<Cci.ITypeReference>);
                //if (optimizations == OptimizationLevel.Debug && stateMachineTypeOpt != null)
                //{
                //    Debug.Assert(method.IsAsync || method.IsIterator);
                //    GetStateMachineSlotDebugInfo(moduleBuilder, moduleBuilder.GetSynthesizedFields(stateMachineTypeOpt), variableSlotAllocatorOpt, diagnosticsForThisMethod, out stateMachineHoistedLocalSlots, out stateMachineAwaiterSlots);
                //    Debug.Assert(!diagnostics.HasAnyErrors());
                //}

                return new MethodBody(
                    il.RealizedIL,
                    il.MaxStack,
                    (Cci.IMethodDefinition)routine,
                    new DebugId(0, moduleBuilder.CurrentGenerationOrdinal),
                    localVariables,
                    il.RealizedSequencePoints,
                    null,
                    il.RealizedExceptionHandlers,
                    il.GetAllScopes(),
                    il.HasDynamicLocal,
                    null, // importScopeOpt,
                    ImmutableArray<LambdaDebugInfo>.Empty, // lambdaDebugInfo,
                    ImmutableArray<ClosureDebugInfo>.Empty, // closureDebugInfo,
                    null, //stateMachineTypeOpt?.Name,
                    stateMachineHoistedLocalScopes,
                    stateMachineHoistedLocalSlots,
                    stateMachineAwaiterSlots,
                    null);
            }
            finally
            {
                // Basic blocks contain poolable builders for IL and sequence points. Free those back
                // to their pools.
                il.FreeBasicBlocks();

                //// Remember diagnostics.
                //diagnostics.AddRange(diagnosticsForThisMethod);
                //diagnosticsForThisMethod.Free();
            }
        }
    }
}
