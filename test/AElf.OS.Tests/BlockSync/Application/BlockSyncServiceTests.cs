using System.Linq;
using System.Threading.Tasks;
using AElf.CSharp.Core.Extension;
using AElf.Kernel;
using AElf.Kernel.Blockchain.Application;
using AElf.OS.BlockSync.Dto;
using AElf.OS.BlockSync.Infrastructure;
using AElf.OS.Network.Application;
using AElf.Types;
using Shouldly;
using Xunit;

namespace AElf.OS.BlockSync.Application;

public class BlockSyncServiceTests : BlockSyncTestBase
{
    private readonly IAnnouncementCacheProvider _announcementCacheProvider;
    private readonly IBlockchainService _blockchainService;
    private readonly IBlockDownloadJobStore _blockDownloadJobStore;
    private readonly IBlockSyncService _blockSyncService;
    private readonly IBlockSyncStateProvider _blockSyncStateProvider;
    private readonly INetworkService _networkService;
    private readonly OSTestHelper _osTestHelper;

    public BlockSyncServiceTests()
    {
        _blockSyncService = GetRequiredService<IBlockSyncService>();
        _blockchainService = GetRequiredService<IBlockchainService>();
        _networkService = GetRequiredService<INetworkService>();
        _blockSyncStateProvider = GetRequiredService<IBlockSyncStateProvider>();
        _blockDownloadJobStore = GetRequiredService<IBlockDownloadJobStore>();
        _announcementCacheProvider = GetRequiredService<IAnnouncementCacheProvider>();
        _osTestHelper = GetRequiredService<OSTestHelper>();
    }

    [Fact]
    public async Task SyncByAnnounce_Success()
    {
        var chain = await _blockchainService.GetChainAsync();
        var resp = await _networkService.GetBlocksAsync(chain.BestChainHash, 30);
        var peerBlocks = resp.Payload;

        var block = peerBlocks[0];
        var peerBlockHash = block.GetHash();
        var peerBlockHeight = block.Height;
        {
            // Sync one block to best chain
            // BestChainHeight: 12
            await _blockSyncService.SyncByAnnouncementAsync(chain, new SyncAnnouncementDto
            {
                SyncBlockHash = peerBlockHash,
                SyncBlockHeight = peerBlockHeight,
                BatchRequestBlockCount = 5
            });
            chain = await _blockchainService.GetChainAsync();
            chain.BestChainHeight.ShouldBe(12);
            chain.BestChainHash.ShouldBe(peerBlocks[0].GetHash());
        }

        {
            // Handle the same announcement again
            // BestChainHeight: 12
            await _blockSyncService.SyncByAnnouncementAsync(chain, new SyncAnnouncementDto
            {
                SyncBlockHash = peerBlockHash,
                SyncBlockHeight = peerBlockHeight,
                BatchRequestBlockCount = 5
            });
            chain = await _blockchainService.GetChainAsync();
            chain.BestChainHash.ShouldBe(peerBlocks[0].GetHash());
            chain.BestChainHeight.ShouldBe(12);
        }

        {
            // Mined one block, and fork
            await _osTestHelper.MinedOneBlock();
            chain = await _blockchainService.GetChainAsync();
            chain.BestChainHeight.ShouldBe(13);
        }

        {
            // Receive a higher fork block, sync from the lib
            // BestChainHeight: 31
            block = peerBlocks.Last();
            peerBlockHash = block.GetHash();
            peerBlockHeight = block.Height;
            await _blockSyncService.SyncByAnnouncementAsync(chain, new SyncAnnouncementDto
            {
                SyncBlockHash = peerBlockHash,
                SyncBlockHeight = peerBlockHeight,
                BatchRequestBlockCount = 5
            });

            var jobInfo = await _blockDownloadJobStore.GetFirstWaitingJobAsync();
            jobInfo.TargetBlockHeight.ShouldBe(peerBlockHeight);
            jobInfo.TargetBlockHash.ShouldBe(peerBlockHash);
        }
    }

    [Fact]
    public async Task SyncByAnnounce_LessThenFetchLimit_Success()
    {
        var response = await _networkService.GetBlockByHashAsync(HashHelper.ComputeFrom("PeerBlock"));
        var peerBlock = response.Payload;

        var block = await _blockchainService.GetBlockByHashAsync(peerBlock.GetHash());
        block.ShouldBeNull();

        var chain = await _blockchainService.GetChainAsync();
        await _blockSyncService.SyncByAnnouncementAsync(chain, new SyncAnnouncementDto
        {
            SyncBlockHash = peerBlock.GetHash(),
            SyncBlockHeight = peerBlock.Height,
            BatchRequestBlockCount = 5
        });

        block = await _blockchainService.GetBlockByHashAsync(peerBlock.GetHash());
        block.GetHash().ShouldBe(peerBlock.GetHash());

        chain = await _blockchainService.GetChainAsync();
        chain.BestChainHash.ShouldBe(peerBlock.GetHash());
        chain.BestChainHeight.ShouldBe(peerBlock.Height);
    }

    [Fact]
    public async Task SyncByAnnounce_FetchQueueIsBusy()
    {
        var response = await _networkService.GetBlockByHashAsync(HashHelper.ComputeFrom("PeerBlock"));
        var peerBlock = response.Payload;

        var block = await _blockchainService.GetBlockByHashAsync(peerBlock.GetHash());
        block.ShouldBeNull();

        var chain = await _blockchainService.GetChainAsync();
        var bestChainHash = chain.BestChainHash;
        var bestChainHeight = chain.BestChainHeight;

        _blockSyncStateProvider.SetEnqueueTime(OSConstants.BlockFetchQueueName, TimestampHelper.GetUtcNow()
            .AddMilliseconds(-(BlockSyncConstants.BlockSyncFetchBlockAgeLimit + 100)));

        await _blockSyncService.SyncByAnnouncementAsync(chain, new SyncAnnouncementDto
        {
            SyncBlockHash = peerBlock.GetHash(),
            SyncBlockHeight = peerBlock.Height,
            BatchRequestBlockCount = 5
        });

        block = await _blockchainService.GetBlockByHashAsync(peerBlock.GetHash());
        block.ShouldBeNull();

        chain = await _blockchainService.GetChainAsync();
        chain.BestChainHash.ShouldBe(bestChainHash);
        chain.BestChainHeight.ShouldBe(bestChainHeight);
    }

    [Fact]
    public async Task SyncByAnnounce_LessThenFetchLimit_FetchReturnFalse()
    {
        var chain = await _blockchainService.GetChainAsync();
        var bestChainHash = chain.BestChainHash;
        var bestChainHeight = chain.BestChainHeight;

        await _blockSyncService.SyncByAnnouncementAsync(chain, new SyncAnnouncementDto
        {
            SyncBlockHash = Hash.Empty,
            SyncBlockHeight = 12,
            BatchRequestBlockCount = 5
        });

        chain = await _blockchainService.GetChainAsync();
        chain.BestChainHash.ShouldBe(bestChainHash);
        chain.BestChainHeight.ShouldBe(bestChainHeight);
    }

    [Fact]
    public async Task SyncByAnnounce_MoreThenFetchLimit_Success()
    {
        var chain = await _blockchainService.GetChainAsync();

        var peerBlockHash = HashHelper.ComputeFrom("PeerBlock");
        var peerBlockHeight = chain.LongestChainHeight + BlockSyncConstants.BlockSyncModeHeightOffset + 1;

        await _blockSyncService.SyncByAnnouncementAsync(chain, new SyncAnnouncementDto
        {
            SyncBlockHash = peerBlockHash,
            SyncBlockHeight = peerBlockHeight,
            BatchRequestBlockCount = 5
        });

        var jobInfo = await _blockDownloadJobStore.GetFirstWaitingJobAsync();
        jobInfo.TargetBlockHeight.ShouldBe(peerBlockHeight);
        jobInfo.TargetBlockHash.ShouldBe(peerBlockHash);
    }

    [Fact]
    public async Task SyncByAnnounce_Fetch_AttachAndExecuteQueueIsBusy()
    {
        _blockSyncStateProvider.SetEnqueueTime(KernelConstants.UpdateChainQueueName,
            TimestampHelper.GetUtcNow()
                .AddMilliseconds(-(BlockSyncConstants.BlockSyncAttachAndExecuteBlockAgeLimit + 100)));

        var response = await _networkService.GetBlockByHashAsync(HashHelper.ComputeFrom("PeerBlock"));
        var peerBlock = response.Payload;

        var chain = await _blockchainService.GetChainAsync();
        await _blockSyncService.SyncByAnnouncementAsync(chain, new SyncAnnouncementDto
        {
            SyncBlockHash = peerBlock.GetHash(),
            SyncBlockHeight = chain.LongestChainHeight + BlockSyncConstants.BlockSyncModeHeightOffset,
            BatchRequestBlockCount = 5
        });

        var block = await _blockchainService.GetBlockByHashAsync(peerBlock.GetHash());
        block.ShouldBeNull();
    }

    [Fact]
    public async Task SyncByAnnounce_Download_AttachAndExecuteQueueIsBusy()
    {
        _blockSyncStateProvider.SetEnqueueTime(KernelConstants.UpdateChainQueueName,
            TimestampHelper.GetUtcNow()
                .AddMilliseconds(-(BlockSyncConstants.BlockSyncAttachAndExecuteBlockAgeLimit + 100)));

        var response = await _networkService.GetBlockByHashAsync(HashHelper.ComputeFrom("PeerBlock"));
        var peerBlock = response.Payload;

        var chain = await _blockchainService.GetChainAsync();
        var bestChainHash = chain.BestChainHash;
        var bestChainHeight = chain.BestChainHeight;

        await _blockSyncService.SyncByAnnouncementAsync(chain, new SyncAnnouncementDto
        {
            SyncBlockHash = peerBlock.GetHash(),
            SyncBlockHeight = chain.LongestChainHeight + BlockSyncConstants.BlockSyncModeHeightOffset + 1,
            BatchRequestBlockCount = 5
        });

        chain = await _blockchainService.GetChainAsync();
        chain.BestChainHash.ShouldBe(bestChainHash);
        chain.BestChainHeight.ShouldBe(bestChainHeight);
    }

    [Fact]
    public async Task SyncByAnnounce_Fetch_AttachQueueIsBusy()
    {
        _blockSyncStateProvider.SetEnqueueTime(OSConstants.BlockSyncAttachQueueName,
            TimestampHelper.GetUtcNow().AddMilliseconds(-(BlockSyncConstants.BlockSyncAttachBlockAgeLimit + 100)));

        var response = await _networkService.GetBlockByHashAsync(HashHelper.ComputeFrom("PeerBlock"));
        var peerBlock = response.Payload;

        var chain = await _blockchainService.GetChainAsync();
        await _blockSyncService.SyncByAnnouncementAsync(chain, new SyncAnnouncementDto
        {
            SyncBlockHash = peerBlock.GetHash(),
            SyncBlockHeight = chain.LongestChainHeight + BlockSyncConstants.BlockSyncModeHeightOffset,
            BatchRequestBlockCount = 5
        });

        var block = await _blockchainService.GetBlockByHashAsync(peerBlock.GetHash());
        block.ShouldBeNull();
    }

    [Fact]
    public async Task SyncByAnnounce_Download_AttachQueueIsBusy()
    {
        _blockSyncStateProvider.SetEnqueueTime(OSConstants.BlockSyncAttachQueueName,
            TimestampHelper.GetUtcNow().AddMilliseconds(-(BlockSyncConstants.BlockSyncAttachBlockAgeLimit + 100)));

        var response = await _networkService.GetBlockByHashAsync(HashHelper.ComputeFrom("PeerBlock"));
        var peerBlock = response.Payload;

        var chain = await _blockchainService.GetChainAsync();
        var bestChainHash = chain.BestChainHash;
        var bestChainHeight = chain.BestChainHeight;

        await _blockSyncService.SyncByAnnouncementAsync(chain, new SyncAnnouncementDto
        {
            SyncBlockHash = peerBlock.GetHash(),
            SyncBlockHeight = chain.LongestChainHeight + BlockSyncConstants.BlockSyncModeHeightOffset + 1,
            BatchRequestBlockCount = 5
        });

        chain = await _blockchainService.GetChainAsync();
        chain.BestChainHash.ShouldBe(bestChainHash);
        chain.BestChainHeight.ShouldBe(bestChainHeight);
    }

    [Fact]
    public async Task SyncByAnnounce_RetryByNextSender()
    {
        _blockSyncStateProvider.SetEnqueueTime(OSConstants.BlockSyncAttachQueueName,
            TimestampHelper.GetUtcNow().AddMilliseconds(-(BlockSyncConstants.BlockSyncAttachBlockAgeLimit + 100)));

        var response = await _networkService.GetBlockByHashAsync(HashHelper.ComputeFrom("PeerBlock"));
        var peerBlock = response.Payload;

        _announcementCacheProvider.TryAddOrUpdateAnnouncementCache(peerBlock.GetHash(), peerBlock.Height,
            "NextPeerPubkey");

        var chain = await _blockchainService.GetChainAsync();
        await _blockSyncService.SyncByAnnouncementAsync(chain, new SyncAnnouncementDto
        {
            SyncBlockHash = peerBlock.GetHash(),
            SyncBlockHeight = chain.LongestChainHeight + BlockSyncConstants.BlockSyncModeHeightOffset,
            BatchRequestBlockCount = 5
        });

        _announcementCacheProvider.TryGetAnnouncementNextSender(peerBlock.GetHash(), out var nextPeerPubkey);
        nextPeerPubkey.ShouldBeNull();
    }

    [Fact]
    public async Task SyncByBlock_Success()
    {
        var response = await _networkService.GetBlockByHashAsync(HashHelper.ComputeFrom("PeerBlock"));
        var peerBlock = response.Payload;

        var block = await _blockchainService.GetBlockByHashAsync(peerBlock.GetHash());
        block.ShouldBeNull();

        var chain = await _blockchainService.GetChainAsync();
        await _blockSyncService.SyncByBlockAsync(chain, new SyncBlockDto
        {
            BlockWithTransactions = peerBlock,
            BatchRequestBlockCount = 5
        });

        block = await _blockchainService.GetBlockByHashAsync(peerBlock.GetHash());
        block.GetHash().ShouldBe(peerBlock.GetHash());

        chain = await _blockchainService.GetChainAsync();
        chain.BestChainHash.ShouldBe(peerBlock.GetHash());
        chain.BestChainHeight.ShouldBe(peerBlock.Height);
    }

    [Fact]
    public async Task SyncByBlock_MoreThenFetchLimit_Success()
    {
        var chain = await _blockchainService.GetChainAsync();

        var peerBlockHash = HashHelper.ComputeFrom("PeerBlock");
        var peerBlockHeight = chain.LongestChainHeight + BlockSyncConstants.BlockSyncModeHeightOffset + 1;

        var block = _osTestHelper.GenerateBlockWithTransactions(peerBlockHash, peerBlockHeight);

        await _blockSyncService.SyncByBlockAsync(chain,
            new SyncBlockDto
            {
                BlockWithTransactions = block, BatchRequestBlockCount = 10,
                SuggestedPeerPubkey = "SuggestedPeerPubkey"
            });

        var jobInfo = await _blockDownloadJobStore.GetFirstWaitingJobAsync();
        jobInfo.TargetBlockHeight.ShouldBe(block.Height);
        jobInfo.TargetBlockHash.ShouldBe(block.GetHash());
    }
}