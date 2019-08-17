using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace DynamicProxy
{
    public interface IInterceptorContext
    {
        MethodInfo Method { get; }

        object Target { get; }

        IReadOnlyList<IDynamicAccessor> Parameters { get; }

        IDynamicAccessor ReturnValue { get; }

        IDictionary<object, object> Context { get; set; }
    }
}