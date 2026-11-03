# CLAUDE.md

Two independent scripting engines for .NET live in this repo. They share no code.

| | DynLan 1 (`DynLan/`, `DynLan*` variants) | DynLan 2 (`DynLan2/`) |
|---|---|---|
| Syntax | own (braces, `this`) | Python |
| Targets | .NET 2.0 … netstandard2.0, CE, PCL | net8.0 + netstandard2.1 |
| Status | maintained as-is — do not change behaviour | active development |

The `DynLanNet20/35/CE/PCL/NetCore/NetStandard` projects compile the same `DynLan/` sources for old frameworks; any change to DynLan 1 must still build for all of them.

## Commands

```
dotnet test DynLan2.Tests/DynLan2.Tests.csproj                     # DynLan 2 tests (xUnit)
dotnet run -c Release --project DynLan2.Bench                      # benchmarks (C#, interpreter, JIT, CPython, DynLan 1, load time)
dotnet run -c Release --project DynLan2.Bench -- --quick           # skip interpreter column
dotnet run -c Release --project DynLan2.Bench -- parsebench        # parser throughput per construct
dotnet run -c Release --project DynLan2.Bench -- save x.state      # suspend a job and save it ...
dotnet run -c Release --project DynLan2.Bench -- resume x.state    # ... and finish it in another process
dotnet run --project DynLan2.Cli -- script.py [--jit=off|--jit=eager]
dotnet test DynLan2.Tests -p:EngineTarget=netstandard2.1           # same suite against the netstandard2.1 engine build
dotnet run -c Release --project DynLan2.Bench -p:EngineTarget=netstandard2.1   # benchmark that build
dotnet test DynLanTests/DynLanTests.csproj                         # DynLan 1 tests
```

Releases: the `DynLan` NuGet package ships DynLan 2 from 2.0.0 on (`DynLan2/DynLan2.csproj` has `PackageId=DynLan`; DynLan 1 stays on 1.2.x). `python Deploy/publish.py` (tests both builds, bumps the patch, packs, pushes; `--dry-run`, `--no-bump`, `--set-version x.y.z`, `--engine 1` for a 1.x maintenance release). Needs `Deploy/.env` with `NUGET_API_KEY`.

Python conformance: `DynLan2.Tests/cases/*.py` run in three modes (interpreter, eager JIT, JIT threshold 5) and are compared line-by-line with the `.out` files produced by real CPython. After adding or editing a case regenerate them (bash):

```
cd DynLan2.Tests/cases && for f in *.py; do python -I -X utf8 "$f" > "${f%.py}.out"; done
```

`-X utf8` is required on Windows (`PYTHONIOENCODING` is ignored with `-I`). Never hand-edit `.out` files.

## DynLan 2 architecture (`DynLan2/`)

Pipeline: `Lexer` → `Parser` (AST in `Ast.cs`) → `ScopeAnalyzer` (`Scope.cs`) → `CodeGen` (register bytecode, `Bytecode.cs`) → `VM` interpreter → `Jit`/`JitCompiler` (expression trees → IL).

- `Value` — 16-byte struct (`O` = type tag or object, `N` = payload). `default(Value)` means *unbound*. Ints overflow into `BigInteger`.
- Laziness: only module-level code is analyzed at load. Functions whose enclosing scopes are only module/classes are scope stubs (`Scope.Lazy`) analyzed in `FunctionCode.EnsureCompiled`. Bytecode is generated on first call. Scope facts (`IsGenerator`, `FreeNames`, `ParamNames`) are copied into `FunctionCode` only in `EnsureCompiled` — never read them from a code that is not compiled.
- Code keys: `FunctionCode.Key = (rootIndex << 20) | localIndex`. They are deterministic regardless of compile order, and snapshots depend on that. `Script.GetCode(key)` analyzes the root on demand.
- `VM` frames are heap objects; script→script calls do not recurse on the CLR stack. `Run(baseDepth)` exits when the stack drops to `baseDepth`. Nested runs (builtins calling scripts, `ctx.Call`, generators stepped from .NET) increment `NoSuspend` — suspension is deferred there.
- Safe points: loop back-edges and after calls. Suspension = `Interrupt` flag / `Fuel` (step limit) / `checkpoint()`.
- Exceptions: `ExecSimple` handles all non-control instructions and is shared by the interpreter and the JIT fallback. `HandleException` walks frames using `Frame.PcExact` (exact pc vs. resume pc).
- Frames of plain calls are pooled (`VM.Recycle`); never keep a reference to a popped `Normal` frame.

### JIT rules (must not change behaviour)

- Eligible: no try/except, no yield, no class definition, not a class body or generator.
- Flow-sensitive types: each register is boxed / `long` / `double` per program point. Unboxed int arithmetic that overflows **deoptimizes** (status 2) so the interpreter redoes the instruction with big ints. Annotated `x: int` registers use checked arithmetic (`OverflowError`).
- Return statuses of compiled code: 0 returned (`vm.Result`), 1 suspended (state spilled into `f.R`, `f.Pc` = resume pc), 2 deopt (continue interpreting at `f.Pc`). Calls from compiled code use `JitOps.Call*` → `VM.JitInvoke`, which also returns 3 = completed, but suspend requested.
- Entry points are pc 0 and every backward-jump target (OSR). The entry prologue guards unboxed registers (`IsIntOrUnbound`) and returns 2 before touching anything.
- `JitMode.Off` is per engine: never mark a shared `FunctionCode` as non-compilable because of it (`VM._jitOn` gates the triggers).
- Any change to JIT or VM: run the whole test suite — every Python case runs under all three modes, and snapshot tests suspend at every safe point with JIT on.

### Snapshots (`Snapshot.cs`)

- Two-phase object table (shells, then bodies). Dicts/sets are filled last because hashes may depend on restored objects. Class MROs are recomputed after loading.
- Host objects are saved only by name (`BindHost`, `AddModule` → `"module:name"`), plus value types listed in `HostValues`. Anything else → `SnapshotException`.
- Bump `Snapshot.CompilerVersion` whenever bytecode layout, register allocation or code keys change.
- Saving is refused while `Run` executes (`ScriptContext.IsRunning`).

### Sandbox (`ReflectionGuard.cs`, `Interop.cs`)

- Reflection members/types are filtered in `Interop.Lookup`.
- Static access and construction only for types in `ScriptContext.AllowedTypes` (filled by `SetGlobal`/`BindHost`/`AddModule`/`Restore` bindings; nested types of allowed types are added on access).
- Do not add built-ins that touch files, processes, environment or reflection.

## Conventions

- Python semantics first. A Python feature that is not supported must fail with a clear error, not a silent wrong result. Document deliberate differences in `DynLan2/README.md` (currently only the enforced `x: int` / `x: float` annotations).
- New built-ins: functions go in `Builtins.InitFunctions`, methods of built-in types in `BuiltinMethods.cs`. Functions are registered by a unique key (`Builtins.Fn`) so snapshots can find them.
- Keep the engine free of external NuGet dependencies.
- The engine must build for `netstandard2.1`. APIs newer than that go through `Compat.cs` (`#if NET5_0_OR_GREATER` → BCL, else a hand-written equivalent covered by `CompatTests`). Run the test suite with `-p:EngineTarget=netstandard2.1` after touching them.
- Shell editing pitfall on this machine: bash heredocs mangle `\\` — write helper scripts with the editor tools instead of inline heredocs.
