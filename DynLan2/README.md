# DynLan 2

A fast scripting language for .NET with **Python syntax**, **resumable / serializable execution** and a **tiered JIT**.

```csharp
var engine = new ScriptEngine();
var ctx = engine.CreateContext(@"
def fib(n):
    a, b = 0, 1
    for _ in range(n):
        a, b = b, a + b
    return a

result = [fib(i) for i in range(10)]
");
ctx.Run();
ctx["result"];            // List<object> { 0, 1, 1, 2, 3, 5, 8, 13, 21, 34 }
ctx.Call("fib", 90);      // 2880067194370816120
```

DynLan 2 is a new engine; it does not share code or syntax with DynLan 1 (which stays in `DynLan/`).

## Suspend, save, move, resume

The whole call stack lives on the heap, so a running script can be stopped at any *safe point*
(loop iteration, call), serialized to bytes, sent to another process or machine and resumed there.

```csharp
var ctx = engine.CreateContext(source);
ctx.BindHost("db", dbConnection);            // host objects are saved by *name*, not by value

if (ctx.Run(stepLimit: 10_000) == ExecutionStatus.Suspended)   // or ctx.RequestSuspend() from any thread,
{                                                             // or checkpoint() inside the script
    byte[] state = ctx.SaveState();          // globals, frames, generators, closures, objects, iterators, RNG state
    File.WriteAllBytes("job.state", state);
}

// ... later, anywhere:
var resumed = new ScriptEngine().Restore(File.ReadAllBytes("job.state"),
                  new Dictionary<string, object> { ["db"] = otherConnection });
resumed.Run();                               // continues exactly where it stopped
```

* State format: compact binary (Deflate), includes the source by default (`SaveState(includeSource: false)` + `Restore(state, source)` otherwise).
* Code is referenced by deterministic keys; restoring checks the source hash.
* Host modules (`AddModule`) are passed to `Restore` as `["module:name"] = obj`; they are then importable again, also for `import` statements not yet executed.
* `SaveState` refuses (`SnapshotException`) while `Run` is executing — also when called from host code invoked by the script; call `RequestSuspend()` first.
* .NET types passed with `BindHost` must be passed again to `Restore` under the same name to keep their static members usable.
* Suspension inside a .NET callback (e.g. a script function called by `sorted(key=...)` or by host code) is deferred until control returns to script code.
* Objects that cannot be saved (arbitrary .NET objects not registered with `BindHost`) make `SaveState` throw `SnapshotException`.

## Python compatibility

Test-suite cases in `DynLan2.Tests/cases/*.py` are executed by DynLan 2 (interpreter and JIT) and compared
line-by-line with the output of real CPython.

Supported: indentation blocks; `def` with defaults, `*args`, keyword-only args, `**kwargs`, annotations; `lambda`;
closures, `global`, `nonlocal`; classes with multiple inheritance (C3 MRO), `super()`, `@property` (+ setter),
`@staticmethod`, `@classmethod`, decorators, operator overloading (`__add__`, `__eq__`, `__lt__`, `__getitem__`,
`__iter__`/`__next__`, `__len__`, `__hash__`, `__call__`, `__str__`/`__repr__`, ...); `if/elif/else`,
`while`/`for` with `else`, `break`/`continue`; `try/except/else/finally`, `raise ... from`, exception hierarchy;
`with`; generators (`yield`, `yield from`, `send`); list/dict/set comprehensions and generator expressions;
f-strings (format spec, `!r`, nested fields, `=`), `%` formatting, `str.format`; walrus `:=`; chained comparisons;
slicing (incl. assignment/deletion); starred unpacking; arbitrary-precision ints; `int`/`float`/`str`/`list`/`dict`/
`set`/`tuple`/`range` with their common methods; most built-ins (`len`, `sorted`, `zip`, `map`, `enumerate`, `min`,
`max`, `sum`, `round`, `isinstance`, ...); modules `math`, `time`, `random` (seedable, state is saved in snapshots).

Not supported: `async`/`await`, `match`, metaclasses, subclassing built-in types, `bytes`, complex numbers,
`__slots__`, custom descriptors (other than `property`), file I/O and the wider standard library
(scripts are sandboxed — they only see what the host gives them).

Deliberate difference: variables annotated `x: int` / `x: float` are **enforced** (a wrong type raises `TypeError`;
`int` is 64-bit and overflow raises `OverflowError`). This lets the JIT keep them unboxed. Unannotated ints behave
exactly like Python (arbitrary precision).

## .NET interop

```csharp
ctx["customer"] = customer;               // properties, fields, methods (overloads), indexers, IEnumerable
ctx["Math"] = typeof(Math);               // static members; calling a Type constructs an instance
ctx.AddModule("svc", service);            // `import svc` inside the script
ctx.Eval("customer.Orders[0].Total * 2");
ctx.Exec("customer.Tags.Add('vip')");
```

Script callables passed to .NET parameters of delegate types (`Func<int,int>`, ...) are converted automatically.
`IDictionary<string, object>` / `ExpandoObject` members are accessible as attributes.

### Sandbox

Scripts see only what the host gives them:

* No file, process, network or reflection access; `open`, `input`, `exec`/`eval` are not available inside scripts.
* Reflection is hidden: `GetType()`, members returning `Type`/`MemberInfo`/`Assembly`/`Module`, `Delegate.Method`
  and anything from `System.Reflection` raise `AttributeError`.
* Static members and constructors work only for types the host passed in (`ctx[...]`, `BindHost`, `AddModule`;
  nested types of those are allowed too). A `Type` that reaches the script any other way — e.g. returned by a host
  method — is inert (attribute access → `AttributeError`, calling → `TypeError`).

## Performance

`DynLan2.Bench` (Release, .NET 8, warm = code already compiled):

| benchmark | C# | DL2 interpreter | DL2 JIT (warm) | CPython 3 |
|---|---:|---:|---:|---:|
| fib(30) recursive | 2.6 ms | 234 ms | 95 ms | 87 ms |
| loop sum 20M | 3.9 ms | 268 ms | 23 ms | 490 ms |
| typed loop 20M (`x: int`) | 16 ms | 1125 ms | 37 ms | 1001 ms |
| float math 5M | 4.8 ms | 366 ms | 6.7 ms | 293 ms |
| matmul 120 (nested lists) | 3.0 ms | 90 ms | 27 ms | 73 ms |
| objects + methods 2M | 0.4 ms | 265 ms | 156 ms | 132 ms |
| dict + strings 300k | 8.4 ms | 86 ms | 32 ms | 40 ms |
| DynLan 1 vs 2 (while loop 200k) | | 1339 ms → 5.9 ms | 3.5 ms | |

Loading: ~0.6–2.5 M lines/s (lexer ~40 M tokens/s). A 200 000-line script with 20 000 functions loads in ~0.34 s;
function bodies are analyzed and compiled to bytecode only when first called. Parsed scripts are cached per engine.

## Requirements

| | |
|---|---|
| Runtime | .NET 8+ (`net8.0` build) or any runtime implementing .NET Standard 2.1: .NET Core 3.0+, Mono 6.4+, Xamarin, Unity 2021.2+ |
| Build | .NET 8 SDK |
| Dependencies | none (BCL only) |
| JIT | needs runtime code generation. On AOT-only platforms (NativeAOT, iOS, Unity IL2CPP, consoles) use `JitMode.Off` — interpreter, snapshots and interop work unchanged |
| Snapshots | restoring requires the same `DynLan2` compiler version and the same script source (included in the state by default) |

The `netstandard2.1` build replaces a few .NET 5+ APIs (`Math.BigMul`, `BigInteger.GetBitLength`, `Math.Log2`,
`Math.CopySign`, `ReferenceEqualityComparer`) with hand-written equivalents in `Compat.cs`. Running on .NET 8, both
builds benchmark the same (differences are within measurement noise). One numeric difference remains: on `netstandard2.1`
`math.log2` is exact for powers of two but differs from the platform `log2` by 1 ulp for ~0.3% of other inputs. Performance on another runtime depends on
that runtime: Mono's JIT generates slower code than RyuJIT, and AOT-only platforms run the interpreter only.

## Architecture

```
source ─► Lexer (INDENT/DEDENT, interned names) ─► Parser (AST) ─► ScopeAnalyzer (locals/cells/free/globals, lazy per function)
       ─► CodeGen (register bytecode, lazy per function) ─► VM interpreter (heap frames, safe points)
                                                         └► JIT: flow-sensitive type inference → expression trees → IL
```

* **Value** — 16-byte struct; ints, floats, bools and `None` are never boxed; big ints switch to `BigInteger` on overflow.
* **VM** — register machine; frames are heap objects (no CLR recursion for script→script calls), so the stack can be
  serialized at any safe point; deep recursion is limited only by `EngineOptions.RecursionLimit`.
* **JIT** — hot functions and hot loops (on-stack replacement) are compiled to IL. Each register gets a representation
  per program point (boxed / `long` / `double`); integer overflow deoptimizes back to the interpreter, which finishes the
  instruction with arbitrary-precision ints. At every safe point compiled code can spill its registers into the heap
  frame and return — which is why suspension and snapshots keep working with the JIT enabled.
  Modes: `JitMode.Background` (default), `JitMode.Eager`, `JitMode.Off`.

## Projects

| | |
|---|---|
| `DynLan2` | the engine (`net8.0`, `netstandard2.1`) |
| `DynLan2.Tests` | xUnit tests: CPython conformance, snapshots, JIT, API |
| `DynLan2.Bench` | benchmarks; `save <file>` / `resume <file>` demo moving a running job between processes |
| `DynLan2.Cli` | `dl2 script.py` runner (`--jit=off`, `--resume=state`) |
