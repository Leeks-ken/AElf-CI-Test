using AElf.Sdk.CSharp;
using AElf.Standards.ACS1;
using AElf.Types;
using Google.Protobuf.WellKnownTypes;

namespace AElf.Contracts.Association;

public partial class AssociationContract
{
    public override Empty SetMethodFee(MethodFees input)
    {
        foreach (var methodFee in input.Fees) AssertValidToken(methodFee.Symbol, methodFee.BasicFee);
        RequiredMethodFeeControllerSet();

        Assert(Context.Sender == State.MethodFeeController.Value.OwnerAddress, "Unauthorized to set method fee.");
        State.TransactionFees[input.MethodName] = input;

        return new Empty();
    }

    public override Empty ChangeMethodFeeController(AuthorityInfo input)
    {
        RequiredMethodFeeControllerSet();
        AssertSenderAddressWith(State.MethodFeeController.Value.OwnerAddress);
        var organizationExist = CheckOrganizationExist(input);
        Assert(organizationExist, "Invalid authority input.");

        State.MethodFeeController.Value = input;
        return new Empty();
    }

    #region Views

    public override MethodFees GetMethodFee(StringValue input)
    {
        return State.TransactionFees[input.Value];
    }

    public override AuthorityInfo GetMethodFeeController(Empty input)
    {
        RequiredMethodFeeControllerSet();
        return State.MethodFeeController.Value;
    }

    #endregion

    #region private methods

    private void RequiredMethodFeeControllerSet()
    {
        if (State.MethodFeeController.Value != null) return;
        if (State.ParliamentContract.Value == null)
            State.ParliamentContract.Value =
                Context.GetContractAddressByName(SmartContractConstants.ParliamentContractSystemName);

        var defaultAuthority = new AuthorityInfo
        {
            OwnerAddress = State.ParliamentContract.GetDefaultOrganizationAddress.Call(new Empty()),
            ContractAddress = State.ParliamentContract.Value
        };

        State.MethodFeeController.Value = defaultAuthority;
    }

    private void AssertSenderAddressWith(Address address)
    {
        Assert(Context.Sender == address, "Unauthorized behavior.");
    }

    private bool CheckOrganizationExist(AuthorityInfo authorityInfo)
    {
        return Context.Call<BoolValue>(authorityInfo.ContractAddress,
            nameof(ValidateOrganizationExist), authorityInfo.OwnerAddress).Value;
    }

    private void AssertValidToken(string symbol, long amount)
    {
        Assert(amount >= 0, "Invalid amount.");
        if (State.TokenContract.Value == null)
            State.TokenContract.Value =
                Context.GetContractAddressByName(SmartContractConstants.TokenContractSystemName);

        Assert(State.TokenContract.IsTokenAvailableForMethodFee.Call(new StringValue { Value = symbol }).Value,
            $"Token {symbol} cannot set as method fee.");
    }

    #endregion
}