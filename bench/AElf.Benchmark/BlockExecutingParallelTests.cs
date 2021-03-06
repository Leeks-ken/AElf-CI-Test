using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AElf.Cryptography.ECDSA;
using AElf.Kernel;
using AElf.Kernel.Blockchain.Application;
using AElf.Kernel.Infrastructure;
using AElf.Kernel.Miner.Application;
using AElf.Kernel.SmartContract.Infrastructure;
using AElf.Kernel.SmartContractExecution.Application;
using AElf.OS;
using AElf.Types;
using BenchmarkDotNet.Attributes;

namespace AElf.Benchmark;

[MarkdownExporterAttribute.GitHub]
public class BlockExecutingParallelTests : BenchmarkParallelTestBase
{
    private Block _block;
    private IBlockchainService _blockchainService;
    private IBlockExecutingService _blockExecutingService;
    private INotModifiedCachedStateStore<BlockStateSet> _blockStateSets;
    private List<Transaction> _cancellableTransactions;
    private List<ECKeyPair> _keyPairs;
    private IMinerService _minerService;
    private OSTestHelper _osTestHelper;
    private List<Transaction> _prepareTransactions;

    private List<Transaction> _systemTransactions;

    [Params(1, 10, 100, 1000, 3000, 5000)] public int TransactionCount;

    [GlobalSetup]
    public void GlobalSetup()
    {
        _blockchainService = GetRequiredService<IBlockchainService>();
        _blockExecutingService = GetRequiredService<IBlockExecutingService>();
        _minerService = GetRequiredService<IMinerService>();
        _blockStateSets = GetRequiredService<INotModifiedCachedStateStore<BlockStateSet>>();
        _osTestHelper = GetRequiredService<OSTestHelper>();

        _prepareTransactions = new List<Transaction>();
        _systemTransactions = new List<Transaction>();
        _cancellableTransactions = new List<Transaction>();
        _keyPairs = new List<ECKeyPair>();
    }

    [IterationSetup]
    public async Task IterationSetup()
    {
        var chain = await _blockchainService.GetChainAsync();

        (_prepareTransactions, _keyPairs) = await _osTestHelper.PrepareTokenForParallel(TransactionCount);
        _block = _osTestHelper.GenerateBlock(chain.BestChainHash, chain.BestChainHeight, _prepareTransactions);
        await _blockExecutingService.ExecuteBlockAsync(_block.Header, _prepareTransactions);
        await _osTestHelper.BroadcastTransactions(_prepareTransactions);
        _block = (await _minerService.MineAsync(chain.BestChainHash, chain.BestChainHeight,
            TimestampHelper.GetUtcNow(), TimestampHelper.DurationFromSeconds(4))).Block;

        _systemTransactions = await _osTestHelper.GenerateTransferTransactions(1);
        _cancellableTransactions = await _osTestHelper.GenerateTransactionsWithoutConflictAsync(_keyPairs);
        chain = await _blockchainService.GetChainAsync();
        _block = _osTestHelper.GenerateBlock(chain.BestChainHash, chain.BestChainHeight,
            _systemTransactions.Concat(_cancellableTransactions));
    }

    [Benchmark]
    public async Task ExecuteBlock()
    {
        _block = (await _blockExecutingService.ExecuteBlockAsync(_block.Header,
            _systemTransactions, _cancellableTransactions, CancellationToken.None)).Block;
    }

    [IterationCleanup]
    public async Task IterationCleanup()
    {
        await _blockStateSets.RemoveAsync(_block.GetHash().ToStorageKey());

        var transactionIds = _systemTransactions.Concat(_cancellableTransactions).Select(t => t.GetHash()).ToList();
        await RemoveTransactionResultsAsync(transactionIds, _block.GetHash());
    }
}