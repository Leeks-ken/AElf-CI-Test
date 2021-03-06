using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.CrossChain.Application;
using AElf.CrossChain.Communication;
using AElf.Kernel.Node.Infrastructure;
using AElf.Standards.ACS7;

namespace AElf.CrossChain;

public interface IChainInitializationDataPlugin
{
    Task<ChainInitializationData> GetChainInitializationDataAsync(int chainId);
}

public class CrossChainPlugin : IChainInitializationDataPlugin, INodePlugin
{
    private readonly List<ICrossChainCommunicationPlugin> _crossChainCommunicationPlugins;
    private readonly ICrossChainRequestService _crossChainRequestService;

    public CrossChainPlugin(ICrossChainRequestService crossChainRequestService,
        IEnumerable<ICrossChainCommunicationPlugin> crossChainCommunicationPlugins)
    {
        _crossChainRequestService = crossChainRequestService;
        _crossChainCommunicationPlugins = crossChainCommunicationPlugins.ToList();
    }

    public async Task<ChainInitializationData> GetChainInitializationDataAsync(int chainId)
    {
        var chainInitializationInformation =
            await _crossChainRequestService.RequestChainInitializationDataAsync(chainId);
        return chainInitializationInformation;
    }

    public async Task StartAsync(int chainId)
    {
        foreach (var plugin in _crossChainCommunicationPlugins) await plugin.StartAsync(chainId);
    }

    public async Task ShutdownAsync()
    {
        foreach (var plugin in _crossChainCommunicationPlugins) await plugin.ShutdownAsync();
    }
}