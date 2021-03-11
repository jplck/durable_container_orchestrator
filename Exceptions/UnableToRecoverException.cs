using System;
using System.Runtime.Serialization;

namespace ContainerRunnerFuncApp.Exceptions
{
    [Serializable]
    class UnableToRecoverException : Exception
    {

        public UnableToRecoverException() : base() { }

        public UnableToRecoverException(string message) : base(message) { }

        public UnableToRecoverException(string message, Exception inner) : base(message, inner) { }

        protected UnableToRecoverException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
