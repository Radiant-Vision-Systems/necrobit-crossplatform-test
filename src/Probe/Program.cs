using System;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

// Loads one assembly, forces its module constructor, then JIT-prepares every
// method. NecroBit decrypts method bodies at JIT time, so this is the minimum
// that exercises the decryption path.
//
// The AccessViolationException this produces is a fail-fast and CANNOT be
// caught, so the caller must read the EXIT CODE, never stdout.
internal static class Program
{
    private static int Main(string[] args)
    {
        if (args.Length < 1)
        {
            Console.Error.WriteLine("usage: probe <assembly.dll>");
            return 2;
        }

        Console.WriteLine($"[probe] os      = {RuntimeInformation.OSDescription}");
        Console.WriteLine($"[probe] arch    = {RuntimeInformation.ProcessArchitecture}");
        // Reported from INSIDE the process: a net8 app can silently roll forward
        // onto a newer runtime, which would otherwise invalidate the result.
        Console.WriteLine($"[probe] runtime = {RuntimeInformation.FrameworkDescription}");
        Console.WriteLine($"[probe] loading   {args[0]}");

        var asm = Assembly.LoadFrom(args[0]);
        Console.WriteLine($"[probe] loaded:   {asm.FullName}");

        foreach (var mod in asm.GetModules())
        {
            Console.WriteLine($"[probe] RunModuleConstructor({mod.Name})");
            RuntimeHelpers.RunModuleConstructor(mod.ModuleHandle);
        }
        Console.WriteLine("[probe] module ctor OK");

        Type[] types;
        try { types = asm.GetTypes(); }
        catch (ReflectionTypeLoadException ex) { types = Array.FindAll(ex.Types, t => t != null); }

        int prepared = 0, skipped = 0;
        foreach (var t in types)
        {
            if (t.IsGenericTypeDefinition) { skipped++; continue; }
            MethodInfo[] methods;
            try
            {
                methods = t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic
                                       | BindingFlags.Static | BindingFlags.Instance
                                       | BindingFlags.DeclaredOnly);
            }
            catch { skipped++; continue; }

            foreach (var m in methods)
            {
                if (m.IsAbstract || m.ContainsGenericParameters) { skipped++; continue; }
                try { RuntimeHelpers.PrepareMethod(m.MethodHandle); prepared++; } catch { skipped++; }
            }
        }

        Console.WriteLine($"[probe] types={types.Length} prepared={prepared} skipped={skipped}");

        // A run that prepared nothing proves nothing — report it rather than pass.
        if (prepared == 0)
        {
            Console.WriteLine("[probe] INCONCLUSIVE (no methods were prepared)");
            return 3;
        }

        Console.WriteLine("[probe] PASS");
        return 0;
    }
}
