using System.Linq;

namespace AElf.Kernel;

public static class TransactionResultExtensions
{
    public static void UpdateBloom(this TransactionResult transactionResult)
    {
        var bloom = new Bloom();
        bloom.Combine(transactionResult.Logs.Select(l => l.GetBloom()));
        transactionResult.Bloom = ByteString.CopyFrom(bloom.Data);
    }
}