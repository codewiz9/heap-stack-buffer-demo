namespace MemoryDemo;
using System;
using System.Collections.Generic;

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

    static readonly int[] Ids = Enumerable.Range(0, Names.Length)
        .Select(_ => Random.Shared.Next(10_000, 100_000))
        .ToArray();

    static readonly int[] BankAccounts = Enumerable.Range(0, 10000)
        .Select(_ => Random.Shared.Next(1, 100001))
        .ToArray();
}
