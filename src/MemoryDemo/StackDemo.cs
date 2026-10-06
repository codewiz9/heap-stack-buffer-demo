using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace MemoryDemo;

// STACK DEMO
// Shows how C# on .NET uses the call stack: every active method call gets a frame that
// holds that call's parameters, locals, and the information needed to return to the caller.
// Frames are pushed when a method is called and popped when it returns, last in, first out.
//
// Who is responsible for what:
//   - Our code decides which methods call which, so it decides the shape of the stack.
//   - The C# compiler and the .NET JIT decide each frame's layout: which locals sit in the
//     frame, which live only in CPU registers, and which get moved to the heap (captured locals).
//   - The operating system reserves each thread's stack memory (1 MB by default for the main
//     thread on Windows) and puts a guard page at the end to detect overflow.
//
// Methods marked NoInlining keep the JIT from merging small methods into their callers in
// Release builds, so every call shown here really gets its own frame.
//
// Helper types in this file start with "Stack" because every demo file compiles into the
// same partial Program class, so names must not clash with the other demos.
static partial class Program
{
    static void RunStackDemo()
    {
        Console.WriteLine("=== STACK DEMO ===");

        StackNestedCallsDemo();
        StackRecursionDemo();
        StackLocalsDemo();
        StackScopeAndLifetimeDemo();
        StackVsStackCollectionDemo();
        StackRecursionLimitDemo();

        Console.WriteLine();
    }

    // ------------------------------------------------------------------
    // 1. Nested method calls
    // ------------------------------------------------------------------

    static void StackNestedCallsDemo()
    {
        StackSection("1. Nested method calls: push on call, pop on return");

        string badge = PrintBadge(index: 2);

        Console.WriteLine($"  Back in StackNestedCallsDemo: badge = \"{badge}\"");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static string PrintBadge(int index)
    {
        StackCallLog.Enter($"PrintBadge(index: {index})");

        string name = Names[index];
        int id = Ids[index];
        StackCallLog.Note($"locals name = \"{name}\", id = {id}");

        // This frame pauses here while BuildBadge runs. The return address saved for this
        // call is how execution comes back to the next line.
        string badge = BuildBadge(name, id);
        StackCallLog.Note("resumed after BuildBadge returned");

        StackCallLog.Exit("PrintBadge", $"\"{badge}\"");
        return badge;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static string BuildBadge(string name, int id)
    {
        StackCallLog.Enter($"BuildBadge(name: \"{name}\", id: {id})");

        string initials = GetInitials(name);
        string badge = $"{initials}-{id}";
        StackCallLog.Note($"locals initials = \"{initials}\", badge = \"{badge}\"");

        StackCallLog.Exit("BuildBadge", $"\"{badge}\"");
        return badge;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static string GetInitials(string name)
    {
        StackCallLog.Enter($"GetInitials(name: \"{name}\")");

        string initials = string.Concat(name.Split(' ').Select(word => word[0]));

        // Deepest point of the chain. A breakpoint on the next line shows the same frames
        // in the debugger's Call Stack window.
        PrintLiveCallStack();

        StackCallLog.Exit("GetInitials", $"\"{initials}\"");
        return initials;
    }

    // ------------------------------------------------------------------
    // 2. Bounded recursion
    // ------------------------------------------------------------------

    static void StackRecursionDemo()
    {
        StackSection("2. Bounded recursion: every call gets its own frame and its own n");

        long result = Factorial(4);

        Console.WriteLine($"  Back in StackRecursionDemo: 4! = {result}");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static long Factorial(int n)
    {
        StackCallLog.Enter($"Factorial(n: {n})");

        long result;
        if (n <= 1)
        {
            StackCallLog.Note("base case reached, this is the deepest frame");
            PrintLiveCallStack();
            result = 1;
        }
        else
        {
            // The multiply happens after the recursive call returns, so this frame has to
            // stay on the stack (holding its own n) until the deeper call finishes.
            long smaller = Factorial(n - 1);
            result = n * smaller;
            StackCallLog.Note($"this frame's n is still {n}: {n} * {smaller} = {result}");
        }

        StackCallLog.Exit("Factorial", result);
        return result;
    }

    // ------------------------------------------------------------------
    // 3. Locals: values vs references
    // ------------------------------------------------------------------

    static void StackLocalsDemo()
    {
        StackSection("3. Locals: value types vs references to heap objects");

        // Measure heap allocation around each kind of local. Nothing is printed in between,
        // because Console.WriteLine would allocate and skew the numbers.
        long before = GC.GetAllocatedBytesForCurrentThread();
        int count = 5;
        StackPoint point = new StackPoint(1, 2);
        long afterValues = GC.GetAllocatedBytesForCurrentThread();
        StackOrder order = new StackOrder("A-100", 3);
        long afterObject = GC.GetAllocatedBytesForCurrentThread();

        Console.WriteLine($"  int count = 5 and StackPoint point = (1, 2)  -> heap bytes allocated: {afterValues - before}");
        Console.WriteLine($"  StackOrder order = new StackOrder(\"A-100\", 3) -> heap bytes allocated: {afterObject - afterValues}");
        Console.WriteLine("  count and point hold their data in this frame (or a register).");
        Console.WriteLine("  order is a local too, but the frame only holds a reference; the StackOrder object is on the heap.");
        Console.WriteLine();

        ChangeCopies(count, point, order);
        Console.WriteLine($"  Caller after ChangeCopies: count = {count}, point = {point}, order = {order}");
        Console.WriteLine("  count and point were copied, so the caller's values did not change.");
        Console.WriteLine("  order's reference was copied, but both copies point to the same heap object, so Quantity changed.");
        Console.WriteLine();

        ChangeThroughRef(ref count, ref point);
        Console.WriteLine($"  Caller after ChangeThroughRef: count = {count}, point = {point}");
        Console.WriteLine("  'ref' passed the address of the caller's variables, so the callee changed them directly.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void ChangeCopies(int count, StackPoint point, StackOrder order)
    {
        StackCallLog.Enter($"ChangeCopies(count: {count}, point: {point}, order: {order})");

        count++;                               // changes this frame's copy only
        point.X = 99;                          // changes this frame's copy of the struct only
        order.Quantity = 10;                   // follows the reference to the shared heap object
        order = new StackOrder("B-200", 1);    // points this frame's parameter at a new object; caller unaffected

        StackCallLog.Note($"inside callee: count = {count}, point = {point}, order = {order}");
        StackCallLog.Exit("ChangeCopies", "nothing (void)");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static void ChangeThroughRef(ref int count, ref StackPoint point)
    {
        StackCallLog.Enter($"ChangeThroughRef(ref count: {count}, ref point: {point})");

        count++;
        point.X = 99;

        StackCallLog.Exit("ChangeThroughRef", "nothing (void)");
    }

    // ------------------------------------------------------------------
    // 4. Scope vs lifetime
    // ------------------------------------------------------------------

    static void StackScopeAndLifetimeDemo()
    {
        StackSection("4. Scope vs lifetime");

        // (a) An object can outlive the frame that created it.
        StackOrder kept = CreateOrderAndReturn();
        Console.WriteLine($"  CreateOrderAndReturn's frame is gone, but the object it made is still usable: {kept}");
        Console.WriteLine("  The local 'created' went out of scope; the heap object lives while something still references it.");
        Console.WriteLine();

        // (b) A captured local outlives its method.
        Func<int> next = MakeCounter();
        Console.WriteLine($"  MakeCounter already returned, but its local 'count' keeps going: {next()}, {next()}, {next()}");
        Console.WriteLine("  The lambda captured 'count', so the compiler stored it in a heap closure object, not in the frame.");
        Console.WriteLine();

        // (c) Block scope is a compile-time rule, not a memory event.
        {
            int blockLocal = 42;
            Console.WriteLine($"  Inside a {{ }} block: blockLocal = {blockLocal}");
        }
        // Using blockLocal here would be a compile error. Its scope ended at the brace, but no memory
        // was "freed" there. The frame's space is given back only when this method returns.
        Console.WriteLine("  After the block, blockLocal is out of scope (compile error to use it),");
        Console.WriteLine("  but the frame that held it is only popped when this whole method returns.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static StackOrder CreateOrderAndReturn()
    {
        StackCallLog.Enter("CreateOrderAndReturn()");

        StackOrder created = new StackOrder("C-300", 7);

        StackCallLog.Exit("CreateOrderAndReturn", created);
        return created;
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static Func<int> MakeCounter()
    {
        StackCallLog.Enter("MakeCounter()");

        int count = 0;
        Func<int> increment = () => ++count;

        StackCallLog.Exit("MakeCounter", "a delegate that still uses 'count'");
        return increment;
    }

    // ------------------------------------------------------------------
    // 5. The call stack vs a Stack<T> data structure
    // ------------------------------------------------------------------

    static void StackVsStackCollectionDemo()
    {
        StackSection("5. The call stack vs a Stack<T> object in our program");

        StackFolder root = new StackFolder("root", 10,
            new StackFolder("docs", 20,
                new StackFolder("notes", 5)),
            new StackFolder("src", 40));

        Console.WriteLine("  Recursive version (the runtime's call stack tracks where we are):");
        int recursiveTotal = TotalSizeRecursive(root, depth: 1);

        Console.WriteLine();
        Console.WriteLine("  Iterative version (our own Stack<StackFolder> tracks where we are):");
        int iterativeTotal = TotalSizeIterative(root);

        Console.WriteLine();
        Console.WriteLine($"  Recursive total = {recursiveTotal} KB, iterative total = {iterativeTotal} KB, match: {recursiveTotal == iterativeTotal}");
        Console.WriteLine("  The call stack is managed by the runtime, holds frames (parameters, locals, return info),");
        Console.WriteLine("  and is limited by the thread's fixed stack size.");
        Console.WriteLine("  Stack<T> is an ordinary object: its backing array lives on the heap, our code pushes and pops it,");
        Console.WriteLine("  and it holds only the data we put in it. It is limited by heap memory, not stack size.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static int TotalSizeRecursive(StackFolder folder, int depth)
    {
        Console.WriteLine($"{StackCallLog.Pad(depth)}frame for {folder.Name} (call stack depth in this walk: {depth})");

        int total = folder.SizeKb;
        foreach (StackFolder child in folder.Children)
        {
            total += TotalSizeRecursive(child, depth + 1);
        }
        return total;
    }

    static int TotalSizeIterative(StackFolder root)
    {
        Stack<StackFolder> pending = new Stack<StackFolder>();
        pending.Push(root);

        int total = 0;
        while (pending.Count > 0)
        {
            StackFolder folder = pending.Pop();
            total += folder.SizeKb;
            foreach (StackFolder child in folder.Children)
            {
                pending.Push(child);
            }
            Console.WriteLine($"    popped {folder.Name,-6} Stack.Count is now {pending.Count} (still only one frame: TotalSizeIterative)");
        }
        return total;
    }

    // ------------------------------------------------------------------
    // 6. Deep recursion and the stack limit
    // ------------------------------------------------------------------

    // Safety net in case TryEnsureSufficientExecutionStack ever stops reporting low stack.
    const int StackHardDepthCap = 1_000_000;

    static void StackRecursionLimitDemo()
    {
        StackSection("6. Deep recursion: the stack has a fixed size");

        Console.WriteLine("  Running the same recursion on threads with different stack sizes.");
        Console.WriteLine("  Each call checks RuntimeHelpers.TryEnsureSufficientExecutionStack() and stops before overflowing.");

        foreach (int stackKb in new[] { 512, 1024, 2048 })
        {
            int depthReached = 0;
            Thread worker = new Thread(() => depthReached = RecurseUntilStackIsLow(1), maxStackSize: stackKb * 1024);
            worker.Start();
            worker.Join();

            Console.WriteLine($"    thread stack {stackKb,5} KB -> stopped safely at depth {depthReached,8:N0}");
        }

        Console.WriteLine("  Bigger stacks fit more frames, so the limit comes from the stack size the OS reserved.");
        Console.WriteLine("  Depths vary slightly between runs and are higher in Release builds (smaller frames).");
        Console.WriteLine("  Without the check, .NET would throw StackOverflowException. That exception cannot be caught:");
        Console.WriteLine("  the runtime ends the whole process. That is why this demo never actually overflows.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    static int RecurseUntilStackIsLow(int depth)
    {
        if (!RuntimeHelpers.TryEnsureSufficientExecutionStack() || depth >= StackHardDepthCap)
        {
            return depth;
        }

        // Doing work after the call keeps it from being a tail call, so every level keeps a frame.
        int deepest = RecurseUntilStackIsLow(depth + 1);
        return Math.Max(depth, deepest);
    }

    // ------------------------------------------------------------------
    // Helpers
    // ------------------------------------------------------------------

    static void StackSection(string title)
    {
        Console.WriteLine();
        Console.WriteLine($"--- STACK DEMO {title} ---");
    }

    // Asks the runtime for the real call stack at this moment, so the output is evidence from
    // .NET itself and not only our own StackCallLog bookkeeping.
    [MethodImpl(MethodImplOptions.NoInlining)]
    static void PrintLiveCallStack()
    {
        StackFrame[] frames = new StackTrace(skipFrames: 1).GetFrames();
        string pad = StackCallLog.Pad(StackCallLog.Depth);

        Console.WriteLine($"{pad}[live call stack from System.Diagnostics.StackTrace, most recent first]");
        foreach (StackFrame frame in frames)
        {
            if (frame.GetMethod() is not { DeclaringType: { Namespace: "MemoryDemo" } owner } method)
            {
                continue;
            }
            Console.WriteLine($"{pad}  {owner.Name}.{method.Name}");
        }
        Console.WriteLine($"{pad}  ({frames.Length} frames reported by the runtime)");
    }

    // Prints PUSH/POP lines indented by how many traced calls are currently active.
    static class StackCallLog
    {
        public static int Depth { get; private set; }

        public static void Enter(string call)
        {
            Console.WriteLine($"{Pad(Depth)}PUSH {call}");
            Depth++;
        }

        public static void Note(string text)
        {
            Console.WriteLine($"{Pad(Depth)}{text}");
        }

        public static void Exit(string method, object? result)
        {
            Depth--;
            Console.WriteLine($"{Pad(Depth)}POP  {method} -> returns {result}");
        }

        public static string Pad(int depth) => new string(' ', 2 + depth * 4);
    }

    // A value type: a StackPoint local stores its X and Y directly, and passing it copies both.
    struct StackPoint
    {
        public int X;
        public int Y;

        public StackPoint(int x, int y)
        {
            X = x;
            Y = y;
        }

        public override string ToString() => $"({X}, {Y})";
    }

    // A reference type: a StackOrder local stores only a reference; the object is on the heap.
    sealed class StackOrder
    {
        public string Id { get; }
        public int Quantity { get; set; }

        public StackOrder(string id, int quantity)
        {
            Id = id;
            Quantity = quantity;
        }

        public override string ToString() => $"Order {Id} x{Quantity}";
    }

    sealed class StackFolder
    {
        public string Name { get; }
        public int SizeKb { get; }
        public StackFolder[] Children { get; }

        public StackFolder(string name, int sizeKb, params StackFolder[] children)
        {
            Name = name;
            SizeKb = sizeKb;
            Children = children;
        }
    }
}
