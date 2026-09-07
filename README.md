# DynLan

A tiny scripting language for .NET — compile a string, run it, get a value back.

```csharp
object result = new Compiler().Compile(@"
    class Cart() {
      total = 0
      def Add(price) {
        this.total = this.total + price
        return this.total
      }
    }
    cart = Cart()
    cart.Add(10)
    return cart.Add(5)
").Eval();
// result = 15
```

No `DLR`, no `Reflection.Emit`, no code generation — just plain C#. That's also why it runs on frameworks as old as .NET 2.0.

## Why

- **Sandboxed logic at runtime** — let users (or config files, or a DB column) supply business rules without recompiling
- **Full two-way interop** — pass in your own objects / `Dictionary` / `ExpandoObject`, call their properties, methods, indexers straight from script
- **Real language, not a DSL toy** — variables, `def` functions, `class`, closures, `if`/`elif`/`else`, `while`, `try`/`catch`
- **Steppable** — pause and resume execution mid-script (`ContextEvaluator.ExecuteNext`)

## Try it

```csharp
new Compiler().Compile(" round(33.3333, 2) + 'ABC' ").Eval();
// "33.33ABC"
```

```csharp
var variables = new Dictionary<string, object> { ["A"] = "Test string" };
new Compiler().Compile(" A.Length + ' ' + A.Substring(5,6) ").Eval(variables);
// "11 string"
```

```csharp
new Compiler().Compile(@"
    def increment(a) { return a + 1 }
    return increment(10)
").Eval();
// 11
```

<details>
<summary><b>More: control flow, error handling, code that writes code</b></summary>

Full `if`/`elif`/`else` and `while`:
```csharp
new Compiler().Compile(@"
    i = 0
    while i < 100 { i = i + 1 }
    if i == 100 { return 'done' } else { return 'nope' }
").Eval();
// "done"
```

`try`/`catch`, just like C#:
```csharp
new Compiler().Compile(@"
    error = null
    try { throw 'Error!' }
    catch (ex) { error = ex }
    return error.Message
").Eval();
// "Error!"
```

Call a script function straight from C#, no re-compiling:
```csharp
DynLanContext ctx = new Compiler().Compile(@"
    def greet(name) { return 'Hi, ' + name }
").CreateContext();

ctx.InvokeMethod("greet", new object[] { "world" });
// "Hi, world"
```

The party trick — a script can `eval()` code it builds itself, at runtime:
```csharp
new Compiler().Compile(@"
    A = 3
    generated = 'return 556 + A'
    return eval(generated)
").Eval();
// 559
```

More in [EXAMPLES.md](EXAMPLES.md).
</details>

## Compatibility

| Project | Target |
|---|---|
| `DynLan` | .NET Framework 4.6.1, .NET Standard 2.0 (→ any modern .NET) |
| `DynLanNet35` / `DynLanNet20` | .NET Framework 3.5 / 2.0 |
| `DynLanPCL` | PCL, incl. Silverlight |
| `DynLanNetCore` / `DynLanNetStandard` | .NET Core / .NET Standard 2.0 |
| `DynLanCE` | .NET Compact Framework |

## Build & test

```
dotnet build DynLan/DynLan.csproj
dotnet test DynLanTests/DynLanTests.csproj
```

## Performance

~22,800 lines/second, single core (i5-3210M @ 2.50GHz).

## License

[LICENSE](LICENSE)
