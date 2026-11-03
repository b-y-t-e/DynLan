using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using Xunit;

namespace DynLan2.Tests
{
    /// <summary>
    /// Runs every cases/*.py file and compares the output with the .out file produced by real CPython
    /// (regenerate with: for f in cases/*.py; do python -I -X utf8 $f > ${f%.py}.out; done).
    /// Each case runs in the interpreter, with eager JIT, and with a low JIT threshold (tier-up mid-run).
    /// </summary>
    public class PythonCaseTests
    {
        static string Dir([CallerFilePath] string p = "") => Path.Combine(Path.GetDirectoryName(p), "cases");

        public static IEnumerable<object[]> Cases()
        {
            foreach (var f in Directory.GetFiles(Dir(), "*.py"))
                foreach (var mode in new[] { "interp", "jit", "jit5" })
                    yield return new object[] { Path.GetFileNameWithoutExtension(f), mode };
        }

        [Theory]
        [MemberData(nameof(Cases))]
        public void MatchesCPython(string name, string mode)
        {
            string src = File.ReadAllText(Path.Combine(Dir(), name + ".py"));
            string expected = File.ReadAllText(Path.Combine(Dir(), name + ".out")).Replace("\r\n", "\n");
            string actual = H.Out(src, mode);
            var el = expected.Split('\n');
            var al = actual.Split('\n');
            for (int i = 0; i < System.Math.Max(el.Length, al.Length); i++)
            {
                string e = i < el.Length ? el[i] : "<missing>";
                string a = i < al.Length ? al[i] : "<missing>";
                Assert.True(e == a, $"line {i + 1}\nexpected: {e}\nactual:   {a}");
            }
        }
    }
}
