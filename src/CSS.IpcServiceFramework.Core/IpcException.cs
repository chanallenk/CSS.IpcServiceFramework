using System;

namespace CSS.IpcServiceFramework
{
    public abstract class IpcException : Exception
    {
        protected IpcException()
        { }

        protected IpcException(string message)
            : base(message)
        { }

        protected IpcException(string message, Exception innerException)
            : base(message, innerException)
        { }
    }
}
