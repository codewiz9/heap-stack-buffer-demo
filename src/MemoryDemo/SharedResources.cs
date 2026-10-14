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

    // Simple banking operations. An account number is an index into BankAccounts,
    // and the value stored there is that account's balance.

    static int GetBalance(int account)
    {
        return BankAccounts[account];
    }

    static void Deposit(int account, int amount)
    {
        BankAccounts[account] += amount;
    }

    static bool Withdraw(int account, int amount)
    {
        if (amount > BankAccounts[account])
            return false; // insufficient funds

        BankAccounts[account] -= amount;
        return true;
    }

    static bool Transfer(int fromAccount, int toAccount, int amount)
    {
        if (!Withdraw(fromAccount, amount))
            return false;

        Deposit(toAccount, amount);
        return true;
    }
}
