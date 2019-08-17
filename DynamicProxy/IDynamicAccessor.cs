using System;
using System.Collections.Generic;
using System.Linq;

namespace DynamicProxy
{
    public interface IDynamicAccessor
    {
        object GetValue();

        bool CanGet { get; }

        bool CanSet { get; }

        void SetValue(object v);
    }
}