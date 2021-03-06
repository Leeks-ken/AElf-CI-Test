using AElf.Contracts.Association;
using AElf.Contracts.MultiToken;
using AElf.Contracts.Parliament;
using AElf.Standards.ACS0;
using AElf.Standards.ACS11;

namespace AElf.Contracts.CrossChain;

public partial class CrossChainContractState
{
    internal TokenContractContainer.TokenContractReferenceState TokenContract { get; set; }
    internal AssociationContractContainer.AssociationContractReferenceState AssociationContract { get; set; }
    internal ACS0Container.ACS0ReferenceState GenesisContract { get; set; }
    internal ParliamentContractContainer.ParliamentContractReferenceState ParliamentContract { get; set; }

    internal CrossChainInteractionContractContainer.CrossChainInteractionContractReferenceState
        CrossChainInteractionContract { get; set; }
}