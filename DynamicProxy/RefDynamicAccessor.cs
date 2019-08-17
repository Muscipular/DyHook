using System;
using System.Collections.Generic;
using System.Linq;

namespace DynamicProxy
{
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
}