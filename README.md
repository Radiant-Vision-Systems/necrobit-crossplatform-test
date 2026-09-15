# NecroBit cross-platform reproduction

Minimal reproduction of two problems with **.NET Reactor 7.5.0.0** NecroBit protection on non-Windows platforms. One trivial library, four option combinations, three operating systems, all on stock GitHub-hosted runners.

## Symptom

A NecroBit-protected assembly restores and compiles normally, then **crashes the moment it is first executed**, inside Reactor's injected module initializer, before any application code runs. The exception is a fail-fast and cannot be caught.

```
System.AccessViolationException: Attempted to read or write protected memory.
   at System.SpanHelpers.Memmove(Byte ByRef, Byte ByRef, UIntPtr)
   at System.Runtime.InteropServices.Marshal.CopyToNative[Byte](Byte[], Int32, IntPtr, Int32)
   at <mangled>.<mangled>(System.Reflection.MethodBase, Int32, Int32, Int32)
   at <mangled>.<mangled>()
   at <Module>..cctor()
Abort trap: 6
```

## What we observe

Protecting on Windows x64, then loading under a **.NET 10** host:

| variant | options | Windows | Linux | macOS |
|---|---|---|---|---|
| v0 unprotected | control | pass | pass | pass |
| v1 | `-necrobit 1 -necrobit_comp 1 -compression 1` | pass | **crash** | **crash** |
| v2 | `-necrobit 1 -necrobit_comp 0 -compression 1` | pass | **pass** | **crash** |
| v3 | `-necrobit 1 -necrobit_comp 1 -compression 0` | pass | **crash** | **crash** |
| v4 | `-necrobit 1 -necrobit_comp 0 -compression 0` | pass | **pass** | **crash** |

Two independent effects:

1. **On Linux, `-necrobit_comp 1` is what breaks it.** With `-necrobit_comp 0` the protected assembly loads and runs correctly. `-compression` is not involved.
2. **On macOS, every NecroBit variant crashes under .NET 10**, including NecroBit with nothing else enabled.

## The macOS failure is about the runtime, not the target framework

The same protected `net8.0` assembly:

- runs correctly on a **.NET 8** host on macOS
- crashes on a **.NET 10** host on macOS

A `netstandard2.1` build behaves identically. So the assembly's target framework is not the variable — the runtime executing it is. Retargeting a library therefore does not avoid the problem, because the consuming application chooses the runtime.

## Ruled out

- **Tiered compilation.** `DOTNET_TieredCompilation=0`, `DOTNET_TieredPGO=0` and `DOTNET_ReadyToRun=0` each make no difference. (Checked because [dotnet/runtime#132627](https://github.com/dotnet/runtime/issues/132627) reports an unrelated macOS 26 / ARM64 JIT fault that *is* suppressed by that setting.)
- **Protecting on a different platform.** Running Reactor on Linux produces output that still fails off-Windows.
- **Build configuration.** The unprotected control from the identical build passes everywhere; only the protected copies fail.

## Running it

Set a repository secret `REACTOR_LICENSE` containing your .NET Reactor licence, then run the **Reproduce** workflow. It protects the library on `windows-latest` and loads every variant on `ubuntu-latest`, `macos-latest` and `windows-latest`.

The workflow fails loudly rather than reporting a false pass:

- the unprotected control must load on every platform, or the run is declared a broken harness;
- every protected variant is decompiled and its `[MethodImpl(MethodImplOptions.NoInlining)]` stubs counted, so a variant that silently failed to protect cannot masquerade as a fix;
- the probe prints the runtime from inside the process, because a `net8.0` application can silently roll forward onto a newer runtime;
- a probe that prepares no methods reports `INCONCLUSIVE` rather than passing.

## Layout

```
src/HelloReactor/   trivial library, net10.0 + net8.0 — the subject
src/Probe/          loads an assembly, forces <Module>..cctor, JIT-prepares every method
.github/workflows/  protect on Windows, load on all three platforms
```

The probe reports via **exit code**, since the fault is an uncatchable fail-fast.

## Environment observed

- .NET Reactor 7.5.0.0, protecting on Windows x64
- .NET 10.0.12 and .NET 8.0.30
- macOS 26.6.2 on ARM64 (Apple Silicon), Ubuntu 24.04 x64, Windows Server x64
- The same source protected with .NET Reactor 6.7.0.0 against earlier .NET targets did not exhibit the macOS failure
