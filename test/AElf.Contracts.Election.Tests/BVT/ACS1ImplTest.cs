using System.Threading.Tasks;
using AElf.Contracts.Parliament;
using AElf.CSharp.Core.Extension;
using AElf.Kernel;
using AElf.Standards.ACS1;
using AElf.Standards.ACS3;
using AElf.Types;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Shouldly;
using Xunit;

namespace AElf.Contracts.Election;

public partial class ElectionContractTests
{
    [Fact]
    public async Task ChangeMethodFeeController_With_Invalid_Authority_Test()
    {
        var newController = new AuthorityInfo
        {
            OwnerAddress = ElectionContractAddress,
            ContractAddress = ParliamentContractAddress
        };
        var methodFeeController = await ElectionContractStub.GetMethodFeeController.CallAsync(new Empty());
        var proposalCreationMethodName = nameof(ElectionContractStub.ChangeMethodFeeController);
        var proposalId = await CreateProposalAsync(ElectionContractAddress,
            methodFeeController.OwnerAddress, proposalCreationMethodName, newController);
        await ApproveWithMinersAsync(proposalId);
        var releaseResult = await ParliamentContractStub.Release.SendAsync(proposalId);
        releaseResult.TransactionResult.Status.ShouldBe(TransactionResultStatus.Failed);
        releaseResult.TransactionResult.Error.ShouldContain("Invalid authority input");
    }

    [Fact]
    public async Task ChangeMethodFeeController_Test()
    {
        var createOrganizationResult =
            await ParliamentContractStub.CreateOrganization.SendAsync(
                new CreateOrganizationInput
                {
                    ProposalReleaseThreshold = new ProposalReleaseThreshold
                    {
                        MinimalApprovalThreshold = 1000,
                        MinimalVoteThreshold = 1000
                    }
                });
        var organizationAddress = Address.Parser.ParseFrom(createOrganizationResult.TransactionResult.ReturnValue);

        var methodFeeController = await ElectionContractStub.GetMethodFeeController.CallAsync(new Empty());
        var defaultOrganization = await ParliamentContractStub.GetDefaultOrganizationAddress.CallAsync(new Empty());
        methodFeeController.OwnerAddress.ShouldBe(defaultOrganization);

        const string proposalCreationMethodName = nameof(ElectionContractStub.ChangeMethodFeeController);
        var proposalId = await CreateProposalAsync(ElectionContractAddress,
            methodFeeController.OwnerAddress, proposalCreationMethodName, new AuthorityInfo
            {
                OwnerAddress = organizationAddress,
                ContractAddress = ParliamentContractAddress
            });
        await ApproveWithMinersAsync(proposalId);
        var releaseResult = await ParliamentContractStub.Release.SendAsync(proposalId);
        releaseResult.TransactionResult.Error.ShouldBeNullOrEmpty();
        releaseResult.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);

        var newMethodFeeController = await ElectionContractStub.GetMethodFeeController.CallAsync(new Empty());
        newMethodFeeController.OwnerAddress.ShouldBe(organizationAddress);
    }

    [Fact]
    public async Task ChangeMethodFeeController_WithoutAuth_Test()
    {
        var createOrganizationResult =
            await ParliamentContractStub.CreateOrganization.SendAsync(
                new CreateOrganizationInput
                {
                    ProposalReleaseThreshold = new ProposalReleaseThreshold
                    {
                        MinimalApprovalThreshold = 1000,
                        MinimalVoteThreshold = 1000
                    }
                });
        var organizationAddress = Address.Parser.ParseFrom(createOrganizationResult.TransactionResult.ReturnValue);
        var result = await ElectionContractStub.ChangeMethodFeeController.SendAsync(new AuthorityInfo
        {
            OwnerAddress = organizationAddress,
            ContractAddress = ParliamentContractAddress
        });

        result.TransactionResult.Status.ShouldBe(TransactionResultStatus.Failed);
        result.TransactionResult.Error.Contains("Unauthorized behavior.").ShouldBeTrue();
    }

    [Fact]
    public async Task SetMethodFee_With_Invalid_Input_Test()
    {
        // Invalid amount
        {
            var setMethodFeeRet = await ElectionContractStub.SetMethodFee.SendAsync(new MethodFees
            {
                MethodName = "Test",
                Fees =
                {
                    new MethodFee
                    {
                        Symbol = "NOTEXIST",
                        BasicFee = -111
                    }
                }
            });
            setMethodFeeRet.TransactionResult.Status.ShouldBe(TransactionResultStatus.Failed);
            setMethodFeeRet.TransactionResult.Error.ShouldContain("Invalid amount.");
        }

        // token does not exist
        {
            var setMethodFeeRet = await ElectionContractStub.SetMethodFee.SendAsync(new MethodFees
            {
                MethodName = "Test",
                Fees =
                {
                    new MethodFee
                    {
                        Symbol = "NOTEXIST",
                        BasicFee = 111
                    }
                }
            });
            setMethodFeeRet.TransactionResult.Status.ShouldBe(TransactionResultStatus.Failed);
            setMethodFeeRet.TransactionResult.Error.ShouldContain("Token is not found.");
        }
    }

    [Fact]
    public async Task SetMethodFee_Without_Authority_Test()
    {
        var tokenSymbol = "ELF";
        var methodName = "Test";
        var basicFee = 111;
        var setMethodFeeRet = await ElectionContractStub.SetMethodFee.SendAsync(new MethodFees
        {
            MethodName = methodName,
            Fees =
            {
                new MethodFee
                {
                    Symbol = tokenSymbol,
                    BasicFee = basicFee
                }
            }
        });
        setMethodFeeRet.TransactionResult.Status.ShouldBe(TransactionResultStatus.Failed);
        setMethodFeeRet.TransactionResult.Error.ShouldContain("Unauthorized to set method fee.");
    }

    [Fact]
    public async Task SetMethodFee_Success_Test()
    {
        var tokenSymbol = "ELF";
        var methodName = "Test";
        var basicFee = 111;
        var methodFeeController = await ElectionContractStub.GetMethodFeeController.CallAsync(new Empty());
        const string proposalCreationMethodName = nameof(ElectionContractStub.SetMethodFee);
        var proposalId = await CreateProposalAsync(ElectionContractAddress,
            methodFeeController.OwnerAddress, proposalCreationMethodName, new MethodFees
            {
                MethodName = methodName,
                Fees =
                {
                    new MethodFee
                    {
                        Symbol = tokenSymbol,
                        BasicFee = basicFee
                    }
                }
            });
        await ApproveWithMinersAsync(proposalId);
        var releaseResult = await ParliamentContractStub.Release.SendAsync(proposalId);
        releaseResult.TransactionResult.Error.ShouldBeNullOrEmpty();
        releaseResult.TransactionResult.Status.ShouldBe(TransactionResultStatus.Mined);
        var getMethodFee = await ElectionContractStub.GetMethodFee.CallAsync(new StringValue
        {
            Value = methodName
        });
        getMethodFee.Fees.Count.ShouldBe(1);
        getMethodFee.Fees[0].Symbol.ShouldBe(tokenSymbol);
        getMethodFee.Fees[0].BasicFee.ShouldBe(basicFee);
    }

    private async Task<Hash> CreateProposalAsync(Address contractAddress, Address organizationAddress,
        string methodName, IMessage input)
    {
        var proposal = new CreateProposalInput
        {
            OrganizationAddress = organizationAddress,
            ContractMethodName = methodName,
            ExpiredTime = TimestampHelper.GetUtcNow().AddHours(1),
            Params = input.ToByteString(),
            ToAddress = contractAddress
        };

        var createResult = await ParliamentContractStub.CreateProposal.SendAsync(proposal);
        var proposalId = createResult.Output;

        return proposalId;
    }

    private async Task ApproveWithMinersAsync(Hash proposalId)
    {
        foreach (var bp in InitialCoreDataCenterKeyPairs)
        {
            var tester = GetParliamentContractTester(bp);
            var approveResult = await tester.Approve.SendAsync(proposalId);
            approveResult.TransactionResult.Error.ShouldBeNullOrEmpty();
        }
    }
}