using AElf.Contracts.MultiToken;
using AElf.Contracts.Treasury;
using AElf.Standards.ACS0;

namespace AElf.Contracts.TestContract.MethodCallThreshold;

public partial class MethodCallThresholdContractState
{
    internal TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }
    internal TreasuryContractContainer.TreasuryContractReferenceState TreasuryContract { get; set; }
    internal ACS0Container.ACS0ReferenceState Acs0Contract { get; set; }
}