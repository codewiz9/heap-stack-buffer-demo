namespace MemoryDemo;

static partial class Program
{
    const int BufferSize = 1024;

    static void RunBufferDemo()
    {
        Console.WriteLine("=== BUFFER DEMO ===");

        // Save every account's balance to a file, one line per account: "account,balance"
        string path = Path.Combine(Path.GetTempPath(), "balances.txt");
        using (StreamWriter writer = new StreamWriter(path))
        {
            for (int account = 0; account < BankAccounts.Length; account++)
                writer.Write($"{account},{GetBalance(account)}\n");
        } 


        //print file sizes
        Console.WriteLine($"File: {new FileInfo(path).Length} bytes, {BankAccounts.Length} accounts");
        Console.WriteLine($"Reading it through a {BufferSize}-byte buffer:");
        BufferCountLinesInChunks(path, showReads: true);

        Console.WriteLine();
        Console.WriteLine("=== MEMORY COMPARISON ===");
        //count file size all at once 
        BufferCompare("Whole file at once", () => BufferCountLinesWholeFile(path));
        //count file size in chunks
        BufferCompare("Chunks, one buffer", () => BufferCountLinesInChunks(path, showReads: false));

        File.Delete(path);
    }

    // Approach 1: load the entire file into one array, then count lines.
    static int BufferCountLinesWholeFile(string path)
    {
        byte[] wholeFile = File.ReadAllBytes(path); // array as big as the file, on the heap

        int lines = 0;
        foreach (byte b in wholeFile)
            if (b == '\n') lines++;
        return lines;
    }

    // Approach 2: read the file in pieces through one small, reused buffer.
    static int BufferCountLinesInChunks(string path, bool showReads)
    {
        byte[] buffer = new byte[BufferSize]; // created once, reused for every read

        int lines = 0;
        using FileStream file = File.OpenRead(path);

        int bytesRead;
        //file buffer starting a slot 0, with up to 1024 bytes, and then return how many bytes were put in
        while ((bytesRead = file.Read(buffer, 0, buffer.Length)) > 0)
        {
            if (showReads)
                Console.WriteLine($"  read {bytesRead,4} of {buffer.Length} bytes" +
                                  (bytesRead < buffer.Length ? "  <- final partial chunk" : ""));

            // Only the first bytesRead bytes are valid; anything after is left over
            // from the previous read, so we never look past bytesRead.
            for (int i = 0; i < bytesRead; i++)
                if (buffer[i] == '\n') lines++;
        }
        return lines;
    }

    // Measures how many bytes this thread allocates while the approach runs.
    static void BufferCompare(string name, Func<int> approach)
    {
        // a total of every byte this thread has allocated on the heap
        long before = GC.GetAllocatedBytesForCurrentThread();
        // run either BufferCountLinesWholeFile or BufferCountLinesInChunks, and return the number of lines counted
        int lines = approach();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;

        Console.WriteLine($"{name,-20} lines: {lines}   bytes allocated: {allocated:N0}");
    }
}
