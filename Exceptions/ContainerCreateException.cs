using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace ContainerRunnerFuncApp.Exceptions
{
    [Serializable]
    class ContainerCreateException : Exception
    {

        public ContainerCreateException() : base() { }

        public ContainerCreateException(string message) : base(message) { }

        public ContainerCreateException(string message, Exception inner) : base(message, inner) { }

        protected ContainerCreateException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
