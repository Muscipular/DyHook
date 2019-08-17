using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace DynamicProxy
{
    public interface IInterceptor
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        InterceptControl BeforeProcess(InterceptorContext ctx);

        [MethodImpl(MethodImplOptions.NoInlining)]
        InterceptControl AfterProcess(InterceptorContext ctx);
    }

    public enum InterceptControl
    {
        None,
        
        SkipIntercept,
        
        SkipOriginalMethod,
        
        SkipAll,
    }
}