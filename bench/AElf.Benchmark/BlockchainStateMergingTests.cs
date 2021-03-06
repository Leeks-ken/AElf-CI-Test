using System.Collections.Generic;
using System.Threading.Tasks;
using AElf.Kernel;
using AElf.Kernel.Blockchain.Application;
using AElf.Kernel.Blockchain.Domain;
using AElf.Kernel.Blockchain.Infrastructure;
using AElf.Kernel.Infrastructure;
using AElf.Kernel.SmartContract.Application;
using AElf.Kernel.SmartContract.Domain;
using AElf.Kernel.SmartContract.Infrastructure;
using AElf.Kernel.TransactionPool.Application;
using AElf.OS;
using BenchmarkDotNet.Attributes;

namespace AElf.Benchmark;

[MarkdownExporterAttribute.GitHub]
public class BlockchainStateMergingTests : BenchmarkTestBase
{
    private IBlockchainService _blockchainService;
    private IBlockchainStateService _blockchainStateService;
    private IBlockManager _blockManager;
    private List<Block> _blocks;
    private IBlockStateSetManger _blockStateSetManger;
    private List<BlockStateSet> _blockStateSets;

    private Chain _chain;
    private IChainManager _chainManager;
    private IBlockchainStore<Chain> _chains;
    private ChainStateInfo _chainStateInfo;
    private IStateStore<ChainStateInfo> _chainStateInfoCollection;
    private OSTestHelper _osTestHelper;
    private ITransactionManager _transactionManager;
    private ITransactionPoolService _transactionPoolService;

    [Params(1, 10, 50)] public int BlockCount;

    [Params(1, 10, 100, 1000, 3000, 5000)] public int TransactionCount;

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        _chains = GetRequiredService<IBlockchainStore<Chain>>();
        _chainStateInfoCollection = GetRequiredService<IStateStore<ChainStateInfo>>();
        _blockchainStateService = GetRequiredService<IBlockchainStateService>();
        _blockStateSetManger = GetRequiredService<IBlockStateSetManger>();
        _blockchainService = GetRequiredService<IBlockchainService>();
        _osTestHelper = GetRequiredService<OSTestHelper>();
        _chainManager = GetRequiredService<IChainManager>();
        _blockManager = GetRequiredService<IBlockManager>();
        _transactionManager = GetRequiredService<ITransactionManager>();
        _transactionPoolService = GetRequiredService<ITransactionPoolService>();


        _blockStateSets = new List<BlockStateSet>();
        _blocks = new List<Block>();

        _chain = await _blockchainService.GetChainAsync();

        var blockHash = _chain.BestChainHash;
        while (true)
        {
            var blockState = await _blockStateSetManger.GetBlockStateSetAsync(blockHash);
            _blockStateSets.Add(blockState);

            var blockHeader = await _blockchainService.GetBlockHeaderByHashAsync(blockHash);
            blockHash = blockHeader.PreviousBlockHash;
            if (blockHash == _chain.LastIrreversibleBlockHash) break;
        }

        await _blockchainStateService.MergeBlockStateAsync(_chain.BestChainHeight, _chain.BestChainHash);

        for (var i = 0; i < BlockCount; i++)
        {
            var transactions = await _osTestHelper.GenerateTransferTransactions(TransactionCount);
            await _osTestHelper.BroadcastTransactions(transactions);
            var block = await _osTestHelper.MinedOneBlock();
            _blocks.Add(block);

            var blockState = await _blockStateSetManger.GetBlockStateSetAsync(block.GetHash());
            _blockStateSets.Add(blockState);
        }

        var chain = await _blockchainService.GetChainAsync();
        await _chainManager.SetIrreversibleBlockAsync(chain, chain.BestChainHash);

        _chainStateInfo = await _chainStateInfoCollection.GetAsync(chain.Id.ToStorageKey());
    }

    [Benchmark]
    public async Task MergeBlockStateTest()
    {
        var chain = await _blockchainService.GetChainAsync();
        await _blockchainStateService.MergeBlockStateAsync(chain.BestChainHeight, chain.BestChainHash);
    }

    [IterationCleanup]
    public async Task IterationCleanup()
    {
        await _chainStateInfoCollection.SetAsync(_chain.Id.ToStorageKey(), _chainStateInfo);
        foreach (var blockStateSet in _blockStateSets) await _blockStateSetManger.SetBlockStateSetAsync(blockStateSet);
    }

    [GlobalCleanup]
    public async Task GlobalCleanup()
    {
        foreach (var block in _blocks)
        {
            await _transactionPoolService.CleanByTransactionIdsAsync(block.TransactionIds);

            await _transactionManager.RemoveTransactionsAsync(block.Body.TransactionIds);
            await RemoveTransactionResultsAsync(block.Body.TransactionIds, block.GetHash());
            await _chainManager.RemoveChainBlockLinkAsync(block.GetHash());
            await _blockManager.RemoveBlockAsync(block.GetHash());
        }

        await _transactionPoolService.UpdateTransactionPoolByBestChainAsync(_chain.BestChainHash,
            _chain.BestChainHeight);

        await _chains.SetAsync(_chain.Id.ToStorageKey(), _chain);
    }
}