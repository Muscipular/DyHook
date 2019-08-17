using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using FastExpressionCompiler;

namespace DynamicProxy
{
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
}