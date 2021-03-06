using Microsoft.Extensions.Options;

namespace AElf.Kernel.Blockchain.Infrastructure;

public interface IStaticChainInformationProvider
{
    int ChainId { get; }
    Address ZeroSmartContractAddress { get; }
    Address GetZeroSmartContractAddress(int chainId);
}

public class StaticChainInformationProvider : IStaticChainInformationProvider, ISingletonDependency
{
    public StaticChainInformationProvider(IOptionsSnapshot<ChainOptions> chainOptions)
    {
        ChainId = chainOptions.Value.ChainId;
        ZeroSmartContractAddress = BuildZeroContractAddress(ChainId);
    }

    public int ChainId { get; }
    public Address ZeroSmartContractAddress { get; }

    public Address GetZeroSmartContractAddress(int chainId)
    {
        return BuildZeroContractAddress(chainId);
    }

    private static Address BuildZeroContractAddress(int chainId)
    {
        var hash = HashHelper.ConcatAndCompute(HashHelper.ComputeFrom(chainId), HashHelper.ComputeFrom(0L));
        return Address.FromBytes(hash.ToByteArray());
    }
}