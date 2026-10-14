using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace MemoryDemo;

// STACK DEMO
// Every active method call gets a frame on the call stack holding its parameters, locals, and
// return address. Frames are pushed on call and popped on return (last in, first out).
// Our code decides the call order, the C# compiler and .NET JIT decide each frame's layout,
// and the OS reserves each thread's stack memory (1 MB for the main thread on Windows).
// Helper names start with "Stack" so they don't clash with the other demos in Program.
static partial class Program
{
    static int StackDepth; // number of traced calls currently active, only used to indent output

    static void RunStackDemo()
    {
        Console.WriteLine("=== STACK DEMO ===");

        // 1. Each call pushes a frame; each return pops it and resumes the caller where it left off.
        StackSection("1. Nested method calls");
        string badge = MakeBadge(2);
        Console.WriteLine($"  Back in RunStackDemo: badge = {badge}");

        // 2. Every Factorial call gets its own frame with its own copy of n.
        StackSection("2. Bounded recursion");
        Factorial(4);

        // 3. A struct local keeps its data in the frame. A class local keeps only a reference;
        //    the object itself is on the heap. (.NET 10 can stack-allocate objects that never leave
        //    a method, but this one is passed to another method, so it goes on the heap.)
        StackSection("3. Locals: values vs references");
        long before = GC.GetAllocatedBytesForCurrentThread();
        StackPoint point = new(1, 2);
        long afterStruct = GC.GetAllocatedBytesForCurrentThread();
        StackOrder order = new("A-100", 3);
        long afterClass = GC.GetAllocatedBytesForCurrentThread();
        Console.WriteLine($"  struct local: {afterStruct - before} heap bytes, class local: {afterClass - afterStruct} heap bytes");

        int count = 5;
        ChangeArguments(point, order, ref count);
        Console.WriteLine($"  Caller after the call: point = {point}  (struct was copied, so unchanged)");
        Console.WriteLine($"                         order = {order}  (reference was copied, same heap object, so changed)");
        Console.WriteLine($"                         count = {count}  (passed by ref, so the callee changed it)");

        // 4. MakeCounter's frame is popped, but the local it captured lives on in a heap closure object.
        StackSection("4. Scope vs lifetime");
        Func<int> next = MakeCounter();
        Console.WriteLine($"  MakeCounter already returned, but its local count keeps going: {next()}, {next()}, {next()}");

        // 5. Stack<T> is an ordinary heap object our code manages. It is not the call stack.
        StackSection("5. The call stack vs a Stack<T> object");
        Stack<int> pending = new();
        for (int n = 1; n <= 4; n++)
        {
            pending.Push(n);
        }
        long product = 1;
        while (pending.Count > 0)
        {
            product *= pending.Pop();
        }
        Console.WriteLine($"  4! using a Stack<int> = {product}, same answer as section 2 without any extra frames:");
        PrintLiveCallStack();

        // 6. The stack has a fixed size, so deep recursion runs out of room. Each run stops early, because
        //    a real StackOverflowException cannot be caught in .NET and would end the whole program.
        StackSection("6. Deep recursion vs a fixed stack size");
        foreach (int kb in new[] { 512, 1024, 2048 })
        {
            int depth = 0;
            Thread worker = new(() => depth = RecurseUntilStackIsLow(1), kb * 1024);
            worker.Start();
            worker.Join();
            Console.WriteLine($"  {kb,4} KB thread stack -> stopped safely at depth {depth,6:N0}");
        }

        Console.WriteLine();
    }

    static string MakeBadge(int index)
    {
        StackPush($"MakeBadge(index: {index})");
        string name = Names[index];
        string initials = GetInitials(name); // this frame waits here until GetInitials returns
        StackNote($"resumed in MakeBadge, its local name is still \"{name}\"");
        string badge = $"{initials}-{Ids[index]}";
        StackPop("MakeBadge", badge);
        return badge;
    }

    static string GetInitials(string name)
    {
        StackPush($"GetInitials(name: \"{name}\")");
        string initials = string.Concat(name.Split(' ').Select(word => word[0]));
        PrintLiveCallStack(); // a breakpoint here shows the same frames in the debugger's Call Stack window
        StackPop("GetInitials", initials);
        return initials;
    }

    static long Factorial(int n)
    {
        StackPush($"Factorial(n: {n})");
        if (n <= 1)
        {
            PrintLiveCallStack();
        }
        // The multiply runs after the inner call returns, so this frame (and its n) has to wait.
        long result = n <= 1 ? 1 : n * Factorial(n - 1);
        StackPop($"Factorial(n: {n})", result);
        return result;
    }

    static void ChangeArguments(StackPoint point, StackOrder order, ref int count)
    {
        StackPush($"ChangeArguments(point: {point}, order: {order}, ref count: {count})");
        point.X = 99;        // changes this frame's own copy
        order.Quantity = 10; // follows the copied reference to the shared heap object
        count++;             // writes straight to the caller's variable
        StackNote($"inside the callee: point = {point}");
        StackPop("ChangeArguments");
    }

    static Func<int> MakeCounter()
    {
        StackPush("MakeCounter()");
        int count = 0;
        Func<int> increment = () => ++count; // capturing count moves it out of the frame into a heap closure
        StackPop("MakeCounter", "a delegate that still uses count");
        return increment;
    }

    static int RecurseUntilStackIsLow(int depth)
    {
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack())
        {
            return depth;
        }
        // Math.Max runs after the call returns, so the JIT can't turn this into a loop (a tail call).
        return Math.Max(depth, RecurseUntilStackIsLow(depth + 1));
    }

    // Prints the real call stack as reported by .NET, not just our own PUSH/POP bookkeeping.
    static void PrintLiveCallStack()
    {
        StackNote("[live call stack from .NET, most recent first]");
        foreach (StackFrame frame in new StackTrace(1).GetFrames())
        {
            if (frame.GetMethod() is { } method && method.DeclaringType == typeof(Program))
            {
                StackNote("  " + method.Name);
            }
        }
    }

    static void StackSection(string title) => Console.WriteLine($"\n--- STACK DEMO {title} ---");
    static void StackNote(string text) => Console.WriteLine(new string(' ', 2 + StackDepth * 4) + text);

    static void StackPush(string call)
    {
        StackNote("PUSH " + call);
        StackDepth++;
    }

    static void StackPop(string call, object? result = null)
    {
        StackDepth--;
        StackNote($"POP  {call}" + (result is null ? "" : $" -> {result}"));
    }

    // Value type: its fields are stored inline and copied when passed.
    record struct StackPoint(int X, int Y);

    // Reference type: the object lives on the heap, and variables hold a reference to it.
    class StackOrder(string id, int quantity)
    {
        public int Quantity = quantity;
        public override string ToString() => $"Order {id} x{Quantity}";
    }
}
