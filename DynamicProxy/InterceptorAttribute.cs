using System;
using System.Collections.Generic;
using System.Linq;

namespace DynamicProxy
{
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
}