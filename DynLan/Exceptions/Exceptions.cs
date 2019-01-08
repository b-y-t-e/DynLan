
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DynLan.Exceptions
{
    public interface IDynLanException
    {
        Exception Create(String Message, String DynLanStacktrace);

        String DynLanStacktrace { get; set; }
    }

#if !PCL
    [Serializable]
#endif
    public class DynLanAbortException : Exception, IDynLanException
    {
        public String DynLanStacktrace { get; set; }

        public DynLanAbortException() : this("Abort execution") { }
        public DynLanAbortException(string message) : base(message) { }
        public DynLanAbortException(string message, Exception inner) : base(message, inner) { }
#if !PCL
        protected DynLanAbortException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
#endif

        public Exception Create(string Message, String DynLanStacktrace)
        {
            return new DynLanAbortException(Message, this) { DynLanStacktrace = DynLanStacktrace };
        }
    }

#if !PCL
    [Serializable]
#endif
    public class DynLanMethodNotFoundException : Exception, IDynLanException
    {
        public String DynLanStacktrace { get; set; }

        public DynLanMethodNotFoundException() { }
        public DynLanMethodNotFoundException(string message) : base(message) { }
        public DynLanMethodNotFoundException(string message, Exception inner) : base(message, inner) { }
#if !PCL
        protected DynLanMethodNotFoundException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
#endif
        public Exception Create(string Message, String DynLanStacktrace)
        {
            return new DynLanMethodNotFoundException(Message, this) { DynLanStacktrace = DynLanStacktrace };
        }
    }

#if !PCL
    [Serializable]
#endif
    public class DynLanUnsupportedMethodTypeException : Exception, IDynLanException
    {
        public String DynLanStacktrace { get; set; }

        public DynLanUnsupportedMethodTypeException() { }
        public DynLanUnsupportedMethodTypeException(string message) : base(message) { }
        public DynLanUnsupportedMethodTypeException(string message, Exception inner) : base(message, inner) { }
#if !PCL
        protected DynLanUnsupportedMethodTypeException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
#endif
        public Exception Create(string Message, String DynLanStacktrace)
        {
            return new DynLanUnsupportedMethodTypeException(Message, this) { DynLanStacktrace = DynLanStacktrace };
        }
    }

#if !PCL
    [Serializable]
#endif
    public class DynLanInvalidOperationException : Exception, IDynLanException
    {
        public String DynLanStacktrace { get; set; }

        public DynLanInvalidOperationException() : this("Invalid operation type") { }
        public DynLanInvalidOperationException(string message) : base(message) { }
        public DynLanInvalidOperationException(string message, Exception inner) : base(message, inner) { }
#if !PCL
        protected DynLanInvalidOperationException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
#endif
        public Exception Create(string Message, String DynLanStacktrace)
        {
            return new DynLanInvalidOperationException(Message, this) { DynLanStacktrace = DynLanStacktrace };
        }
    }

#if !PCL
    [Serializable]
#endif
    public class DynLanInvalidExpressionException : Exception, IDynLanException
    {
        public String DynLanStacktrace { get; set; }

        public DynLanInvalidExpressionException() : this("Invalid expression") { }
        public DynLanInvalidExpressionException(string message) : base(message) { }
        public DynLanInvalidExpressionException(string message, Exception inner) : base(message, inner) { }
#if !PCL
        protected DynLanInvalidExpressionException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
#endif
        public Exception Create(string Message, String DynLanStacktrace)
        {
            return new DynLanInvalidExpressionException(Message, this) { DynLanStacktrace = DynLanStacktrace };
        }
    }

#if !PCL
    [Serializable]
#endif
    public class DynLanIncorrectExpressionFormatException : Exception, IDynLanException
    {
        public String DynLanStacktrace { get; set; }

        public DynLanIncorrectExpressionFormatException() : this("Incorrect expression format") { }
        public DynLanIncorrectExpressionFormatException(string message) : base(message) { }
        public DynLanIncorrectExpressionFormatException(string message, Exception inner) : base(message, inner) { }
#if !PCL
        protected DynLanIncorrectExpressionFormatException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
#endif
        public Exception Create(string Message, String DynLanStacktrace)
        {
            return new DynLanIncorrectExpressionFormatException(Message, this) { DynLanStacktrace = DynLanStacktrace };
        }
    }

#if !PCL
    [Serializable]
#endif
    public class DynLanCompileException : Exception, IDynLanException
    {
        public String DynLanStacktrace { get; set; }

        public DynLanCompileException() : this("Compile exception") { }
        public DynLanCompileException(string message) : base(message) { }
        public DynLanCompileException(string message, Exception inner) : base(message, inner) { }
#if !PCL
        protected DynLanCompileException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
#endif
        public Exception Create(string Message, String DynLanStacktrace)
        {
            return new DynLanCompileException(Message, this) { DynLanStacktrace = DynLanStacktrace };
        }
    }

#if !PCL
    [Serializable]
#endif
    public class DynLanExecuteException : Exception, IDynLanException
    {
        public String DynLanStacktrace { get; set; }

        public DynLanExecuteException() : this("Execute exception") { }
        public DynLanExecuteException(string message) : base(message) { }
        public DynLanExecuteException(string message, Exception inner) : base(message, inner) { }
#if !PCL
        protected DynLanExecuteException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context)
            : base(info, context) { }
#endif
        public Exception Create(string Message, String DynLanStacktrace)
        {
            return new DynLanExecuteException(Message, this) { DynLanStacktrace = DynLanStacktrace };
        }
    }
}
