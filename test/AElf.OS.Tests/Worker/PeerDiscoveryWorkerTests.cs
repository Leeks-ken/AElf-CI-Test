using System.Threading.Tasks;
using AElf.Kernel;
using AElf.OS.Network;
using AElf.OS.Network.Application;
using AElf.OS.Network.Helpers;
using AElf.OS.Network.Infrastructure;
using AElf.OS.Network.Protocol.Types;
using AElf.OS.Network.Types;
using Google.Protobuf;
using Moq;
using Shouldly;
using Xunit;

namespace AElf.OS.Worker;

public class PeerDiscoveryWorkerTests : PeerDiscoveryWorkerTestBase
{
    private readonly IPeerDiscoveryJobProcessor _peerDiscoveryJobProcessor;
    private readonly IPeerDiscoveryService _peerDiscoveryService;
    private readonly PeerDiscoveryWorker _peerDiscoveryWorker;
    private readonly IPeerPool _peerPool;
    private readonly IReconnectionService _reconnectionService;

    public PeerDiscoveryWorkerTests()
    {
        _peerDiscoveryWorker = GetRequiredService<PeerDiscoveryWorker>();
        _peerPool = GetRequiredService<IPeerPool>();
        _peerDiscoveryService = GetRequiredService<IPeerDiscoveryService>();
        _reconnectionService = GetRequiredService<IReconnectionService>();
        _peerDiscoveryJobProcessor = GetRequiredService<IPeerDiscoveryJobProcessor>();
    }

    [Fact]
    public async Task ProcessPeerDiscoveryJob_Test()
    {
        var node1 = new NodeInfo
        {
            Endpoint = "192.168.100.1:8001",
            Pubkey = ByteString.CopyFromUtf8("node1")
        };
        await _peerDiscoveryService.AddNodeAsync(node1);

        await RunDiscoveryWorkerAsync();

        var endpointString = "192.168.100.100:8003";
        var nodeList = await _peerDiscoveryService.GetNodesAsync(10);
        nodeList.Nodes.Count.ShouldBe(1);
        nodeList.Nodes[0].Endpoint.ShouldBe(endpointString);
        nodeList.Nodes[0].Pubkey.ShouldBe(ByteString.CopyFromUtf8(endpointString));

        AElfPeerEndpointHelper.TryParse(endpointString, out var aelEndpoint);
        var peer = _peerPool.FindPeerByEndpoint(aelEndpoint);
        peer.ShouldNotBeNull();
    }

    [Fact]
    public async Task ProcessPeerDiscoveryJob_PeerPoolIsFull_Test()
    {
        var endpoint = new AElfPeerEndpoint("192.168.100.1", 8000);
        var peer = new Mock<IPeer>();
        peer.Setup(p => p.IsReady).Returns(true);
        peer.Setup(p => p.Info).Returns(new PeerConnectionInfo
            { Pubkey = endpoint.ToString(), ConnectionTime = TimestampHelper.GetUtcNow() });
        peer.Setup(p => p.RemoteEndpoint).Returns(endpoint);

        _peerPool.TryAddPeer(peer.Object);

        await RunDiscoveryWorkerAsync();

        var endpointString = "192.168.100.100:8003";
        var nodeList = await _peerDiscoveryService.GetNodesAsync(10);
        nodeList.Nodes.Count.ShouldBe(1);
        nodeList.Nodes[0].Endpoint.ShouldBe(endpointString);
        nodeList.Nodes[0].Pubkey.ShouldBe(ByteString.CopyFromUtf8(endpointString));

        AElfPeerEndpointHelper.TryParse(endpointString, out var aelEndpoint);
        var result = _peerPool.FindPeerByEndpoint(aelEndpoint);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ProcessPeerDiscoveryJob_ReconnectingPeer_Test()
    {
        var endpointString = "192.168.100.100:8003";
        _reconnectionService.SchedulePeerForReconnection(endpointString);

        await RunDiscoveryWorkerAsync();

        var nodeList = await _peerDiscoveryService.GetNodesAsync(10);
        nodeList.Nodes.Count.ShouldBe(1);
        nodeList.Nodes[0].Endpoint.ShouldBe(endpointString);
        nodeList.Nodes[0].Pubkey.ShouldBe(ByteString.CopyFromUtf8(endpointString));

        AElfPeerEndpointHelper.TryParse(endpointString, out var aelEndpoint);
        var result = _peerPool.FindPeerByEndpoint(aelEndpoint);
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ProcessPeerDiscoveryJob_PeerIsInPeerPool_Test()
    {
        var pubkey = ByteString.CopyFromUtf8("SamePubkey");

        var mockPeer = new Mock<IPeer>();
        mockPeer.Setup(p => p.Info).Returns(new PeerConnectionInfo
            { Pubkey = pubkey.ToHex(), ConnectionTime = TimestampHelper.GetUtcNow() });
        mockPeer.Setup(p => p.IsReady).Returns(true);
        mockPeer.Setup(p => p.RemoteEndpoint).Returns(new AElfPeerEndpoint("192.168.99.99", 8801));
        mockPeer.Setup(m => m.GetNodesAsync(It.IsAny<int>()))
            .Returns(Task.FromResult(new NodeList()));

        _peerPool.TryAddPeer(mockPeer.Object);

        var node = new NodeInfo
        {
            Endpoint = "192.168.99.100:8002",
            Pubkey = pubkey
        };
        await _peerDiscoveryService.AddNodeAsync(node);

        await RunDiscoveryWorkerAsync();

        var peer = _peerPool.FindPeerByPublicKey(node.Pubkey.ToHex());
        peer.RemoteEndpoint.Host.ShouldBe("192.168.99.99");
        peer.RemoteEndpoint.Port.ShouldBe(8801);
    }

    private async Task RunDiscoveryWorkerAsync()
    {
        await _peerDiscoveryWorker.ProcessPeerDiscoveryJobAsync();
        await _peerDiscoveryJobProcessor.CompleteAsync();
        await _peerDiscoveryWorker.ProcessPeerDiscoveryJobAsync();
    }
}