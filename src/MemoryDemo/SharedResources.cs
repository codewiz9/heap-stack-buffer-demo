namespace MemoryDemo;

static partial class Program
{
    static readonly string[] Names =
    [
        "Ada Lovelace",
        "Alan Turing",
        "Grace Hopper",
        "Katherine Johnson",
        "Margaret Hamilton",
    ];

    static readonly int[] Ids =
    [
        18421,
        19123,
        19062,
        19183,
        19368,
    ];

    static readonly int[] BankAccounts = Enumerable.Range(0, 1000)
        .Select(_ => Random.Shared.Next(1, 1001))
        .ToArray();
}
