using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace ContainerRunnerFuncApp.Exceptions
{
    [Serializable]
    class TriggerRetryException : Exception
    {
        public static string DefaultMessage = "Orchestrator needs retry";

        public TriggerRetryException() : base(DefaultMessage) { }

        public TriggerRetryException(string message) : base(message) { }

        public TriggerRetryException(string message, Exception inner) : base(message, inner) { }

        protected TriggerRetryException(SerializationInfo info, StreamingContext context) : base(info, context) { }

    }
}
