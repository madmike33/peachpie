﻿using Microsoft.CodeAnalysis;
using Pchp.CodeAnalysis.CodeGen;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Metadata;
using System.Text;
using System.Threading.Tasks;

namespace Pchp.CodeAnalysis.Symbols
{
    partial class NamedTypeSymbol
    {
        /// <summary>
        /// Gets special <c>_statics</c> nested class holding static fields bound to context.
        /// </summary>
        /// <returns></returns>
        internal TypeSymbol TryGetStatics()
            => GetTypeMembers(WellKnownPchpNames.StaticsHolderClassName)
                .Where(t => t.Arity == 0 && t.DeclaredAccessibility == Accessibility.Public && !t.IsStatic)
                .SingleOrDefault();

        /// <summary>
        /// Tries to find field with given name that can be used as a static field.
        /// Lookups through the class inheritance.
        /// Does not handle member visibility.
        /// </summary>
        internal FieldSymbol ResolveStaticField(string name)
        {
            FieldSymbol field = null;

            for (var t = this; t != null && field == null; t = t.BaseType)
            {
                field = t.GetMembers(name).OfType<FieldSymbol>().SingleOrDefault();
                if (field == null)
                {
                    var statics = t.TryGetStatics();
                    if (statics != null)
                    {
                        field = statics.GetMembers(name).OfType<FieldSymbol>().SingleOrDefault();
                    }
                }
            }

            return field;
        }

        /// <summary>
        /// Emits load of statics holder.
        /// </summary>
        internal TypeSymbol EmitLoadStatics(CodeGenerator cg)
        {
            var statics = TryGetStatics();

            if (statics != null && statics.GetMembers().OfType<IFieldSymbol>().Any())
            {
                // Template: <ctx>.GetStatics<_statics>()
                cg.EmitLoadContext();
                return cg.EmitCall(ILOpCode.Callvirt, cg.CoreMethods.Context.GetStatic_T.Symbol.Construct(statics))
                    .Expect(statics);
            }

            return null;
        }
    }

    partial class SourceNamedTypeSymbol
    {
        internal void EmitInit(Emit.PEModuleBuilder module)
        {
            // .cctor
            EmitFieldsCctor(module);

            // __statics.Init
            var statics = this.EnsureStaticsContainer();
            if (statics != null && !statics.IsEmpty)
                statics.EmitCtors(module);

            // IPhpCallable.Invoke
            EmitInvoke(EnsureInvokeMethod(), module);
        }

        void EmitFieldsCctor(Emit.PEModuleBuilder module)
        {
            var sflds = GetMembers().OfType<SourceFieldSymbol>().Where(f => f.IsStatic).ToArray();
            if (sflds.Length != 0)
            {
                // emit initialization of app static fields
                // note, their initializers do not have Context available, since they are not bound to a Context

                var cctor = module.GetStaticCtorBuilder(this);
                var cg = new CodeGenerator(cctor, module, DiagnosticBag.GetInstance(), OptimizationLevel.Release, false, this, null, null);

                foreach (var f in sflds)
                {
                    f.EmitInit(cg);
                }
            }
        }

        void EmitInvoke(MethodSymbol invoke, Emit.PEModuleBuilder module)
        {
            if (invoke == null)
            {
                return;
            }

            module.SetMethodBody(invoke, MethodGenerator.GenerateMethodBody(module, invoke, il =>
            {
                var cg = new CodeGenerator(il, module, DiagnosticBag.GetInstance(), OptimizationLevel.Release, false, this, new ParamPlace(invoke.Parameters[0]), new ArgPlace(this, 0));
                //var __invoke = (MethodSymbol)GetMembers(Pchp.Syntax.Name.SpecialMethodNames.Invoke.Value).Single(s => s is MethodSymbol);

                // TODO: call __invoke() directly

                // context.Call<T>(T, TypeMethods.MagicMethods, params PhpValue[])
                var call_t = cg.CoreTypes.Context.Symbol.GetMembers("Call")
                    .OfType<MethodSymbol>()
                    .Where(s => s.Arity == 1 && s.ParameterCount == 3 && s.Parameters[2].IsParams)
                    .Single()
                    .Construct(this);

                // return context.Call<T>(this, __invoke, args)
                cg.EmitLoadContext();
                cg.EmitThis();
                cg.Builder.EmitIntConstant((int)Core.Reflection.TypeMethods.MagicMethods.__invoke);
                cg.Builder.EmitLoadArgumentOpcode(2);
                cg.EmitCall(ILOpCode.Call, call_t);
                cg.EmitRet(false);

            }, null, DiagnosticBag.GetInstance(), false));
        }
    }
}
