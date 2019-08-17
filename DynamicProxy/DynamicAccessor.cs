using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using FastExpressionCompiler;

namespace DynamicProxy
{
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
}