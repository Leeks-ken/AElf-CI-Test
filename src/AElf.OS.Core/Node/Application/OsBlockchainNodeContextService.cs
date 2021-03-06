using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AElf.Contracts.Genesis;
using AElf.Kernel.Node.Application;
using AElf.Kernel.Node.Infrastructure;
using AElf.Kernel.SmartContract;
using AElf.Kernel.SmartContract.Application;
using AElf.OS.Network.Infrastructure;
using AElf.OS.Node.Domain;
using AElf.Standards.ACS0;
using AElf.Types;
using Google.Protobuf;
using Microsoft.Extensions.Options;
using Volo.Abp.DependencyInjection;

namespace AElf.OS.Node.Application;

public class OsBlockchainNodeContextService : IOsBlockchainNodeContextService, ITransientDependency
{
    private readonly IBlockchainNodeContextService _blockchainNodeContextService;
    private readonly ContractOptions _contractOptions;
    private readonly IAElfNetworkServer _networkServer;
    private readonly IEnumerable<INodePlugin> _nodePlugins;
    private readonly ISmartContractAddressService _smartContractAddressService;

    public OsBlockchainNodeContextService(IBlockchainNodeContextService blockchainNodeContextService,
        IAElfNetworkServer networkServer, ISmartContractAddressService smartContractAddressService,
        IEnumerable<INodePlugin> nodePlugins, IOptionsSnapshot<ContractOptions> contractOptions)
    {
        _blockchainNodeContextService = blockchainNodeContextService;
        _networkServer = networkServer;
        _smartContractAddressService = smartContractAddressService;
        _nodePlugins = nodePlugins;
        _contractOptions = contractOptions.Value;
    }

    public async Task<OsBlockchainNodeContext> StartAsync(OsBlockchainNodeContextStartDto dto)
    {
        var transactions = new List<Transaction>();

        transactions.Add(GetTransactionForDeployment(dto.ZeroSmartContract,
            ZeroSmartContractAddressNameProvider.Name,
            dto.SmartContractRunnerCategory));

        transactions.AddRange(dto.InitializationSmartContracts
            .Select(p => GetTransactionForDeployment(p.Code, p.SystemSmartContractName,
                dto.SmartContractRunnerCategory,
                p.ContractInitializationMethodCallList)));

        if (dto.InitializationTransactions != null)
            transactions.AddRange(dto.InitializationTransactions);

        // Add transaction for initialization
        transactions.Add(GetTransactionForGenesisOwnerInitialization(dto));

        var blockchainNodeContextStartDto = new BlockchainNodeContextStartDto
        {
            ChainId = dto.ChainId,
            ZeroSmartContractType = dto.ZeroSmartContract,
            Transactions = transactions.ToArray()
        };

        var context = new OsBlockchainNodeContext
        {
            BlockchainNodeContext =
                await _blockchainNodeContextService.StartAsync(blockchainNodeContextStartDto),
            AElfNetworkServer = _networkServer
        };

        await _networkServer.StartAsync();

        foreach (var nodePlugin in _nodePlugins) await nodePlugin.StartAsync(dto.ChainId);

        return context;
    }

    public async Task StopAsync(OsBlockchainNodeContext blockchainNodeContext)
    {
        await _networkServer.StopAsync(false);

        await _blockchainNodeContextService.StopAsync(blockchainNodeContext.BlockchainNodeContext);

        foreach (var nodePlugin in _nodePlugins)
        {
            var _ = nodePlugin.ShutdownAsync();
        }
    }

    private Transaction GetTransactionForDeployment(Type contractType, Hash systemContractName,
        int category,
        List<ContractInitializationMethodCall> contractInitializationMethodCallList = null)
    {
        var dllPath = Directory.Exists(_contractOptions.GenesisContractDir)
            ? Path.Combine(_contractOptions.GenesisContractDir, $"{contractType.Assembly.GetName().Name}.dll")
            : contractType.Assembly.Location;
        var code = File.ReadAllBytes(dllPath);

        return GetTransactionForDeployment(code, systemContractName, category, contractInitializationMethodCallList);
    }

    private Transaction GetTransactionForDeployment(byte[] code, Hash systemContractName,
        int category,
        List<ContractInitializationMethodCall> contractInitializationMethodCallList = null)
    {
        var transactionMethodCallList = new SystemContractDeploymentInput.Types.SystemTransactionMethodCallList();
        if (contractInitializationMethodCallList != null)
            transactionMethodCallList.Value.Add(contractInitializationMethodCallList.Select(call =>
                new SystemContractDeploymentInput.Types.SystemTransactionMethodCall
                {
                    MethodName = call.MethodName,
                    Params = call.Params ?? ByteString.Empty
                }));
        var zeroAddress = _smartContractAddressService.GetZeroSmartContractAddress();

        return new Transaction
        {
            From = zeroAddress,
            To = zeroAddress,
            MethodName = nameof(ACS0Container.ACS0Stub.DeploySystemSmartContract),
            Params = new SystemContractDeploymentInput
            {
                Name = systemContractName,
                Category = category,
                Code = ByteString.CopyFrom(code),
                TransactionMethodCallList = transactionMethodCallList
            }.ToByteString()
        };
    }

    private Transaction GetTransactionForGenesisOwnerInitialization(OsBlockchainNodeContextStartDto dto)
    {
        var zeroAddress = _smartContractAddressService.GetZeroSmartContractAddress();
        return new Transaction
        {
            From = zeroAddress,
            To = zeroAddress,
            MethodName = nameof(BasicContractZeroContainer.BasicContractZeroStub.Initialize),
            Params = new InitializeInput
                { ContractDeploymentAuthorityRequired = dto.ContractDeploymentAuthorityRequired }.ToByteString()
        };
    }
}