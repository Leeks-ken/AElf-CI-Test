using System;
using System.Runtime.Serialization;

namespace AElf.OS.Network.Application;

public enum NetworkExceptionType
{
    Rpc,
    PeerUnstable,
    Unrecoverable,
    FullBuffer,
    NotConnected,
    HandlerException
}

[Serializable]
public class NetworkException : Exception
{
    public NetworkException()
    {
    }

    public NetworkException(string message, NetworkExceptionType exceptionType) : base(message)
    {
        ExceptionType = exceptionType;
    }

    public NetworkException(string message, Exception inner,
        NetworkExceptionType exceptionType = NetworkExceptionType.Rpc) : base(message, inner)
    {
        ExceptionType = exceptionType;
    }

    protected NetworkException(SerializationInfo info, StreamingContext context)
        : base(info, context)
    {
    }

    public NetworkExceptionType ExceptionType { get; }
}