using System;
using System.Collections.Generic;
using System.Linq;

namespace DynamicProxy
{
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