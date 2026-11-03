using System;
using System.Collections.Generic;
using System.Text;

namespace DynLan2
{
    /// <summary>A script-level exception (Python exception instance) surfacing in .NET.</summary>
    public class ScriptException : Exception
    {
        internal Value PyValue;
        internal readonly List<(string Func, int Line)> Trace = new List<(string, int)>();
        internal string FileName;

        internal ScriptException(Value pyValue) : base(null) { PyValue = pyValue; }
        internal ScriptException(Value pyValue, Exception inner) : base(null, inner) { PyValue = pyValue; }

        internal ScriptException(PyClass cls, string message)
            : this(Value.Obj(Builtins.NewException(cls, message))) { }

        /// <summary>Python exception type name, e.g. "ValueError".</summary>
        public string PythonType => PyValue.O is PyInstance i ? i.Class.Name : "Exception";

        /// <summary>Exception message (str(e) in the script).</summary>
        public string PythonMessage
        {
            get
            {
                var prev = VM.Current;
                if (VM.Current == null) VM.Current = Vm ?? VM.Fallback;
                try { return Ops.Str(PyValue); }
                catch { return ""; }
                finally { VM.Current = prev; }
            }
        }

        internal VM Vm;

        public override string Message
        {
            get
            {
                string m = PythonMessage;
                return string.IsNullOrEmpty(m) ? PythonType : PythonType + ": " + m;
            }
        }

        /// <summary>Python-style traceback (innermost call last).</summary>
        public string ScriptTraceback
        {
            get
            {
                var sb = new StringBuilder("Traceback (most recent call last):\n");
                for (int i = Trace.Count - 1; i >= 0; i--)
                    sb.Append("  File \"").Append(FileName ?? "<script>").Append("\", line ").Append(Trace[i].Line)
                      .Append(", in ").Append(Trace[i].Func).Append('\n');
                sb.Append(Message);
                return sb.ToString();
            }
        }

        /// <summary>Line of the innermost frame where the exception was raised (0 if unknown).</summary>
        public int Line => Trace.Count > 0 ? Trace[0].Line : 0;

        internal bool IsInstanceOf(PyClass cls) => PyValue.O is PyInstance i && i.Class.IsSubclassOf(cls);

        public override string ToString() => ScriptTraceback;
    }

    public sealed class ScriptSyntaxException : ScriptException
    {
        public string File { get; }
        public int Column { get; }
        public new int Line { get; }
        internal string RawMessage { get; }

        internal ScriptSyntaxException(string msg, string file, int line, int col)
            : base(Builtins.SyntaxError, msg + " (" + (file ?? "<script>") + ", line " + line + ")")
        {
            RawMessage = msg;
            File = file;
            Line = line;
            Column = col;
        }
    }

    /// <summary>Thrown when execution state cannot be saved or restored.</summary>
    public sealed class SnapshotException : Exception
    {
        public SnapshotException(string msg) : base(msg) { }
    }

    internal static class Err
    {
        public static ScriptException Type(string m) => new ScriptException(Builtins.TypeError, m);
        public static ScriptException Value(string m) => new ScriptException(Builtins.ValueError, m);
        public static ScriptException Index(string m) => new ScriptException(Builtins.IndexError, m);
        public static ScriptException Key(global::DynLan2.Value k) => new ScriptException(global::DynLan2.Value.Obj(Builtins.NewExceptionArgs(Builtins.KeyError, k)));
        public static ScriptException Name(string n) => new ScriptException(Builtins.NameError, "name '" + n + "' is not defined");
        public static ScriptException Unbound(string n) =>
            new ScriptException(Builtins.UnboundLocalError, "cannot access local variable '" + n + "' where it is not associated with a value");
        public static ScriptException Attr(global::DynLan2.Value o, string n) =>
            new ScriptException(Builtins.AttributeError, "'" + Ops.TypeName(o) + "' object has no attribute '" + n + "'");
        public static ScriptException ZeroDiv(string m = "division by zero") => new ScriptException(Builtins.ZeroDivisionError, m);
        public static ScriptException Runtime(string m) => new ScriptException(Builtins.RuntimeError, m);
        public static ScriptException Overflow(string m) => new ScriptException(Builtins.OverflowError, m);
        public static ScriptException Recursion() => new ScriptException(Builtins.RecursionError, "maximum recursion depth exceeded");
        public static ScriptException Import(string m) => new ScriptException(Builtins.ImportError, m);
        public static ScriptException StopIter(global::DynLan2.Value v) => new ScriptException(global::DynLan2.Value.Obj(Builtins.NewExceptionArgs(Builtins.StopIteration, v)));
        public static ScriptException NotImpl(string m) => new ScriptException(Builtins.NotImplementedError, m);

        public static ScriptException Unsupported(string op, global::DynLan2.Value a, global::DynLan2.Value b) =>
            Type("unsupported operand type(s) for " + op + ": '" + Ops.TypeName(a) + "' and '" + Ops.TypeName(b) + "'");
    }
}
