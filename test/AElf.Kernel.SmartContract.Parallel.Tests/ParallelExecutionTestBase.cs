using System.Collections.Generic;
using AElf.Cryptography.ECDSA;
using AElf.Kernel.Blockchain.Application;
using AElf.Kernel.Blockchain.Domain;
using AElf.Kernel.Miner.Application;
using AElf.Kernel.SmartContract.Application;
using AElf.Kernel.SmartContract.Infrastructure;
using AElf.Kernel.SmartContractExecution.Application;
using AElf.OS;
using AElf.TestBase;
using AElf.Types;

namespace AElf.Kernel.SmartContract.Parallel.Tests;

public class ParallelExecutionTestBase : AElfIntegratedTest<ParallelExecutionTestModule>
{
    protected Block Block;
    protected List<Transaction> CancellableTransactions;
    protected List<ECKeyPair> KeyPairs;
    protected List<Transaction> PrepareTransactions;

    protected List<Transaction> SystemTransactions;

    public ParallelExecutionTestBase()
    {
        BlockchainService = GetRequiredService<IBlockchainService>();
        BlockExecutingService = GetRequiredService<IBlockExecutingService>();
        MinerService = GetRequiredService<IMinerService>();
        SmartContractAddressService = GetRequiredService<ISmartContractAddressService>();
        TransactionResultManager = GetRequiredService<ITransactionResultManager>();
        TransactionReadOnlyExecutionService = GetRequiredService<ITransactionReadOnlyExecutionService>();
        BlockStateSets = GetRequiredService<INotModifiedCachedStateStore<BlockStateSet>>();
        OsTestHelper = GetRequiredService<OSTestHelper>();
        BlockAttachService = GetRequiredService<IBlockAttachService>();

        PrepareTransactions = new List<Transaction>();
        SystemTransactions = new List<Transaction>();
        CancellableTransactions = new List<Transaction>();
        KeyPairs = new List<ECKeyPair>();
        ;
    }

    protected IBlockExecutingService BlockExecutingService { get; set; }
    protected IBlockchainService BlockchainService { get; set; }
    protected IBlockAttachService BlockAttachService { get; set; }
    protected IMinerService MinerService { get; set; }

    protected ISmartContractAddressService SmartContractAddressService { get; set; }
    protected ITransactionResultManager TransactionResultManager { get; set; }
    protected ITransactionReadOnlyExecutionService TransactionReadOnlyExecutionService { get; set; }
    protected INotModifiedCachedStateStore<BlockStateSet> BlockStateSets { get; set; }
    protected OSTestHelper OsTestHelper { get; set; }
}