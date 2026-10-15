namespace MemoryDemo;

static partial class Program
{
    // capacity of buffer.
    const int BufferSize = 8192;

    // how many bytes of the first chunk to print
    const int BufferPreviewBytes = 12;

    // each approach is measured this many times
    const int BufferMeasureRuns = 3;

    static void RunBufferDemo()
    {
        Console.WriteLine("=== BUFFER DEMO ===");
        Console.WriteLine();

        // save every account's balance to a file, one line per account: "account,balance"
        string path = Path.Combine(Path.GetTempPath(), "balances.txt");

        // write the file with shared resource
        BufferWriteBalancesFile(path, BankAccounts.Length);

        // The file is bigger than the buffer, so it can't be read in one go
        Console.WriteLine($"File: {new FileInfo(path).Length:N0} bytes, {BankAccounts.Length} accounts " +
                          $"(bigger than the {BufferSize}-byte buffer, so it takes several reads)");
        Console.WriteLine($"Reading it through a {BufferSize}-byte buffer:");
        int lines = BufferCountLinesInChunks(path, showReads: true);

        // Each account wrote exactly one line, so the count must equal the number of accounts
        Console.WriteLine($"Lines counted: {lines}, expected: {BankAccounts.Length} (one per account), " +
                          $"match: {lines == BankAccounts.Length}");

        File.Delete(path);

        Console.WriteLine();
        Console.WriteLine("=== MEMORY COMPARISON ===");
        Console.WriteLine("Same task (count lines) and same file for both approaches, at three file sizes.");

        // Small file, the real 1000-account file, and a large file, so we can see
        // which approach's memory use grows with the file and which stays the same
        foreach (int lineCount in new[] { 100, BankAccounts.Length, 100_000 })
        { 
            //make a file of that size, then run both approaches on it and compare results.
            string comparePath = Path.Combine(Path.GetTempPath(), $"balances_{lineCount}.txt");
            BufferWriteBalancesFile(comparePath, lineCount);
            BufferCompareOnFile(comparePath, lineCount);
            File.Delete(comparePath);
        }

        Console.WriteLine();
        Console.WriteLine("Measured: total heap bytes each approach allocated while counting.");
        Console.WriteLine("Inferred: how much each one holds at the same time. Whole-file holds the entire file in one");
        Console.WriteLine("array; chunked holds only its one buffer. The counter adds up allocations, it does not report peak memory.");
        Console.WriteLine();
    }

    // Writes lineCount lines of "account,balance". Past 1000 lines the accounts repeat,
    // which just makes a bigger file with the same kind of data.
    static void BufferWriteBalancesFile(string path, int lineCount)
    {
        // streamWriter turns text into bytes and collects them in its own buffer. Disposing it
        // (end of this method) flushes what's left to the file and closes the file handle.
        using StreamWriter writer = new StreamWriter(path);
        for (int line = 0; line < lineCount; line++)
        {
            int account = line % BankAccounts.Length;
            writer.Write($"{account},{GetBalance(account)}\n");
        }
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
        byte[] buffer = new byte[BufferSize]; // created once, reused for every read, lives on the heap

        int lines = 0;

        // bufferSize: 0 turns off FileStream's own hidden 4 KB buffer. Without it, our 1 KB reads
        // would be copied out of a second buffer, and that buffer would show up in the measurement.
        using FileStream file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 0);

        int chunk = 0;
        int bytesRead;
        // Fill the buffer starting at slot 0 with up to 1024 bytes; Read returns how many bytes
        // it actually put in (0 = end of file)
        while ((bytesRead = file.Read(buffer, 0, buffer.Length)) > 0)
        {
            chunk++;

            // Only the first bytesRead bytes are valid; anything after is left over
            // from the previous read, so we never look past bytesRead.
            for (int i = 0; i < bytesRead; i++)
                if (buffer[i] == '\n') lines++;

            if (!showReads)
                continue;

            bool partial = bytesRead < buffer.Length;
            Console.WriteLine($"  read {bytesRead,4} of {buffer.Length} bytes" +
                              (partial ? "  <- final partial chunk" : ""));

            if (chunk == 1)
                BufferPrintPreview(buffer, Math.Min(BufferPreviewBytes, bytesRead));

            if (partial && chunk > 1)
            {
                // Count the newlines in the stale part of the buffer, to show what a loop
                // to buffer.Length (instead of bytesRead) would have gotten wrong.
                int staleNewlines = 0;
                for (int i = bytesRead; i < buffer.Length; i++)
                    if (buffer[i] == '\n') staleNewlines++;

                Console.WriteLine($"    slots {bytesRead}-{buffer.Length - 1} still hold old bytes from the previous read, " +
                                  $"including {staleNewlines} newlines.");
                Console.WriteLine($"    looping to buffer.Length would have counted {lines + staleNewlines} lines " +
                                  $"instead of {lines}, so the loop stops at bytesRead.");
            }
        }
        return lines;
    }

    // Prints the first bytes of the buffer as numbers, then as the characters they stand for.
    static void BufferPrintPreview(byte[] buffer, int count)
    {
        string numbers = "";
        string characters = "";
        for (int i = 0; i < count; i++)
        {
            numbers += $"{buffer[i],4}";
            characters += buffer[i] == '\n' ? "  \\n" : $"{(char)buffer[i],4}";
        }
        // -24 pads both labels to the same width so the two rows line up
        Console.WriteLine($"    {$"buffer[0..{count - 1}] as bytes:",-24}{numbers}");
        Console.WriteLine($"    {"same bytes as text:",-24}{characters}");
    }

    // Runs both approaches on the same file and checks that they agree.
    static void BufferCompareOnFile(string path, int expectedLines)
    {
        Console.WriteLine();
        Console.WriteLine($"  File: {new FileInfo(path).Length:N0} bytes, {expectedLines:N0} lines");
        int wholeLines = BufferMeasure("Whole file at once", () => BufferCountLinesWholeFile(path));
        int chunkLines = BufferMeasure("Chunks, one buffer", () => BufferCountLinesInChunks(path, showReads: false));
        Console.WriteLine($"    results agree: {wholeLines == chunkLines}, match expected {expectedLines:N0}: " +
                          $"{wholeLines == expectedLines && chunkLines == expectedLines}");
    }

    // Measures how many heap bytes this thread allocates while the approach runs, several times.
    static int BufferMeasure(string name, Func<int> approach)
    {
        long[] allocated = new long[BufferMeasureRuns];
        int lines = 0;

        for (int run = 0; run < BufferMeasureRuns; run++)
        {
            // A running total of every byte this thread has ever allocated on the heap
            long before = GC.GetAllocatedBytesForCurrentThread();
            // Run either BufferCountLinesWholeFile or BufferCountLinesInChunks
            lines = approach();
            allocated[run] = GC.GetAllocatedBytesForCurrentThread() - before;
        }

        Console.WriteLine($"    {name,-20} lines: {lines,7:N0}   bytes allocated per run: " +
                          string.Join(", ", allocated.Select(a => $"{a:N0}")));
        return lines;
    }
}
