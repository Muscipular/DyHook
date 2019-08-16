using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Threading;
using FastExpressionCompiler;
using HarmonyLib;

namespace DynamicProxy
{
    public class InterceptorManager
    {
        protected static Harmony _harmony = new Harmony(typeof(InterceptorManager).FullName);
    }

    public class InterceptorManager<T> : InterceptorManager
    {
        public static ConcurrentDictionary<string, IInterceptor[]> Dictionary { get; } = new ConcurrentDictionary<string, IInterceptor[]>();

        public static ConcurrentDictionary<string, (DynamicMethod, DynamicMethod)> Dictionary2 { get; } = new ConcurrentDictionary<string, (DynamicMethod, DynamicMethod)>();

        public static bool Intercept()
        {
            if (typeof(T).IsInterface)
            {
                return true;
            }

            foreach (var methodInfo in typeof(T).GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static))
            {
                if (methodInfo.IsAbstract)
                {
                    continue;
                }
                var interceptorAttribute = methodInfo.GetCustomAttributes<InterceptorAttribute>(true);
                var s = $"{methodInfo.Name}_{methodInfo.MetadataToken}";
                var interceptors = interceptorAttribute.Select(e => (IInterceptor)Activator.CreateInstance(e.InterceptorType, e.InterceptorArguments)).ToArray();
                if (!interceptors.Any())
                {
                    continue;
                }
                Dictionary[s] = interceptors;
                var list = new List<(Type type, string name, bool isRef, bool isPara)>()
                {
                    (typeof(Dictionary<object, object>), "__state", true, false)
                };
                if (methodInfo.ReturnType != null)
                {
                    list.Add((methodInfo.ReturnType, "__result", true, false));
                }
                if (!methodInfo.IsStatic)
                {
                    list.Add((methodInfo.DeclaringType, "__instance", false, false));
                }
                list.Add((typeof(MethodInfo), "__originalMethod", false, false));
                int i = list.Count;
                foreach (var parameterInfo in methodInfo.GetParameters())
                {
                    list.Add((parameterInfo.ParameterType, parameterInfo.Name, true, true));
                }
                var pa = Prefix(list, s, i, methodInfo);
                var p2 = Postfix(list, s, i, methodInfo);
                Dictionary2[s] = (pa, p2);
                var factory1 = AccessTools.Method(typeof(Generator), "Prefix");
                var factory2 = AccessTools.Method(typeof(Generator), "Postfix");
                _harmony.Patch(methodInfo, new HarmonyMethod(factory1), new HarmonyMethod(factory2));
            }

            return true;
        }

        static class Generator
        {
            static DynamicMethod Prefix(MethodBase method)
            {
                var s = $"{method.Name}_{method.MetadataToken}";
                return Dictionary2[s].Item1;
            }

            static DynamicMethod Postfix(MethodBase method)
            {
                var s = $"{method.Name}_{method.MetadataToken}";
                return Dictionary2[s].Item2;
            }
        }


        private static DynamicMethod Postfix(List<(Type type, string name, bool isRef, bool isPara)> list, string key, int startIndex, MethodInfo methodInfo)
        {
            var parameterExpressions = list.Select(e => Expression.Parameter(e.isRef && !e.type.IsByRef ? e.type.MakeByRefType() : e.type, e.name)).ToArray();
            var interceptorListExp = Expression.Variable(typeof(IInterceptor[]));
            var interceptorContextExp = Expression.Variable(typeof(InterceptorContext));
            var iexp = Expression.Variable(typeof(int));
            var retExp = Expression.Variable(typeof(bool));
            var dicExp = Expression.Property(
                null,
                typeof(InterceptorManager<T>).GetProperty(nameof(Dictionary), BindingFlags.Static | BindingFlags.Public)
            );
            var interceptors = Expression.Property(dicExp, "Item", Expression.Constant(key));
            var expressions = new List<Expression>();
            Action<object> writeLine = Console.WriteLine;
            foreach (var p in parameterExpressions)
            {
                expressions.Add(Expression.Call(writeLine.Method, p));
            }
            expressions.Add(Expression.Assign(interceptorListExp, interceptors));
            expressions.Add(Expression.Assign(interceptorContextExp, Expression.New(
                typeof(InterceptorContext).GetConstructors().First(),
                parameterExpressions[startIndex - 1],
                methodInfo.IsStatic ? (Expression)Expression.Default(typeof(object)) : parameterExpressions[startIndex - 2],
                Expression.NewArrayInit(typeof(IDynamicAccessor),
                    parameterExpressions.Skip(startIndex).Select(e =>
                            Expression.New(
                                typeof(RefDynamicAccessor).GetConstructors().First(),
                                Expression.Convert(e, typeof(object))
                            ))),
                methodInfo.ReturnType == null ? (Expression)Expression.Default(typeof(RefDynamicAccessor)) : Expression.New(
                    typeof(RefDynamicAccessor).GetConstructors().First(),
                    Expression.Convert(parameterExpressions[1], typeof(object))
                ),
                parameterExpressions[0]
            )));
            // expressions.Add(Expression.Assign(iexp, Expression.ArrayLength(interceptorListExp)));
            var labelTarget = Expression.Label();
            var retTarget = Expression.Label();
            expressions.Add(Expression.Loop(Expression.Block(new Expression[]
            {
                Expression.IfThen(Expression.GreaterThanOrEqual(iexp, Expression.ArrayLength(interceptorListExp)), Expression.Goto(labelTarget)),
                Expression.IfThen(
                    Expression.Call(
                        Expression.ArrayIndex(interceptorListExp, iexp),
                        method: typeof(IInterceptor).GetMethod(nameof(IInterceptor.AfterProcess)),
                        new[] { interceptorContextExp }),
                    Expression.Block(Expression.Assign(retExp, Expression.Constant(true)), Expression.Goto(retTarget))
                ),
                Expression.Increment(iexp),
            }), labelTarget));
            expressions.Add(Expression.Label(retTarget));
            expressions.Add(Expression.Assign(iexp, Expression.Constant(0)));
            for (var index = startIndex; index < parameterExpressions.Length; index++)
            {
                var expression = parameterExpressions[index];
                expressions.Add(
                    Expression.Assign(
                        expression,
                        Expression.Convert(
                            expression: Expression.Call(
                                Expression.Property(Expression.Property(interceptorContextExp, "Parameters"), "Item", Expression.Constant(index - startIndex)),
                                "GetValue",
                                new Type[0]),
                            type: expression.Type.IsByRef ? expression.Type.GetElementType() : expression.Type)));
            }
            if (methodInfo.ReturnType != null)
            {
                expressions.Add(Expression.Assign(parameterExpressions[1], Expression.Convert(
                    expression: Expression.Call(Expression.Property(interceptorContextExp, "ReturnValue"),
                        "GetValue",
                        new Type[0]),
                    type: parameterExpressions[1].Type.IsByRef ? parameterExpressions[1].Type.GetElementType() : parameterExpressions[1].Type)));
            }
            var block = Expression.Block(typeof(void), new[] { iexp, retExp, interceptorContextExp, interceptorListExp }, expressions);
            var lambdaExpression = Expression.Lambda(block, parameterExpressions);
            var pa = lambdaExpression.Compile();
            var method = DynamicMethod(pa, parameterExpressions);
            return method;
        }

        private static AssemblyBuilder _assemblyBuilder;

        private static ModuleBuilder _defineDynamicModule;

        static InterceptorManager()
        {
            _assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName("AAAA_11"), AssemblyBuilderAccess.Run);
            _defineDynamicModule = _assemblyBuilder.DefineDynamicModule("AA");
        }

        private static object closure;
        
        private static DynamicMethod Prefix(List<(Type type, string name, bool isRef, bool isPara)> list, string key, int startIndex, MethodInfo methodInfo)
        {
            var parameterExpressions = list.Select(e => Expression.Parameter(e.isRef && !e.type.IsByRef ? e.type.MakeByRefType() : e.type, e.name)).ToArray();
            var interceptorListExp = Expression.Variable(typeof(IInterceptor[]));
            var interceptorContextExp = Expression.Variable(typeof(InterceptorContext));
            var iexp = Expression.Variable(typeof(int));
            var retExp = Expression.Variable(typeof(bool));
            var dicExp = Expression.Property(null, typeof(InterceptorManager<T>).GetProperty(nameof(Dictionary), BindingFlags.Static | BindingFlags.Public));
            var interceptors = Expression.Property(dicExp, "Item", Expression.Constant(key));
            var expressions = new List<Expression>();
            Action<object> writeLine = Console.WriteLine;
            foreach (var p in parameterExpressions)
            {
                expressions.Add(Expression.Call(writeLine.Method, p));
            }
            expressions.Add(Expression.Assign(interceptorListExp, interceptors));
            expressions.Add(Expression.Assign(parameterExpressions[0], Expression.New(typeof(Dictionary<object, object>))));
            expressions.Add(Expression.Assign(interceptorContextExp, Expression.New(
                typeof(InterceptorContext).GetConstructors().First(),
                parameterExpressions[startIndex - 1],
                methodInfo.IsStatic ? (Expression)Expression.Default(typeof(object)) : parameterExpressions[startIndex - 2],
                Expression.NewArrayInit(typeof(IDynamicAccessor),
                    parameterExpressions.Skip(startIndex).Select(e =>
                            Expression.New(
                                typeof(RefDynamicAccessor).GetConstructors().First(),
                                Expression.Convert(e, typeof(object))
                            ))),
                methodInfo.ReturnType == null ? (Expression)Expression.Default(typeof(RefDynamicAccessor)) : Expression.New(
                    typeof(RefDynamicAccessor).GetConstructors().First(),
                    Expression.Convert(parameterExpressions[1], typeof(object))
                ),
                parameterExpressions[0]
            )));
            // expressions.Add(Expression.Assign(iexp, Expression.ArrayLength(interceptorListExp)));
            var labelTarget = Expression.Label();
            var retTarget = Expression.Label();
            expressions.Add(Expression.Loop(Expression.Block(new Expression[]
            {
                Expression.IfThen(Expression.GreaterThanOrEqual(iexp, Expression.ArrayLength(interceptorListExp)), Expression.Goto(labelTarget)),
                Expression.IfThen(
                    Expression.Call(
                        Expression.ArrayIndex(interceptorListExp, iexp),
                        method: typeof(IInterceptor).GetMethod(nameof(IInterceptor.BeforeProcess)),
                        new[] { interceptorContextExp }),
                    Expression.Block(Expression.Assign(retExp, Expression.Constant(true)), Expression.Goto(retTarget))
                ),
                Expression.Increment(iexp),
            }), labelTarget));
            expressions.Add(Expression.Label(retTarget));
            expressions.Add(Expression.Assign(iexp, Expression.Constant(0)));
            for (var index = startIndex; index < parameterExpressions.Length; index++)
            {
                var expression = parameterExpressions[index];
                expressions.Add(
                    Expression.Assign(
                        expression,
                        Expression.Convert(
                            expression: Expression.Call(
                                Expression.Property(Expression.Property(interceptorContextExp, "Parameters"), "Item", Expression.Constant(index - startIndex)),
                                "GetValue",
                                new Type[0]),
                            type: expression.Type.IsByRef ? expression.Type.GetElementType() : expression.Type)));
            }
            if (methodInfo.ReturnType != null)
            {
                expressions.Add(Expression.Assign(parameterExpressions[1], Expression.Convert(
                    expression: Expression.Call(Expression.Property(interceptorContextExp, "ReturnValue"),
                        "GetValue",
                        new Type[0]),
                    type: parameterExpressions[1].Type.IsByRef ? parameterExpressions[1].Type.GetElementType() : parameterExpressions[1].Type)));
            }
            expressions.Add(retExp);
            var block = Expression.Block(typeof(bool), new[] { iexp, retExp, interceptorContextExp, interceptorListExp }, expressions);
            var lambdaExpression = Expression.Lambda(block, parameterExpressions);
            var pa = lambdaExpression.Compile();
            var method = DynamicMethod(pa, parameterExpressions);
            return method;
        }

        private static DynamicMethod DynamicMethod(Delegate pa, ParameterExpression[] parameterExpressions)
        {
            var parameterTypes = parameterExpressions.Select(e => e.IsByRef ? e.Type.MakeByRefType() : e.Type).ToArray();
            closure = pa.Target;
            var method = new DynamicMethod(pa.Method.Name, pa.Method.ReturnType, parameterTypes);
            for (var index = 0; index < parameterExpressions.Length; index++)
            {
                var expression = parameterExpressions[index];
                method.DefineParameter(index + 1, ParameterAttributes.None, expression.Name);
            }
            var ilGenerator = method.GetILGenerator();
            var fieldInfo = AccessTools.Field(typeof(InterceptorManager<T>), nameof(closure));
            ilGenerator.Emit(OpCodes.Ldsfld, fieldInfo);
            for (int i = 0; i < parameterExpressions.Length; i++)
            {
                ilGenerator.Emit(OpCodes.Ldarg, i);
            }
            ilGenerator.Emit(OpCodes.Call, (MethodInfo)pa.Method);
            // ilGenerator.Emit(OpCodes.Call, (MethodInfo)pa.Method.GetType().GetField("m_owner", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance).GetValue(pa.Method));
            ilGenerator.Emit(OpCodes.Ret);
            return method;
        }
    }

    public interface IInterceptor
    {
        bool BeforeProcess(InterceptorContext ctx);

        bool AfterProcess(InterceptorContext ctx);
    }

    public interface IInterceptorContext
    {
        MethodInfo Method { get; }

        object Target { get; }

        IReadOnlyList<IDynamicAccessor> Parameters { get; }

        IDynamicAccessor ReturnValue { get; }

        IDictionary<object, object> Context { get; set; }
    }

    public class InterceptorContext : IInterceptorContext
    {
        public InterceptorContext(MethodInfo method, object target, IReadOnlyList<IDynamicAccessor> parameters, IDynamicAccessor returnValue, IDictionary<object, object> context)
        {
            Method = method;
            Target = target;
            Parameters = parameters;
            ReturnValue = returnValue;
            Context = context;
        }

        public MethodInfo Method { get; set; }

        public object Target { get; set; }

        public IReadOnlyList<IDynamicAccessor> Parameters { get; set; }

        public IDynamicAccessor ReturnValue { get; set; }

        public IDictionary<object, object> Context { get; set; }
    }

    public interface IDynamicAccessor
    {
        object GetValue();

        bool CanGet { get; }

        bool CanSet { get; }

        void SetValue(object v);
    }

    public class RefDynamicAccessor : IDynamicAccessor
    {
        public object Value { get; set; }

        public RefDynamicAccessor(object f)
        {
            this.Value = f;
        }

        public object GetValue()
        {
            return Value;
        }

        public bool CanGet => true;

        public bool CanSet => true;

        public void SetValue(object v)
        {
            Value = v;
        }
    }

    [AttributeUsage(AttributeTargets.Method)]
    public class InterceptorAttribute : Attribute
    {
        public InterceptorAttribute(Type interceptorType)
        {
            InterceptorType = interceptorType;
        }

        public Type InterceptorType { get; }

        public object[] InterceptorArguments { get; set; } = Array.Empty<object>();
    }

    public class PropDynamicAccessor : IDynamicAccessor
    {
        private PropertyInfo _fieldInfo;

        private Func<object> Getter;

        private Action<object> Setter;

        public PropDynamicAccessor(object instance, PropertyInfo fieldInfo)
        {
            _fieldInfo = fieldInfo;
            var constant = Expression.Constant(instance, fieldInfo.DeclaringType);
            if (!fieldInfo.CanRead)
            {
                Getter = Expression.Lambda<Func<object>>(Expression.Convert(Expression.Property(constant, _fieldInfo), typeof(object))).CompileFast();
            }
            if (fieldInfo.CanWrite)
            {
                var varExp = Expression.Variable(fieldInfo.DeclaringType);
                Setter = Expression.Lambda<Action<object>>(Expression.Assign(Expression.Property(constant, fieldInfo), Expression.Convert(varExp, fieldInfo.PropertyType)), varExp).CompileFast();
            }
        }

        public object GetValue()
        {
            if (!CanGet)
            {
                throw new AccessorException($"{_fieldInfo.DeclaringType.FullName}.{_fieldInfo.Name} is writeonly.");
            }
            return Getter();
        }

        public bool CanGet => Getter != null;

        public bool CanSet => Setter != null;

        public void SetValue(object v)
        {
            if (!CanSet)
            {
                throw new AccessorException($"{_fieldInfo.DeclaringType.FullName}.{_fieldInfo.Name} is readonly.");
            }
            Setter(v);
        }
    }

    public class DynamicAccessor : IDynamicAccessor
    {
        private FieldInfo _fieldInfo;

        private Func<object> Getter;

        private Action<object> Setter;

        public DynamicAccessor(object instance, FieldInfo fieldInfo)
        {
            _fieldInfo = fieldInfo;
            var constant = Expression.Constant(instance, fieldInfo.DeclaringType);
            Getter = Expression.Lambda<Func<object>>(Expression.Convert(Expression.Field(constant, _fieldInfo), typeof(object))).CompileFast();
            if (!fieldInfo.IsInitOnly)
            {
                var varExp = Expression.Variable(fieldInfo.DeclaringType);
                Setter = Expression.Lambda<Action<object>>(Expression.Assign(Expression.Field(constant, fieldInfo), Expression.Convert(varExp, fieldInfo.FieldType)), varExp).CompileFast();
            }
        }

        public object GetValue() => Getter();

        public bool CanGet => Getter != null;

        public bool CanSet => Setter != null;

        public void SetValue(object v)
        {
            if (Setter == null)
            {
                throw new AccessorException($"{_fieldInfo.DeclaringType.FullName}.{_fieldInfo.Name} is readonly.");
            }
            Setter(v);
        }
    }

    public class AccessorException : Exception
    {
        public AccessorException()
        {
        }

        public AccessorException(string message) : base(message)
        {
        }
    }
}