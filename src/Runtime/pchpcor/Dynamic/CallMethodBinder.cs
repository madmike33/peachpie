﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Dynamic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace Pchp.Core.Dynamic
{
    public abstract class CallMethodBinder : DynamicMetaObjectBinder
    {
        protected readonly Type _returnType;
        protected readonly int _genericParamsCount;
        protected readonly Type _classContext;

        protected abstract string ResolveName(DynamicMetaObject[] args, ref BindingRestrictions restrictions);

        #region Factory

        protected CallMethodBinder(RuntimeTypeHandle classContext, RuntimeTypeHandle returnType, int genericParams)
        {
            _returnType = Type.GetTypeFromHandle(returnType);
            _genericParamsCount = genericParams;
            _classContext = Type.GetTypeFromHandle(classContext);
        }

        public static CallMethodBinder Create(string name, RuntimeTypeHandle classContext, RuntimeTypeHandle returnType, int genericParams)
        {
            if (name != null)
            {
                // direct method call
                return new DirectCallMethodBinder(name, classContext, returnType, genericParams);
            }

            throw new NotImplementedException();
        }

        #endregion

        #region DynamicMetaObjectBinder

        public override DynamicMetaObject Bind(DynamicMetaObject target, DynamicMetaObject[] args)
        {
            BindingRestrictions restrictions = BindingRestrictions.Empty;

            if (target.Value == null)
            {
                throw new NotImplementedException();    // TODO: call on NULL
            }

            var methodName = ResolveName(args, ref restrictions);
            var targetType = target.Value.GetType();
            var method = targetType.GetTypeInfo().GetDeclaredMethod(methodName);

            restrictions = restrictions.Merge(BindingRestrictions.GetTypeRestriction(target.Expression, target.RuntimeType));
            var invocation = Expression.Call(Expression.Convert(target.Expression, targetType), method, BindArguments(method.GetParameters(), args, ref restrictions));

            return new DynamicMetaObject(ConvertExpression.Bind(invocation, _returnType), restrictions);
        }

        #endregion

        Expression[] BindArguments(ParameterInfo[] parameters, DynamicMetaObject[] args, ref BindingRestrictions restrictions)
        {
            var bound = new List<Expression>();
            var ctx = args[0].Expression;

            var arg_index = 1;

            for (int i = 0; i < parameters.Length; i++)
            {
                var p = parameters[i];
                if (p.ParameterType == typeof(Context))
                {
                    bound.Add(ctx);
                    continue;
                }

                var arg = (arg_index < args.Length) ? args[arg_index ++] : null;

                // bind arg
                if (arg != null)
                {
                    // restriction
                    if (arg.RuntimeType != arg.Expression.Type)
                    {
                        restrictions = restrictions.Merge(BindingRestrictions.GetTypeRestriction(arg.Expression, arg.RuntimeType));
                    }

                    bound.Add(ConvertExpression.Bind(arg.Expression, p.ParameterType));
                }
                else
                {
                    throw new NotImplementedException();    // TODO: load default value
                }
            }

            //
            return bound.ToArray();
        }
    }

    sealed class DirectCallMethodBinder : CallMethodBinder
    {
        readonly string _name;

        public DirectCallMethodBinder(string name, RuntimeTypeHandle classContext, RuntimeTypeHandle returnType, int genericParams)
            :base(classContext, returnType, genericParams)
        {
            Debug.Assert(name != null);
            _name = name;
        }

        protected override string ResolveName(DynamicMetaObject[] args, ref BindingRestrictions restrictions)
        {
            return _name;
        }
    }
}