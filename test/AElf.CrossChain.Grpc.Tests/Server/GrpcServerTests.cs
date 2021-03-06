using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AElf.CSharp.Core.Extension;
using AElf.Kernel;
using AElf.Kernel.SmartContract.Application;
using AElf.Standards.ACS7;
using Grpc.Core;
using Grpc.Core.Testing;
using Grpc.Core.Utils;
using Moq;
using Shouldly;
using Xunit;

namespace AElf.CrossChain.Grpc.Server;

public class GrpcServerTests : GrpcCrossChainServerTestBase
{
    private ISmartContractAddressService _smartContractAddressService;
    private BasicCrossChainRpc.BasicCrossChainRpcBase BasicCrossChainRpcBase;
    private ParentChainRpc.ParentChainRpcBase ParentChainGrpcServerBase;
    private SideChainRpc.SideChainRpcBase SideChainGrpcServerBase;

    public GrpcServerTests()
    {
        ParentChainGrpcServerBase = GetRequiredService<GrpcParentChainServerBase>();
        SideChainGrpcServerBase = GetRequiredService<GrpcSideChainServerBase>();
        BasicCrossChainRpcBase = GetRequiredService<GrpcBasicServerBase>();
        _smartContractAddressService = GetRequiredService<SmartContractAddressService>();
        // _smartContractAddressService.SetAddress(CrossChainSmartContractAddressNameProvider.Name,
        //     SampleAddress.AddressList[0]);
    }

    [Fact]
    public async Task RequestIndexingParentChain_MaximalResponse_Test()
    {
        var requestData = new CrossChainRequest
        {
            ChainId = ChainHelper.GetChainId(1),
            NextHeight = 10
        };

        var responseResults = new List<ParentChainBlockData>();
        var responseStream = MockServerStreamWriter(responseResults);
        var context = BuildServerCallContext();
        await ParentChainGrpcServerBase.RequestIndexingFromParentChain(requestData, responseStream, context);
        Assert.Equal(GrpcCrossChainConstants.MaximalIndexingCount, responseResults.Count);
        Assert.Equal(10, responseResults[0].Height);
    }

    [Fact]
    public async Task RequestIndexingParentChain_EmptyResponse_Test()
    {
        var requestData = new CrossChainRequest
        {
            ChainId = ChainHelper.GetChainId(1),
            NextHeight = 101
        };

        var responseResults = new List<ParentChainBlockData>();
        var responseStream = MockServerStreamWriter(responseResults);
        var context = BuildServerCallContext();
        await ParentChainGrpcServerBase.RequestIndexingFromParentChain(requestData, responseStream, context);
        Assert.Empty(responseResults);
    }

    [Fact]
    public async Task RequestIndexingParentChain_SpecificResponse_Test()
    {
        var requestData = new CrossChainRequest
        {
            ChainId = ChainHelper.GetChainId(1),
            NextHeight = 81
        };

        var responseResults = new List<ParentChainBlockData>();
        var responseStream = MockServerStreamWriter(responseResults);
        var context = BuildServerCallContext();
        await ParentChainGrpcServerBase.RequestIndexingFromParentChain(requestData, responseStream, context);
        Assert.Equal(20, responseResults.Count);
        Assert.Equal(81, responseResults.First().Height);
        Assert.Equal(100, responseResults.Last().Height);
    }

    [Fact]
    public async Task RequestIndexingSideChain_MaximalResponse_Test()
    {
        var requestData = new CrossChainRequest
        {
            ChainId = ChainHelper.GetChainId(1),
            NextHeight = 10
        };

        var responseResults = new List<SideChainBlockData>();
        var responseStream = MockServerStreamWriter(responseResults);
        var context = BuildServerCallContext();
        await SideChainGrpcServerBase.RequestIndexingFromSideChain(requestData, responseStream, context);
        Assert.Equal(GrpcCrossChainConstants.MaximalIndexingCount, responseResults.Count);
        Assert.Equal(10, responseResults[0].Height);
    }

    [Fact]
    public async Task RequestIndexingSideChain_EmptyResponse_Test()
    {
        var requestData = new CrossChainRequest
        {
            ChainId = ChainHelper.GetChainId(1),
            NextHeight = 101
        };

        var responseResults = new List<SideChainBlockData>();
        var responseStream = MockServerStreamWriter(responseResults);
        var context = BuildServerCallContext();
        await SideChainGrpcServerBase.RequestIndexingFromSideChain(requestData, responseStream, context);
        Assert.Empty(responseResults);
    }

    [Fact]
    public async Task CrossChainIndexingShake_Test()
    {
        var request = new HandShake
        {
            ListeningPort = 2100,
            ChainId = ChainHelper.GetChainId(1)
        };
        {
            // invalid peer format
            var context = BuildServerCallContext(null, "127.0.0.1");
            var indexingHandShakeReply = await BasicCrossChainRpcBase.CrossChainHandShake(request, context);
            indexingHandShakeReply.Status.ShouldBe(HandShakeReply.Types.HandShakeStatus.InvalidHandshakeRequest);
        }

        {
            var context = BuildServerCallContext();
            var indexingHandShakeReply = await BasicCrossChainRpcBase.CrossChainHandShake(request, context);

            Assert.NotNull(indexingHandShakeReply);
            Assert.True(indexingHandShakeReply.Status == HandShakeReply.Types.HandShakeStatus.Success);
        }
    }

    [Fact]
    public async Task RequestChainInitializationDataFromParentChain_Test()
    {
        var requestData = new SideChainInitializationRequest
        {
            ChainId = ChainHelper.GetChainId(1)
        };
        var context = BuildServerCallContext();
        var sideChainInitializationResponse =
            await ParentChainGrpcServerBase.RequestChainInitializationDataFromParentChain(requestData, context);
        Assert.Equal(1, sideChainInitializationResponse.CreationHeightOnParentChain);
    }

    private ServerCallContext BuildServerCallContext(Metadata metadata = null, string peer = null)
    {
        var meta = metadata ?? new Metadata();
        return TestServerCallContext.Create("mock", "127.0.0.1",
            TimestampHelper.GetUtcNow().AddHours(1).ToDateTime(), meta, CancellationToken.None,
            peer ?? "ipv4:127.0.0.1:2100", null, null, m => TaskUtils.CompletedTask, () => new WriteOptions(),
            writeOptions => { });
    }

    private IServerStreamWriter<T> MockServerStreamWriter<T>(IList<T> list)
    {
        var mockServerStreamWriter = new Mock<IServerStreamWriter<T>>();
        mockServerStreamWriter.Setup(w => w.WriteAsync(It.IsAny<T>())).Returns<T>(o =>
        {
            list.Add(o);
            return Task.CompletedTask;
        });
        return mockServerStreamWriter.Object;
    }
}