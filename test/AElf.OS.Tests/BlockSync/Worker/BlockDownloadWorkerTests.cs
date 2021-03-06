using System;
using System.Threading.Tasks;
using AElf.CSharp.Core.Extension;
using AElf.Kernel;
using AElf.Kernel.Blockchain.Application;
using AElf.OS.BlockSync.Domain;
using AElf.OS.BlockSync.Infrastructure;
using AElf.OS.Network.Application;
using Microsoft.Extensions.Options;
using Shouldly;
using Xunit;

namespace AElf.OS.BlockSync.Worker;

public class BlockDownloadWorkerTests : BlockSyncTestBase
{
    private readonly IBlockchainService _blockchainService;
    private readonly IBlockDownloadJobManager _blockDownloadJobManager;
    private readonly IBlockDownloadJobStore _blockDownloadJobStore;
    private readonly BlockDownloadWorker _blockDownloadWorker;
    private readonly BlockSyncOptions _blockSyncOptions;
    private readonly IBlockSyncStateProvider _blockSyncStateProvider;
    private readonly INetworkService _networkService;
    private readonly OSTestHelper _osTestHelper;

    public BlockDownloadWorkerTests()
    {
        _blockDownloadWorker = GetRequiredService<BlockDownloadWorker>();
        _blockDownloadJobStore = GetRequiredService<IBlockDownloadJobStore>();
        _blockDownloadJobManager = GetRequiredService<IBlockDownloadJobManager>();
        _blockchainService = GetRequiredService<IBlockchainService>();
        _networkService = GetRequiredService<INetworkService>();
        _blockSyncStateProvider = GetRequiredService<IBlockSyncStateProvider>();
        _blockSyncOptions = GetRequiredService<IOptionsSnapshot<BlockSyncOptions>>().Value;
        _osTestHelper = GetRequiredService<OSTestHelper>();
    }

    [Fact]
    public async Task ProcessDownloadJob_Success()
    {
        var response = await _networkService.GetBlockByHashAsync(HashHelper.ComputeFrom("PeerBlock"));
        var peerBlock = response.Payload;

        var jobId = await _blockDownloadJobManager.EnqueueAsync(peerBlock.GetHash(), peerBlock.Height,
            _blockSyncOptions.MaxBatchRequestBlockCount,
            null);

        await _blockDownloadWorker.ProcessDownloadJobAsync();

        var chain = await _blockchainService.GetChainAsync();
        chain.BestChainHeight.ShouldBe(31);

        var jobInfo = await _blockDownloadJobStore.GetFirstWaitingJobAsync();
        jobInfo.JobId.ShouldBe(jobId);
        _blockSyncStateProvider.TryGetDownloadJobTargetState(chain.BestChainHash, out var state).ShouldBeTrue();
        state.ShouldBeFalse();

        _blockSyncStateProvider.SetDownloadJobTargetState(chain.BestChainHash, true);

        await _blockDownloadWorker.ProcessDownloadJobAsync();

        jobInfo = await _blockDownloadJobStore.GetFirstWaitingJobAsync();
        jobInfo.ShouldBeNull();
        _blockSyncStateProvider.TryGetDownloadJobTargetState(chain.BestChainHash, out state).ShouldBeFalse();
    }

    [Fact]
    public async Task ProcessDownloadJob_CannotGetBlocks()
    {
        await _blockDownloadJobManager.EnqueueAsync(HashHelper.ComputeFrom("PeerBlock"), 30,
            _blockSyncOptions.MaxBatchRequestBlockCount,
            null);

        var block = await _osTestHelper.MinedOneBlock();
        var chain = await _blockchainService.GetChainAsync();
        await _blockchainService.SetIrreversibleBlockAsync(chain, block.Height, block.GetHash());

        await _blockDownloadWorker.ProcessDownloadJobAsync();

        chain = await _blockchainService.GetChainAsync();
        chain.BestChainHeight.ShouldBe(block.Height);

        var jobInfo = await _blockDownloadJobStore.GetFirstWaitingJobAsync();
        jobInfo.ShouldBeNull();
    }

    [Fact]
    public async Task ProcessDownloadJob_ThrowException()
    {
        await _blockDownloadJobManager.EnqueueAsync(HashHelper.ComputeFrom("PeerBlock"), 30,
            _blockSyncOptions.MaxBatchRequestBlockCount,
            "AbnormalPeer");

        _blockDownloadWorker.ProcessDownloadJobAsync().ShouldThrow<Exception>();

        var jobInfo = await _blockDownloadJobStore.GetFirstWaitingJobAsync();
        jobInfo.ShouldBeNull();
    }

    [Fact]
    public async Task ProcessDownloadJob_ValidateFailed()
    {
        var chain = await _blockchainService.GetChainAsync();
        var response = await _networkService.GetBlockByHashAsync(HashHelper.ComputeFrom("PeerBlock"));
        var peerBlock = response.Payload;
        var bestChainHash = chain.BestChainHash;
        var bestChainHeight = chain.BestChainHeight;

        // no job
        await _blockDownloadWorker.ProcessDownloadJobAsync();
        chain = await _blockchainService.GetChainAsync();
        chain.BestChainHash.ShouldBe(bestChainHash);
        chain.BestChainHeight.ShouldBe(bestChainHeight);

        await _blockDownloadJobManager.EnqueueAsync(peerBlock.GetHash(), peerBlock.Height,
            _blockSyncOptions.MaxBatchRequestBlockCount, null);

        // attach queue is too busy
        _blockSyncStateProvider.SetEnqueueTime(OSConstants.BlockSyncAttachQueueName,
            TimestampHelper.GetUtcNow().AddMilliseconds(-(BlockSyncConstants.BlockSyncAttachBlockAgeLimit + 100)));
        await _blockDownloadWorker.ProcessDownloadJobAsync();
        chain = await _blockchainService.GetChainAsync();
        chain.BestChainHash.ShouldBe(bestChainHash);
        chain.BestChainHeight.ShouldBe(bestChainHeight);

        _blockSyncStateProvider.SetEnqueueTime(OSConstants.BlockSyncAttachQueueName, null);

        // update queue is too busy
        _blockSyncStateProvider.SetEnqueueTime(KernelConstants.UpdateChainQueueName, TimestampHelper.GetUtcNow()
            .AddMilliseconds(-(BlockSyncConstants.BlockSyncAttachAndExecuteBlockAgeLimit + 100)));
        await _blockDownloadWorker.ProcessDownloadJobAsync();
        chain = await _blockchainService.GetChainAsync();
        chain.BestChainHash.ShouldBe(bestChainHash);
        chain.BestChainHeight.ShouldBe(bestChainHeight);

        // not reached the download target and less then deadline 
        var jobInfo = await _blockDownloadJobStore.GetFirstWaitingJobAsync();
        jobInfo.CurrentTargetBlockHash = jobInfo.TargetBlockHash;
        jobInfo.CurrentTargetBlockHeight = jobInfo.TargetBlockHeight;
        jobInfo.Deadline = TimestampHelper.GetUtcNow().AddSeconds(4);
        _blockSyncStateProvider.SetDownloadJobTargetState(jobInfo.TargetBlockHash, false);

        _blockSyncStateProvider.SetEnqueueTime(OSConstants.BlockSyncAttachQueueName, null);
        _blockSyncStateProvider.SetEnqueueTime(KernelConstants.UpdateChainQueueName, null);

        await _blockDownloadWorker.ProcessDownloadJobAsync();
        chain = await _blockchainService.GetChainAsync();
        chain.BestChainHash.ShouldBe(bestChainHash);
        chain.BestChainHeight.ShouldBe(bestChainHeight);
    }

    [Fact]
    public async Task ProcessDownloadJob_InvalidJob()
    {
        var chain = await _blockchainService.GetChainAsync();
        var bestChainHash = chain.BestChainHash;
        var bestChainHeight = chain.BestChainHeight;

        await _blockDownloadJobManager.EnqueueAsync(bestChainHash, bestChainHeight,
            _blockSyncOptions.MaxBatchRequestBlockCount, null);
        await _blockDownloadJobManager.EnqueueAsync(bestChainHash, bestChainHeight - 1,
            _blockSyncOptions.MaxBatchRequestBlockCount, null);

        await _blockDownloadWorker.ProcessDownloadJobAsync();

        chain = await _blockchainService.GetChainAsync();
        chain.BestChainHash.ShouldBe(bestChainHash);
        chain.BestChainHeight.ShouldBe(bestChainHeight);

        var jobInfo = await _blockDownloadJobStore.GetFirstWaitingJobAsync();
        jobInfo.ShouldBeNull();
    }
}