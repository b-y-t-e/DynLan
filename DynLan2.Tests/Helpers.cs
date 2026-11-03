using System.IO;
using DynLan2;

namespace DynLan2.Tests
{
    public static class H
    {
        public static ScriptEngine Engine(string mode)
        {
            var o = new EngineOptions { CacheScripts = false };
            switch (mode)
            {
                case "interp": o.Jit = JitMode.Off; break;
                case "jit": o.Jit = JitMode.Eager; o.JitThreshold = 0; break;
                case "jit5": o.Jit = JitMode.Eager; o.JitThreshold = 5; break;
            }
            return new ScriptEngine(o);
        }

        /// <summary>Runs a script and returns everything it printed.</summary>
        public static string Out(string src, string mode = "interp")
        {
            var sw = new StringWriter { NewLine = "\n" };
            var e = Engine(mode);
            e.Options.Output = sw;
            var ctx = e.CreateContext(Dedent(src));
            while (ctx.Run() == ExecutionStatus.Suspended) { }
            return sw.ToString();
        }

        public static string Dedent(string s)
        {
            var lines = s.Replace("\r\n", "\n").Split('\n');
            int min = int.MaxValue;
            foreach (var l in lines)
            {
                if (l.Trim().Length == 0) continue;
                int i = 0;
                while (i < l.Length && l[i] == ' ') i++;
                if (i < min) min = i;
            }
            if (min == int.MaxValue) return s;
            for (int k = 0; k < lines.Length; k++) lines[k] = lines[k].Length >= min ? lines[k].Substring(min) : lines[k].TrimStart();
            return string.Join("\n", lines);
        }
    }
}
