﻿using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Reflection.Emit;
using System.Runtime.CompilerServices;
using System.Threading;
using FastExpressionCompiler;
using HarmonyLib;
using Sigil;

namespace DynamicProxy
{
    public class InterceptorManager
    {
        protected static Harmony _harmony;

        protected static AssemblyBuilder _assemblyBuilder;

        protected static ModuleBuilder _dynamicModule;

        static InterceptorManager()
        {
            _assemblyBuilder = AssemblyBuilder.DefineDynamicAssembly(new AssemblyName(nameof(InterceptorManager)), AssemblyBuilderAccess.Run);
            _dynamicModule = _assemblyBuilder.DefineDynamicModule("m");
            _harmony = new Harmony(typeof(InterceptorManager).FullName);
        }
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
            [MethodImpl(MethodImplOptions.NoInlining)]
            static DynamicMethod Prefix(MethodBase method)
            {
                var s = $"{method.Name}_{method.MetadataToken}";
                return Dictionary2[s].Item1;
            }

            [MethodImpl(MethodImplOptions.NoInlining)]
            static DynamicMethod Postfix(MethodBase method)
            {
                var s = $"{method.Name}_{method.MetadataToken}";
                return Dictionary2[s].Item2;
            }
        }


        private static DynamicMethod Postfix(List<(Type type, string name, bool isRef, bool isPara)> list, string key, int startIndex, MethodInfo methodInfo)
        {
            var parameterTypes = list.Select(e => e.isRef && !e.type.IsByRef ? e.type.MakeByRefType() : e.type).ToArray();
            var dynamicMethod = Sigil.NonGeneric.Emit.NewDynamicMethod(typeof(void), parameterTypes, $"{key}_postfix");
            dynamicMethod.DeclareLocal(typeof(int), "ix");
            dynamicMethod.DeclareLocal(typeof(InterceptorContext), "ctx");
            dynamicMethod.DeclareLocal(typeof(IInterceptor[]), "list");
            dynamicMethod.DeclareLocal(typeof(IDynamicAccessor[]), "accessors");
            // dynamicMethod.DeclareLocal(typeof(bool), "ret");
            dynamicMethod.Call(typeof(InterceptorManager<T>).GetProperty(nameof(Dictionary), BindingFlags.Static | BindingFlags.Public).GetMethod);
            dynamicMethod.LoadConstant(key);
            dynamicMethod.CallVirtual(typeof(ConcurrentDictionary<string, IInterceptor[]>).GetProperty("Item").GetMethod);
            dynamicMethod.StoreLocal("list");
            dynamicMethod.LoadConstant(list.Count - startIndex);
            dynamicMethod.NewArray<IDynamicAccessor>();
            dynamicMethod.StoreLocal("accessors");
            for (int i = startIndex; i < list.Count; i++)
            {
                dynamicMethod.LoadLocal("accessors");
                dynamicMethod.LoadConstant(i - startIndex);
                dynamicMethod.LoadArgument((ushort)i);
                var elementType = list[i].type.IsByRef ? list[i].type.GetElementType() : list[i].type;
                if (list[i].isRef || list[i].type.IsByRef)
                {
                    dynamicMethod.LoadIndirect(elementType);
                }
                if (elementType.IsValueType)
                {
                    dynamicMethod.Box(elementType);
                }
                dynamicMethod.NewObject(typeof(RefDynamicAccessor).GetConstructors().First());
                dynamicMethod.StoreElement<IDynamicAccessor>();
            }
            dynamicMethod.LoadArgument((ushort)(startIndex - 1));
            if (methodInfo.IsStatic)
            {
                dynamicMethod.LoadNull();
            }
            else
            {
                dynamicMethod.LoadArgument((ushort)(startIndex - 2));
            }
            dynamicMethod.LoadLocal("accessors");
            if (methodInfo.ReturnType == null || methodInfo.ReturnType == typeof(void))
            {
                dynamicMethod.LoadNull();
            }
            else
            {
                dynamicMethod.LoadArgument(1);
                var elementType = list[1].type.IsByRef ? list[1].type.GetElementType() : list[1].type;
                if (list[1].isRef || list[1].type.IsByRef)
                {
                    dynamicMethod.LoadIndirect(elementType);
                }
                if (elementType.IsValueType)
                {
                    dynamicMethod.Box(elementType);
                }
                dynamicMethod.NewObject(typeof(RefDynamicAccessor).GetConstructors().First());
            }
            // dynamicMethod.LoadNull();
            dynamicMethod.LoadArgument(0);
            dynamicMethod.LoadIndirect(typeof(Dictionary<object, object>));
            dynamicMethod.NewObject(typeof(InterceptorContext).GetConstructors().First());
            dynamicMethod.StoreLocal("ctx");
            dynamicMethod.DefineLabel("for1");
            dynamicMethod.DefineLabel("break1");
            dynamicMethod.LoadConstant(0);
            dynamicMethod.StoreLocal("ix");
            dynamicMethod.MarkLabel("for1");
            dynamicMethod.LoadLocal("ix");
            dynamicMethod.LoadLocal("list");
            dynamicMethod.LoadLength<IInterceptor>();
            dynamicMethod.BranchIfGreater("break1");

            dynamicMethod.LoadLocal("list");
            dynamicMethod.LoadLocal("ix");
            dynamicMethod.LoadElement<IInterceptor>();
            dynamicMethod.LoadLocal("ctx");
            dynamicMethod.CallVirtual(AccessTools.Method(typeof(IInterceptor), nameof(IInterceptor.AfterProcess)));
            dynamicMethod.BranchIfTrue("break1");

            dynamicMethod.LoadConstant(1);
            dynamicMethod.LoadLocal("ix");
            dynamicMethod.Add();
            dynamicMethod.StoreLocal("ix");
            dynamicMethod.MarkLabel("break1");

            for (int i = startIndex; i < list.Count; i++)
            {
                var elementType = list[i].type.IsByRef ? list[i].type.GetElementType() : list[i].type;
                if (list[i].isRef || list[i].type.IsByRef)
                {
                    dynamicMethod.LoadArgument((ushort)i);
                }
                dynamicMethod.LoadLocal("ctx");
                dynamicMethod.CallVirtual(AccessTools.PropertyGetter(typeof(InterceptorContext), nameof(InterceptorContext.Parameters)))
                        .CastClass<IDynamicAccessor[]>()
                        .LoadConstant(i - startIndex)
                        .LoadElement<IDynamicAccessor>()
                        .CallVirtual(AccessTools.Method(typeof(IDynamicAccessor), nameof(IDynamicAccessor.GetValue)));
                if (elementType.IsValueType)
                {
                    dynamicMethod.UnboxAny(elementType);
                }
                else
                {
                    dynamicMethod.CastClass(elementType);
                }
                if (list[i].isRef || list[i].type.IsByRef)
                {
                    dynamicMethod.StoreIndirect(elementType);
                }
                else
                {
                    dynamicMethod.StoreArgument((ushort)i);
                }
            }
            if (!(methodInfo.ReturnType == null || methodInfo.ReturnType == typeof(void)))
            {
                var elementType = list[1].type.IsByRef ? list[1].type.GetElementType() : list[1].type;
                if (list[1].isRef || list[1].type.IsByRef)
                {
                    dynamicMethod.LoadArgument(1);
                }
                dynamicMethod.LoadLocal("ctx");
                dynamicMethod.CallVirtual(AccessTools.PropertyGetter(typeof(InterceptorContext), nameof(InterceptorContext.ReturnValue)))
                        .CallVirtual(AccessTools.Method(typeof(IDynamicAccessor), nameof(IDynamicAccessor.GetValue)));

                if (elementType.IsValueType)
                {
                    dynamicMethod.UnboxAny(elementType);
                }
                else
                {
                    dynamicMethod.CastClass(elementType);
                }
                if (list[1].isRef || list[1].type.IsByRef)
                {
                    dynamicMethod.StoreIndirect(elementType);
                }
                else
                {
                    dynamicMethod.StoreArgument(1);
                }
            }
            dynamicMethod.Return();

            var delegateType = CreateDelegateType(key, parameterTypes, typeof(void));
            var d = dynamicMethod.CreateDelegate(delegateType, out var ccc);
            Console.WriteLine(ccc);
            var dMethod = d.Method;
            var dm = AccessTools.Field(dMethod.GetType(), "m_owner").GetValue(dMethod) as DynamicMethod;
            for (var i = 0; i < dm.GetParameters().Length; i++)
            {
                var parameterInfo = dm.GetParameters()[i];
                AccessTools.Field(parameterInfo.GetType(), "NameImpl").SetValue(parameterInfo, list[i].name);
            }
            return dm;
        }

        private static Type CreateDelegateType(string key, Type[] parameterTypes, Type returnType)
        {
            var typeBuilder = _dynamicModule.DefineType(key, TypeAttributes.Public | TypeAttributes.AutoLayout | TypeAttributes.AnsiClass | TypeAttributes.Sealed, typeof(MulticastDelegate));
            typeBuilder.DefineConstructor(
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.SpecialName | MethodAttributes.RTSpecialName,
                CallingConventions.Standard, new[] { typeof(object), typeof(IntPtr) }
            ).SetImplementationFlags(MethodImplAttributes.Runtime);
            var methodBuilder = typeBuilder.DefineMethod("Invoke",
                MethodAttributes.Public | MethodAttributes.HideBySig | MethodAttributes.NewSlot | MethodAttributes.Virtual,
                CallingConventions.Standard, returnType, parameterTypes);
            methodBuilder.SetImplementationFlags(MethodImplAttributes.Runtime);

            var delegateType = typeBuilder.CreateType();
            return delegateType;
        }

        private static DynamicMethod Prefix(List<(Type type, string name, bool isRef, bool isPara)> list, string key, int startIndex, MethodInfo methodInfo)
        {
            var parameterTypes = list.Select(e => e.isRef && !e.type.IsByRef ? e.type.MakeByRefType() : e.type).ToArray();
            var dynamicMethod = Sigil.NonGeneric.Emit.NewDynamicMethod(typeof(bool), parameterTypes, $"{key}_prefix");
            dynamicMethod.DeclareLocal(typeof(int), "ix");
            dynamicMethod.DeclareLocal(typeof(InterceptorContext), "ctx");
            dynamicMethod.DeclareLocal(typeof(IInterceptor[]), "list");
            dynamicMethod.DeclareLocal(typeof(IDynamicAccessor[]), "accessors");
            dynamicMethod.DeclareLocal(typeof(bool), "ret");
            dynamicMethod.DeclareLocal(typeof(Dictionary<object, object>), "opt");
            dynamicMethod.Call(typeof(InterceptorManager<T>).GetProperty(nameof(Dictionary), BindingFlags.Static | BindingFlags.Public).GetMethod);
            dynamicMethod.LoadConstant(key);
            dynamicMethod.CallVirtual(typeof(ConcurrentDictionary<string, IInterceptor[]>).GetProperty("Item").GetMethod);
            dynamicMethod.StoreLocal("list");
            dynamicMethod.LoadConstant(list.Count - startIndex);
            dynamicMethod.NewArray<IDynamicAccessor>();
            dynamicMethod.StoreLocal("accessors");
            dynamicMethod.NewObject(typeof(Dictionary<object, object>).GetConstructor(Array.Empty<Type>()));
            dynamicMethod.StoreLocal("opt");
            for (int i = startIndex; i < list.Count; i++)
            {
                dynamicMethod.LoadLocal("accessors");
                dynamicMethod.LoadConstant(i - startIndex);
                dynamicMethod.LoadArgument((ushort)i);
                var elementType = list[i].type.IsByRef ? list[i].type.GetElementType() : list[i].type;
                if (list[i].isRef || list[i].type.IsByRef)
                {
                    dynamicMethod.LoadIndirect(elementType);
                }
                if (elementType.IsValueType)
                {
                    dynamicMethod.Box(elementType);
                }
                dynamicMethod.NewObject(typeof(RefDynamicAccessor).GetConstructors().First());
                dynamicMethod.StoreElement<IDynamicAccessor>();
            }
            dynamicMethod.LoadArgument((ushort)(startIndex - 1));
            if (methodInfo.IsStatic)
            {
                dynamicMethod.LoadNull();
            }
            else
            {
                dynamicMethod.LoadArgument((ushort)(startIndex - 2));
            }
            dynamicMethod.LoadLocal("accessors");
            if (methodInfo.ReturnType == null || methodInfo.ReturnType == typeof(void))
            {
                dynamicMethod.LoadNull();
            }
            else
            {
                dynamicMethod.LoadArgument(1);
                var elementType = list[1].type.IsByRef ? list[1].type.GetElementType() : list[1].type;
                if (list[1].isRef || list[1].type.IsByRef)
                {
                    dynamicMethod.LoadIndirect(elementType);
                }
                if (elementType.IsValueType)
                {
                    dynamicMethod.Box(elementType);
                }
                dynamicMethod.NewObject(typeof(RefDynamicAccessor).GetConstructors().First());
            }
            // dynamicMethod.LoadNull();
            // dynamicMethod.LoadArgument(0);
            // dynamicMethod.LoadIndirect(typeof(Dictionary<object, object>));
            dynamicMethod.LoadLocal("opt");
            dynamicMethod.NewObject(typeof(InterceptorContext).GetConstructors().First());
            dynamicMethod.StoreLocal("ctx");
            dynamicMethod.DefineLabel("for1");
            dynamicMethod.DefineLabel("break1");
            dynamicMethod.LoadConstant(0);
            dynamicMethod.StoreLocal("ix");
            dynamicMethod.MarkLabel("for1");
            dynamicMethod.LoadLocal("ix");
            dynamicMethod.LoadLocal("list");
            dynamicMethod.LoadLength<IInterceptor>();
            dynamicMethod.BranchIfGreater("break1");

            dynamicMethod.LoadLocal("list");
            dynamicMethod.LoadLocal("ix");
            dynamicMethod.LoadElement<IInterceptor>();
            dynamicMethod.LoadLocal("ctx");
            dynamicMethod.CallVirtual(AccessTools.Method(typeof(IInterceptor), nameof(IInterceptor.BeforeProcess)));
            dynamicMethod.Duplicate().StoreLocal("ret");
            dynamicMethod.BranchIfTrue("break1");

            dynamicMethod.LoadConstant(1);
            dynamicMethod.LoadLocal("ix");
            dynamicMethod.Add();
            dynamicMethod.StoreLocal("ix");
            dynamicMethod.MarkLabel("break1");

            for (int i = startIndex; i < list.Count; i++)
            {
                var elementType = list[i].type.IsByRef ? list[i].type.GetElementType() : list[i].type;
                if (list[i].isRef || list[i].type.IsByRef)
                {
                    dynamicMethod.LoadArgument((ushort)i);
                }
                dynamicMethod.LoadLocal("ctx");
                dynamicMethod.CallVirtual(AccessTools.PropertyGetter(typeof(InterceptorContext), nameof(InterceptorContext.Parameters)))
                        .CastClass<IDynamicAccessor[]>()
                        .LoadConstant(i - startIndex)
                        .LoadElement<IDynamicAccessor>()
                        .CallVirtual(AccessTools.Method(typeof(IDynamicAccessor), nameof(IDynamicAccessor.GetValue)));
                if (elementType.IsValueType)
                {
                    dynamicMethod.UnboxAny(elementType);
                }
                else
                {
                    dynamicMethod.CastClass(elementType);
                }
                if (list[i].isRef || list[i].type.IsByRef)
                {
                    dynamicMethod.StoreIndirect(elementType);
                }
                else
                {
                    dynamicMethod.StoreArgument((ushort)i);
                }
            }
            if (!(methodInfo.ReturnType == null || methodInfo.ReturnType == typeof(void)))
            {
                var elementType = list[1].type.IsByRef ? list[1].type.GetElementType() : list[1].type;
                if (list[1].isRef || list[1].type.IsByRef)
                {
                    dynamicMethod.LoadArgument(1);
                }
                dynamicMethod.LoadLocal("ctx");
                dynamicMethod.CallVirtual(AccessTools.PropertyGetter(typeof(InterceptorContext), nameof(InterceptorContext.ReturnValue)))
                        .CallVirtual(AccessTools.Method(typeof(IDynamicAccessor), nameof(IDynamicAccessor.GetValue)));

                if (elementType.IsValueType)
                {
                    dynamicMethod.UnboxAny(elementType);
                }
                else
                {
                    dynamicMethod.CastClass(elementType);
                }
                if (list[1].isRef || list[1].type.IsByRef)
                {
                    dynamicMethod.StoreIndirect(elementType);
                }
                else
                {
                    dynamicMethod.StoreArgument(1);
                }
            }
            dynamicMethod.LoadArgument(0);
            dynamicMethod.LoadLocal("opt");
            // dynamicMethod.WriteLine("{0}", dynamicMethod.Locals["opt"]);
            dynamicMethod.StoreIndirect(typeof(Dictionary<object, object>));
            dynamicMethod.LoadLocal("ret").LoadConstant(true).Xor();
            dynamicMethod.Return();

            var delegateType = CreateDelegateType(key + "_prefix", parameterTypes, typeof(bool));
            var d = dynamicMethod.CreateDelegate(delegateType, out var ccc);
            Console.WriteLine(ccc);
            var dMethod = d.Method;
            var dm = AccessTools.Field(dMethod.GetType(), "m_owner").GetValue(dMethod) as DynamicMethod;
            for (var i = 0; i < dm.GetParameters().Length; i++)
            {
                var parameterInfo = dm.GetParameters()[i];
                AccessTools.Field(parameterInfo.GetType(), "NameImpl").SetValue(parameterInfo, list[i].name);
            }
            return dm;
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