using System;
using System.Threading.Tasks;
using AElf.Kernel.Blockchain.Application;
using AElf.Kernel.Miner.Application;
using Google.Protobuf;
using Shouldly;
using Xunit;

namespace AElf.Kernel;

public class BlockTemplateMinerServiceTests : KernelWithChainTestBase
{
    private readonly IBlockchainService _chainService;
    private readonly IBlockTemplateMinerService _minerService;

    public BlockTemplateMinerServiceTests()
    {
        _chainService = GetRequiredService<IBlockchainService>();
        _minerService = GetRequiredService<IBlockTemplateMinerService>();
    }

    [Fact]
    public async Task MinAsync_Success_Test()
    {
        var chain = await _chainService.GetChainAsync();
        var hash = chain.BestChainHash;
        var height = chain.BestChainHeight;

        var blockHeader = await _minerService.CreateTemplateCacheAsync(hash, height, TimestampHelper.GetUtcNow(),
            TimestampHelper.DurationFromMinutes(1));

        var byteString = blockHeader.ToByteString();

        var bytes = byteString.ToByteArray();


        //Send Bytes to Client

        #region Client Side

        //Client side, you can search nonce and replace it

        var nonce = BitConverter.GetBytes(long.MaxValue - 1);

        var start = bytes.Find(nonce);

        start.ShouldBeGreaterThan(0);

        for (var i = 0; i < nonce.Length; i++) bytes[start + i] = 9; //change nonce

        bytes.Find(nonce).ShouldBe(-1);

        var newHeader = BlockHeader.Parser.ParseFrom(ByteString.CopyFrom(bytes));

        //Test mining method
        newHeader.GetHash().ShouldBe(HashHelper.ComputeFrom(newHeader.ToByteArray()));
        newHeader.GetHash().ShouldBe(HashHelper.ComputeFrom(bytes));


        //Start mining 

        var r = new Random();

        while (HashHelper.ComputeFrom(bytes).Value[0] != 0)
            //find first hash byte is 0

            for (var i = 0; i < nonce.Length; i++)
                bytes[start + i] = (byte)r.Next(); //change nonce, very slow, just for demo

        #endregion

        //Send bytes to Server

        newHeader = BlockHeader.Parser.ParseFrom(ByteString.CopyFrom(bytes));

        var newHeaderHash = newHeader.GetHash();

        newHeaderHash.Value[0].ShouldBe((byte)0); // first byte should be zero

        var block = await _minerService.ChangeTemplateCacheBlockHeaderAndClearCacheAsync(newHeader);

        block.GetHash().ShouldBe(newHeader.GetHash()); // check new block's header
        block.Header.Signature.ShouldBeEmpty(); // check signature
    }
}