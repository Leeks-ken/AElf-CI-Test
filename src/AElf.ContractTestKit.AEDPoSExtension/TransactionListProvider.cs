using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Types;

namespace AElf.ContractTestKit.AEDPoSExtension;

public class TransactionListProvider : ITransactionListProvider
{
    private readonly List<Transaction> _transactionList = new();

    public Task AddTransactionListAsync(List<Transaction> transactions)
    {
        _transactionList.AddRange(transactions);
        return Task.CompletedTask;
    }

    public Task<List<Transaction>> GetTransactionListAsync()
    {
        var list = _transactionList;
        return Task.FromResult(list);
    }

    public Task ResetAsync()
    {
        _transactionList.Clear();
        return Task.CompletedTask;
    }
}