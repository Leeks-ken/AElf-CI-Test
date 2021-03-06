using System.Threading.Tasks;
using AElf.Kernel;
using AElf.Kernel.Blockchain.Application;
using AElf.Kernel.Blockchain.Domain;
using AElf.Kernel.Blockchain.Infrastructure;
using AElf.Kernel.Infrastructure;
using AElf.Kernel.SmartContract.Infrastructure;
using AElf.Kernel.SmartContractExecution.Application;
using AElf.OS;
using BenchmarkDotNet.Attributes;

namespace AElf.Benchmark;

[MarkdownExporterAttribute.GitHub]
public class BlockAttachTests : BenchmarkTestBase
{
    private Block _block;
    private IBlockAttachService _blockAttachService;
    private IBlockchainService _blockchainService;
    private IBlockManager _blockManager;
    private INotModifiedCachedStateStore<BlockStateSet> _blockStateSets;

    private Chain _chain;
    private IChainManager _chainManager;
    private IBlockchainStore<Chain> _chains;
    private OSTestHelper _osTestHelper;
    private ITransactionManager _transactionManager;

    [Params(1, 10, 100, 1000, 3000, 5000)] public int TransactionCount;

    [GlobalSetup]
    public async Task GlobalSetup()
    {
        _chains = GetRequiredService<IBlockchainStore<Chain>>();
        _blockStateSets = GetRequiredService<INotModifiedCachedStateStore<BlockStateSet>>();
        _chainManager = GetRequiredService<IChainManager>();
        _blockManager = GetRequiredService<IBlockManager>();
        _blockchainService = GetRequiredService<IBlockchainService>();
        _blockAttachService = GetRequiredService<IBlockAttachService>();
        _transactionManager = GetRequiredService<ITransactionManager>();
        _osTestHelper = GetRequiredService<OSTestHelper>();

        _chain = await _blockchainService.GetChainAsync();
    }

    [IterationSetup]
    public async Task IterationSetup()
    {
        var transactions = await _osTestHelper.GenerateTransferTransactions(TransactionCount);
        await _blockchainService.AddTransactionsAsync(transactions);
        _block = _osTestHelper.GenerateBlock(_chain.BestChainHash, _chain.BestChainHeight, transactions);

        await _blockchainService.AddBlockAsync(_block);
    }

    [Benchmark]
    public async Task AttachBlockTest()
    {
        await _blockAttachService.AttachBlockAsync(_block);
    }

    [IterationCleanup]
    public async Task IterationCleanup()
    {
        await _blockStateSets.RemoveAsync(_block.GetHash().ToStorageKey());
        await _transactionManager.RemoveTransactionsAsync(_block.Body.TransactionIds);
        await RemoveTransactionResultsAsync(_block.Body.TransactionIds, _block.GetHash());
        await _chainManager.RemoveChainBlockLinkAsync(_block.GetHash());
        await _blockManager.RemoveBlockAsync(_block.GetHash());
        await _chains.SetAsync(_chain.Id.ToStorageKey(), _chain);
    }
}