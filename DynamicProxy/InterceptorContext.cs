using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DynamicProxy
{
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
}