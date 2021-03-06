namespace AElf.Kernel.Txn.Application;

public interface ITransactionValidationProvider
{
    bool ValidateWhileSyncing { get; }
    Task<bool> ValidateTransactionAsync(Transaction transaction, IChainContext chainContext = null);
}