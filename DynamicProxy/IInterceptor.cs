using System;
using System.Collections.Generic;
using System.Linq;

namespace DynamicProxy
{
    public interface IInterceptor
    {
        InterceptControl BeforeProcess(InterceptorContext ctx);

        InterceptControl AfterProcess(InterceptorContext ctx);
    }

    public enum InterceptControl
    {
        None,
        
        SkipIntercept,
        
        SkipOriginalMethod,
        
        All,
    }
}