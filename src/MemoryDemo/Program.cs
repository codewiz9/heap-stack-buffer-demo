namespace MemoryDemo;

// Program is a partial class: each demo lives in its own file
// (StackDemo.cs, HeapDemo.cs, BufferDemo.cs) but compiles into this one class.
static partial class Program
{
    static void Main()
    {
        RunStackDemo();
        RunHeapDemo();
        RunBufferDemo();
        RunDebugDemo();
    }
}
