using System.Diagnostics;

namespace MemoryDemo;

static partial class Program
{
    // Program.Main can wrap each demo with this method without changing the demo itself.
    static void RunWithDiagnostics(string name, Action demo)
    {
        Console.WriteLine();
        DebugLog("INFO", $"{name}: starting");

        long allocatedBefore = GC.GetAllocatedBytesForCurrentThread();
        long managedBefore = GC.GetTotalMemory(false);
        int gen0Before = GC.CollectionCount(0);
        int gen1Before = GC.CollectionCount(1);
        int gen2Before = GC.CollectionCount(2);
        long started = Stopwatch.GetTimestamp();
        bool completed = false;

        try
        {
            demo();
            completed = true;
        }
        catch (Exception error)
        {
            DebugLog("ERROR", $"{name}: {error.GetType().Name}: {error.Message}");
            Console.WriteLine(error.StackTrace);
            throw; // Report the failure, then preserve the original exception and stack trace.
        }
        finally
        {
            // Capture measurements before formatting and printing the summary.
            TimeSpan elapsed = Stopwatch.GetElapsedTime(started);
            long allocated = GC.GetAllocatedBytesForCurrentThread() - allocatedBefore;
            long managedAfter = GC.GetTotalMemory(false);
            int gen0 = GC.CollectionCount(0) - gen0Before;
            int gen1 = GC.CollectionCount(1) - gen1Before;
            int gen2 = GC.CollectionCount(2) - gen2Before;

            DebugLog("DEBUG", $"{name}: elapsed = {elapsed.TotalMilliseconds:N2} ms (includes terminal output)");
            DebugLog("DEBUG", $"{name}: current-thread heap allocations = {allocated:N0} bytes");
            DebugLog("DEBUG", $"{name}: estimated managed heap = {managedBefore:N0} -> {managedAfter:N0} bytes");
            DebugLog("DEBUG", $"{name}: GC collections during demo: gen 0 = {gen0}, gen 1 = {gen1}, gen 2 = {gen2}");
            DebugLog(completed ? "INFO" : "ERROR", $"{name}: {(completed ? "completed" : "failed")}");
        }
    }

    static void RunDebugDemo()
    {
        Console.WriteLine("=== DEBUGGING TOOLS DEMO ===");
        Console.WriteLine();
        Console.WriteLine("--- Step 1: Terminal output vs debugger output ---");
        DebugLog("INFO", "Console.WriteLine sends these diagnostics to the terminal in Debug and Release builds.");
        DebugLog("DEBUG", $"Debugger attached: {Debugger.IsAttached}");
#if DEBUG
        DebugLog("DEBUG", "Build configuration: Debug (DEBUG symbol defined)");
#else
        DebugLog("DEBUG", "Build configuration: Release (DEBUG symbol not defined)");
#endif
        Debug.WriteLine("MemoryDemo: this message uses the debugger output channel.");
        Console.WriteLine("Debug.WriteLine uses the debugger output channel; its calls are omitted without DEBUG.");
        Console.WriteLine("Our [DEBUG] label is ordinary terminal text, so it remains visible in Release builds.");

        Console.WriteLine();
        Console.WriteLine("--- Step 2: Breakpoints and inspecting variables ---");
        int account = 0;
        int balance = GetBalance(account); // Set a breakpoint here, then step over to inspect balance.
        DebugLog("DEBUG", $"account = {account}, balance = {balance:N0}, account count = {BankAccounts.Length:N0}");
        Console.WriteLine("In the debugger, inspect account and balance in Locals or Watch; step into GetBalance.");

        Console.WriteLine();
        Console.WriteLine("--- Step 3: Exception details and the call stack ---");
        DebugLog("INFO", "Deliberately reading one past the last account to demonstrate a caught exception.");
        try
        {
            _ = GetBalance(BankAccounts.Length);
        }
        catch (IndexOutOfRangeException error)
        {
            DebugLog("WARN", $"Expected exception caught: {error.GetType().Name}: {error.Message}");
            Console.WriteLine(error.StackTrace);
            Console.WriteLine("The stack trace identifies where the error occurred; available frames depend on the build.");
        }
        DebugLog("INFO", "The expected exception was handled, so the demo can continue.");

        Console.WriteLine();
        Console.WriteLine("--- Reading the diagnostic summaries ---");
        Console.WriteLine("Elapsed time includes the demo's terminal output; it is not a controlled benchmark.");
        Console.WriteLine("Allocated bytes are cumulative allocations on the calling thread, including output formatting.");
        Console.WriteLine("Worker-thread allocations are excluded. Managed heap estimates and GC counts cover the process.");
        Console.WriteLine("Heap size is not peak memory or stack size; diagnostics do not force extra garbage collections.");
        Console.WriteLine();
    }

    static void DebugLog(string level, string message) =>
        Console.WriteLine($"[{level}] {message}");
}
