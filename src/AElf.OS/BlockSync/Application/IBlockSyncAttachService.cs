using System;
using System.Threading.Tasks;
using AElf.OS.Network;

namespace AElf.OS.BlockSync.Application;

public interface IBlockSyncAttachService
{
    Task AttachBlockWithTransactionsAsync(BlockWithTransactions blockWithTransactions,
        string senderPubkey, Func<Task> attachFinishedCallback = null);
}