using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AElf.Contracts.TestContract.BasicFunction;
using AElf.Contracts.TestContract.BasicFunctionWithParallel;
using AElf.Cryptography;
using AElf.Kernel;
using AElf.Kernel.Blockchain.Application;
using AElf.Kernel.Blockchain.Domain;
using AElf.Kernel.Miner;
using AElf.Kernel.Miner.Application;
using AElf.Kernel.SmartContractExecution.Application;
using AElf.Standards.ACS0;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;
using BetInput = AElf.Contracts.TestContract.BasicFunction.BetInput;

namespace AElf.Contract.TestContract;

public sealed class ContractBasicTests : TestContractTestBase
{
    private readonly IBlockAttachService _blockAttachService;
    private readonly IBlockchainService _blockchainService;
    private readonly IMiningService _miningService;
    private readonly ITransactionResultManager _transactionResultManager;

    public ContractBasicTests()
    {
        _blockchainService = GetRequiredService<IBlockchainService>();
        _miningService = GetRequiredService<IMiningService>();
        _blockAttachService = GetRequiredService<IBlockAttachService>();
        _transactionResultManager = GetRequiredService<ITransactionResultManager>();
        InitializeTestContracts();
    }

    [Fact]
    public async Task Initialize_MultiTimesContract_Test()
    {
        var transactionResult =
            (await TestBasicFunctionContractStub.InitialBasicFunctionContract.SendWithExceptionAsync(
                new InitialBasicContractInput
                {
                    ContractName = "Test initialize again",
                    MinValue = 1000,
                    MaxValue = 10000,
                    Manager = Accounts[0].Address
                })).TransactionResult;

        transactionResult.Status.ShouldBe(TransactionResultStatus.Failed);
        transactionResult.Error.Contains("Already initialized.").ShouldBeTrue();
    }

    [Fact]
    public async Task DeployContract_With_Two_Branch()
    {
        var blockHeader = await _blockchainService.GetBestChainLastBlockHeaderAsync();
        var startBlockHeight = blockHeader.Height;
        var startBlockHash = blockHeader.GetHash();

        Address contractAddress;
        //branch one
        {
            var t = await BasicContractZeroStub.DeploySmartContract.SendAsync(
                new ContractDeploymentInput
                {
                    Code = ByteString.CopyFrom(Codes.Single(kv => kv.Key.EndsWith("BasicFunctionWithParallel"))
                        .Value),
                    Category = KernelConstants.CodeCoverageRunnerCategory
                }
            );
            var transactionResult = t.TransactionResult;
            transactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
            contractAddress = Address.Parser.ParseFrom(transactionResult.ReturnValue);
            blockHeader = await _blockchainService.GetBestChainLastBlockHeaderAsync();

            var queryTwoUserWinMoneyInput = new QueryTwoUserWinMoneyInput
            {
                First = Accounts[0].Address,
                Second = Accounts[1].Address
            }.ToByteString();
            var queryTwoUserWinMoneyTransaction = CreateTransaction(DefaultSender, contractAddress,
                "QueryTwoUserWinMoney", queryTwoUserWinMoneyInput, blockHeader.Height, blockHeader.GetHash());
            var branchOneBlock = await ExecuteAsync(queryTwoUserWinMoneyTransaction, blockHeader.Height,
                blockHeader.GetHash());
            await _blockAttachService.AttachBlockAsync(branchOneBlock);

            var queryTwoUserWinMoneyTransactionResult =
                await _transactionResultManager.GetTransactionResultAsync(queryTwoUserWinMoneyTransaction
                    .GetHash(), branchOneBlock.Header.GetHash());
            queryTwoUserWinMoneyTransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
        }

        //branch two
        {
            var transaction = CreateTransaction(DefaultSender, BasicFunctionContractAddress,
                nameof(TestBasicFunctionContractStub.QueryWinMoney), new Empty().ToByteString(), startBlockHeight,
                startBlockHash);
            var branchTwoBlock = await ExecuteAsync(transaction, startBlockHeight, startBlockHash);
            await _blockAttachService.AttachBlockAsync(branchTwoBlock);

            transaction = CreateTransaction(DefaultSender, BasicFunctionContractAddress,
                nameof(TestBasicFunctionContractStub.QueryWinMoney), new Empty().ToByteString(),
                branchTwoBlock.Height,
                branchTwoBlock.GetHash());
            branchTwoBlock = await ExecuteAsync(transaction, branchTwoBlock.Height, branchTwoBlock.GetHash());
            await _blockAttachService.AttachBlockAsync(branchTwoBlock);

            transaction = CreateTransaction(DefaultSender, BasicFunctionContractAddress,
                nameof(TestBasicFunctionContractStub.QueryWinMoney), new Empty().ToByteString(),
                branchTwoBlock.Height,
                branchTwoBlock.GetHash());
            branchTwoBlock = await ExecuteAsync(transaction, branchTwoBlock.Height, branchTwoBlock.GetHash());
            await _blockAttachService.AttachBlockAsync(branchTwoBlock);

            var queryTwoUserWinMoneyInput = new QueryTwoUserWinMoneyInput
            {
                First = Accounts[0].Address,
                Second = Accounts[1].Address
            }.ToByteString();
            var queryTwoUserWinMoneyTransaction = CreateTransaction(DefaultSender, contractAddress,
                "QueryTwoUserWinMoney", queryTwoUserWinMoneyInput, branchTwoBlock.Height, branchTwoBlock.GetHash());

            branchTwoBlock = await ExecuteAsync(queryTwoUserWinMoneyTransaction, branchTwoBlock.Height,
                branchTwoBlock.GetHash());
            await _blockAttachService.AttachBlockAsync(branchTwoBlock);
            var queryTwoUserWinMoneyTransactionResult =
                await _transactionResultManager.GetTransactionResultAsync(queryTwoUserWinMoneyTransaction.GetHash(),
                    branchTwoBlock.Header.GetDisambiguatingHash());
            queryTwoUserWinMoneyTransactionResult.Status.ShouldBe(TransactionResultStatus.Failed);
            queryTwoUserWinMoneyTransactionResult.Error.ShouldContain("Invalid contract address");
        }
    }

    [Fact]
    public async Task UpdateContract_WithOwner_Test()
    {
        //update with same code
        {
            var transactionResult = (await BasicContractZeroStub.UpdateSmartContract.SendWithExceptionAsync(
                new ContractUpdateInput
                {
                    Address = BasicFunctionContractAddress,
                    Code = ByteString.CopyFrom(Codes.Single(kv => kv.Key.EndsWith("BasicFunction")).Value)
                }
            )).TransactionResult;

            transactionResult.Status.ShouldBe(TransactionResultStatus.Failed);
            transactionResult.Error.Contains("Code is not changed").ShouldBeTrue();
        }

        //different code
        {
            var transactionResult = (await BasicContractZeroStub.UpdateSmartContract.SendAsync(
                new ContractUpdateInput
                {
                    Address = BasicFunctionContractAddress,
                    Code = ByteString.CopyFrom(Codes.Single(kv => kv.Key.EndsWith("BasicUpdate")).Value)
                }
            )).TransactionResult;

            transactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

            var basic11ContractStub = GetTestBasicUpdateContractStub(DefaultSenderKeyPair);
            //execute new action method
            var transactionResult1 = (await basic11ContractStub.UpdateStopBet.SendAsync(
                new Empty())).TransactionResult;
            transactionResult1.Status.ShouldBe(TransactionResultStatus.Mined);

            //call new view method
            var result = (await basic11ContractStub.QueryBetStatus.CallAsync(
                new Empty())).BoolValue;
            result.ShouldBeTrue();
        }
    }

    [Fact]
    public async Task UpdateContract_And_Call_Old_Method_Test()
    {
        var transactionResult = (await BasicContractZeroStub.UpdateSmartContract.SendAsync(
            new ContractUpdateInput
            {
                Address = BasicFunctionContractAddress,
                Code = ByteString.CopyFrom(Codes.Single(kv => kv.Key.EndsWith("BasicUpdate")).Value)
            }
        )).TransactionResult;

        transactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

        //execute new action method
        transactionResult = (await TestBasicFunctionContractStub.UserPlayBet.SendAsync(
            new BetInput
            {
                Int64Value = 100
            })).TransactionResult;
        transactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

        //check result
        var winData = (await TestBasicFunctionContractStub.QueryUserWinMoney.CallAsync(
            DefaultSender)).Int64Value;
        if (winData > 0)
        {
            winData.ShouldBeGreaterThanOrEqualTo(100);
            return;
        }

        var loseData = (await TestBasicFunctionContractStub.QueryUserLoseMoney.CallAsync(
            DefaultSender)).Int64Value;
        (winData + loseData).ShouldBe(100);

        //execute again
        transactionResult = (await TestBasicFunctionContractStub.UserPlayBet.SendAsync(
            new BetInput
            {
                Int64Value = 100
            })).TransactionResult;
        transactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
        //check result
        loseData = (await TestBasicFunctionContractStub.QueryUserLoseMoney.CallAsync(
            DefaultSender)).Int64Value;
        (winData + loseData).ShouldBe(200);
    }

    [Fact]
    public async Task UpdateContract_Attach_After_ReadOnly_Transaction()
    {
        var chain = await _blockchainService.GetChainAsync();
        var blockHeight = chain.BestChainHeight;
        var blockHash = chain.BestChainHash;

        var input = new ContractUpdateInput
        {
            Address = BasicFunctionContractAddress,
            Code = ByteString.CopyFrom(Codes.Single(kv => kv.Key.EndsWith("BasicUpdate")).Value)
        }.ToByteString();
        var transaction = CreateTransaction(DefaultSender, ContractZeroAddress,
            nameof(BasicContractZeroStub.UpdateSmartContract), input, blockHeight, blockHash);
        var block = await ExecuteAsync(transaction, blockHeight, blockHash);
        var transactionResult =
            await _transactionResultManager.GetTransactionResultAsync(transaction.GetHash(),
                block.Header.GetDisambiguatingHash());

        var basicFunctionContractStub = GetTestBasicFunctionContractStub(DefaultSenderKeyPair);
        await basicFunctionContractStub.QueryWinMoney.CallAsync(new Empty());

        await _blockAttachService.AttachBlockAsync(block);

        var basic11ContractStub = GetTestBasicUpdateContractStub(DefaultSenderKeyPair);
//            //execute new action method
        var transactionResult1 = (await basic11ContractStub.UpdateStopBet.SendAsync(
            new Empty())).TransactionResult;
        transactionResult1.Status.ShouldBe(TransactionResultStatus.Mined);

        //call new view method
        var result = (await basic11ContractStub.QueryBetStatus.CallAsync(
            new Empty())).BoolValue;
        result.ShouldBeTrue();

        await _blockchainService.SetIrreversibleBlockAsync(chain, block.Height, block.GetHash());
    }

    [Fact]
    public async Task UpdateContract_With_Two_Branch()
    {
        var blockHeader = await _blockchainService.GetBestChainLastBlockHeaderAsync();
        var startBlockHeight = blockHeader.Height;
        var startBlockHash = blockHeader.GetHash();

        var transactionResult = (await BasicContractZeroStub.UpdateSmartContract.SendAsync(
            new ContractUpdateInput
            {
                Address = BasicFunctionContractAddress,
                Code = ByteString.CopyFrom(Codes.Single(kv => kv.Key.EndsWith("BasicUpdate")).Value)
            }
        )).TransactionResult;
        transactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

        var basic11ContractStub = GetTestBasicUpdateContractStub(DefaultSenderKeyPair);
//            //execute new action method
        var transactionResult1 = (await basic11ContractStub.UpdateStopBet.SendAsync(
            new Empty())).TransactionResult;
        transactionResult1.Status.ShouldBe(TransactionResultStatus.Mined);

        var transaction = CreateTransaction(DefaultSender, BasicFunctionContractAddress,
            nameof(TestBasicFunctionContractStub.QueryWinMoney), new Empty().ToByteString(), startBlockHeight,
            startBlockHash);
        var block = await ExecuteAsync(transaction, startBlockHeight, startBlockHash);
        await _blockAttachService.AttachBlockAsync(block);

        transaction = CreateTransaction(DefaultSender, BasicFunctionContractAddress,
            nameof(TestBasicFunctionContractStub.QueryWinMoney), new Empty().ToByteString(), block.Height,
            block.GetHash());
        block = await ExecuteAsync(transaction, block.Height, block.GetHash());
        await _blockAttachService.AttachBlockAsync(block);

        var input = new Empty().ToByteString();
        var failedTransaction = CreateTransaction(DefaultSender, BasicFunctionContractAddress,
            nameof(basic11ContractStub.UpdateStopBet), input, block.Height, block.GetHash());
        block = await ExecuteAsync(failedTransaction, block.Height, block.GetHash());
        await _blockAttachService.AttachBlockAsync(block);

        transactionResult =
            await _transactionResultManager.GetTransactionResultAsync(failedTransaction.GetHash(),
                block.Header.GetDisambiguatingHash());
        transactionResult.Status.ShouldBe(TransactionResultStatus.Failed);
        transactionResult.Error.ShouldContain("Failed to find handler for UpdateStopBet.");

        input = new ContractUpdateInput
        {
            Address = BasicFunctionContractAddress,
            Code = ByteString.CopyFrom(Codes.Single(kv => kv.Key.EndsWith("BasicFunction")).Value)
        }.ToByteString();
        var updateTransaction = CreateTransaction(DefaultSender, ContractZeroAddress,
            nameof(BasicContractZeroStub.UpdateSmartContract), input, block.Height, block.GetHash());
        var updateBlock = await ExecuteAsync(updateTransaction, block.Height, block.GetHash());
        await _blockAttachService.AttachBlockAsync(updateBlock);
//            
        transactionResult =
            await _transactionResultManager.GetTransactionResultAsync(updateTransaction.GetHash(),
                updateBlock.Header.GetDisambiguatingHash());
        transactionResult.Status.ShouldBe(TransactionResultStatus.Failed);
        transactionResult.Error.Contains("Code is not changed").ShouldBeTrue();

        input = new ContractUpdateInput
        {
            Address = BasicFunctionContractAddress,
            Code = ByteString.CopyFrom(Codes.Single(kv => kv.Key.EndsWith("BasicUpdate")).Value)
        }.ToByteString();
        updateTransaction = CreateTransaction(DefaultSender, ContractZeroAddress,
            nameof(BasicContractZeroStub.UpdateSmartContract), input, updateBlock.Height, updateBlock.GetHash());
        updateBlock = await ExecuteAsync(updateTransaction, updateBlock.Height, updateBlock.GetHash());
        await _blockAttachService.AttachBlockAsync(updateBlock);

        transactionResult =
            await _transactionResultManager.GetTransactionResultAsync(updateTransaction.GetHash(),
                updateBlock.Header.GetDisambiguatingHash());
        transactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

        basic11ContractStub = GetTestBasicUpdateContractStub(DefaultSenderKeyPair);
        //execute new action method
        transactionResult = (await basic11ContractStub.UpdateStopBet.SendAsync(
            new Empty())).TransactionResult;
        transactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

        //call new view method
        var result = (await basic11ContractStub.QueryBetStatus.CallAsync(
            new Empty())).BoolValue;
        result.ShouldBeTrue();
    }

    private Transaction CreateTransaction(Address from, Address to, string methodName,
        ByteString parameters, long blockHeight, Hash blockHash)
    {
        var transaction = new Transaction
        {
            From = from,
            To = to,
            MethodName = methodName,
            Params = parameters,
            RefBlockNumber = blockHeight,
            RefBlockPrefix = BlockHelper.GetRefBlockPrefix(blockHash)
        };
        var signature = CryptoHelper.SignWithPrivateKey(DefaultSenderKeyPair.PrivateKey,
            transaction.GetHash().Value.ToByteArray());
        transaction.Signature = ByteString.CopyFrom(signature);
        return transaction;
    }

    private async Task<Block> ExecuteAsync(Transaction transaction, long previousBlockHeight,
        Hash previousBlockHash)
    {
        var transactionList = new List<Transaction>();
        if (transaction != null) transactionList.Add(transaction);
        var block = (await _miningService.MineAsync(
            new RequestMiningDto
            {
                PreviousBlockHash = previousBlockHash, PreviousBlockHeight = previousBlockHeight,
                BlockExecutionTime = TimestampHelper.DurationFromMilliseconds(int.MaxValue),
                TransactionCountLimit = int.MaxValue
            },
            transactionList,
            DateTime.UtcNow.ToTimestamp())).Block;

        if (transaction != null)
            await _blockchainService.AddTransactionsAsync(new List<Transaction> { transaction });
        await _blockchainService.AddBlockAsync(block);
        return block;
    }
}