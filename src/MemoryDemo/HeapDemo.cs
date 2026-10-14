namespace MemoryDemo;

class Customer
{
    public int Id { get; }
    public string Name { get; }
    public int BankAccount { get; }

    public Customer(int id, string name, int bankAccount)
    {
        Id = id;
        Name = name;
        BankAccount = bankAccount;
    }

    public override string ToString() =>
        $"{Name} | id {Id} | account {BankAccount}";
}

static partial class Program
{
    static void RunHeapDemo()
    {
        Console.WriteLine("=== HEAP DEMO ===");

        Console.WriteLine("--- Step 1: Roots on the heap ---");
        Dictionary<int, Customer>? customers = new();
        for (int i = 1; i <= 100; i++)
        {
            customers[i] = new Customer(i, $"Customer_{i:D3}", 5000);
        }

        Customer survivor = customers[1];
        Console.WriteLine($"Rooted customers: {customers.Count}");
        Console.WriteLine($"Customer #1 generation: {GC.GetGeneration(survivor)}");
        Console.WriteLine();

        Console.WriteLine("--- Step 2: Unreachable objects get reclaimed ---");
        int gen0Before = GC.CollectionCount(0);
        long beforeTemps = GC.GetTotalMemory(false);
        for (int i = 0; i < 50_000; i++)
        {
            _ = new Customer(i, "temp", i);
        }

        long afterAlloc = GC.GetTotalMemory(false);
        int gen0AfterAlloc = GC.CollectionCount(0);

        GC.Collect(0);
        long afterGen0 = GC.GetTotalMemory(false);
        int gen0AfterCollect = GC.CollectionCount(0);

        Console.WriteLine($"Customer #1 generation: {GC.GetGeneration(survivor)}");
        Console.WriteLine($"Rooted customers: {customers.Count}");
        Console.WriteLine($"Managed bytes before temporary allocations: {beforeTemps:N0}");
        Console.WriteLine($"Managed bytes after temporary allocations: {afterAlloc:N0}");
        Console.WriteLine($"Managed bytes after generation-0 collection: {afterGen0:N0}");
        Console.WriteLine($"Generation-0 collections during allocation: {gen0AfterAlloc - gen0Before}");
        Console.WriteLine($"Generation-0 collections including the explicit collection: {gen0AfterCollect - gen0Before}");
        Console.WriteLine();

        Console.WriteLine("--- Step 3: Promotion to the long-lived generation ---");
        GC.Collect(1);
        Console.WriteLine($"Customer #1 generation: {GC.GetGeneration(survivor)}");
        Console.WriteLine();

        Console.WriteLine("--- Step 4: Small object heap vs large object heap ---");
        var rng = new Random(42);
        int[] transactionAmounts = new int[10_000];
        for (int i = 0; i < transactionAmounts.Length; i++)
        {
            transactionAmounts[i] = rng.Next(1, 1001);
        }

        byte[] largeBuffer = new byte[85_000];
        Console.WriteLine($"Transaction array ({transactionAmounts.Length * sizeof(int):N0} bytes) generation: {GC.GetGeneration(transactionAmounts)}");
        Console.WriteLine($"Large buffer ({largeBuffer.Length:N0} bytes) generation: {GC.GetGeneration(largeBuffer)}");

        long beforeDrop = GC.GetTotalMemory(false);
        customers = null;
        GC.Collect();
        long afterDrop = GC.GetTotalMemory(false);
        Console.WriteLine($"Survivor generation after full collection: {GC.GetGeneration(survivor)}");
        Console.WriteLine($"Managed bytes before releasing the dictionary: {beforeDrop:N0}");
        Console.WriteLine($"Managed bytes after full collection: {afterDrop:N0}");
    }
}
