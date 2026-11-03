using System;
using System.IO;
using DynLan2;

// dl2 script.py            run a script
// dl2 --jit=off script.py  interpreter only
// dl2 --resume state.bin   resume a saved state
static class Program
{
    static int Main(string[] args)
    {
        var options = new EngineOptions();
        string file = null, resume = null;
        for (int i = 0; i < args.Length; i++)
        {
            string a = args[i];
            if (a == "--jit=off") options.Jit = JitMode.Off;
            else if (a == "--jit=eager") { options.Jit = JitMode.Eager; options.JitThreshold = 0; }
            else if (a == "--resume" && i + 1 < args.Length) resume = args[++i];
            else if (a.StartsWith("--resume=")) resume = a.Substring(9);
            else file = a;
        }
        if (file == null && resume == null)
        {
            Console.Error.WriteLine("usage: dl2 [--jit=off|--jit=eager] script.py | dl2 --resume state.bin");
            return 64;
        }
        var engine = new ScriptEngine(options);
        try
        {
            var ctx = resume != null ? engine.Restore(File.ReadAllBytes(resume)) : engine.CreateContext(File.ReadAllText(file), Path.GetFileName(file));
            while (ctx.Run() == ExecutionStatus.Suspended)
            {
                string state = Path.ChangeExtension(file ?? resume, ".state");
                File.WriteAllBytes(state, ctx.SaveState());
                Console.Error.WriteLine($"[suspended, state saved to {state}; continue with: dl2 --resume={state}]");
                return 2;
            }
            return 0;
        }
        catch (ScriptException e)
        {
            Console.Error.WriteLine(e.ScriptTraceback);
            if (e.InnerException != null) Console.Error.WriteLine(e.InnerException);
            return 1;
        }
    }
}
