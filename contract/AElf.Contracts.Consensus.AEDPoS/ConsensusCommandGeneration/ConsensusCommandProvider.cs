using AElf.Standards.ACS4;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;

// ReSharper disable once CheckNamespace
namespace AElf.Contracts.Consensus.AEDPoS;

// ReSharper disable once InconsistentNaming
public partial class AEDPoSContract
{
    private class ConsensusCommandProvider
    {
        private readonly ICommandStrategy _commandStrategy;

        public ConsensusCommandProvider(ICommandStrategy commandStrategy)
        {
            _commandStrategy = commandStrategy;
        }

        /// <summary>
        ///     No, you can't mine blocks.
        /// </summary>
        public static ConsensusCommand InvalidConsensusCommand => new()
        {
            ArrangedMiningTime = new Timestamp { Seconds = int.MaxValue },
            Hint = ByteString.CopyFrom(new AElfConsensusHint
            {
                Behaviour = AElfConsensusBehaviour.Nothing
            }.ToByteArray())
        };

        public ConsensusCommand GetConsensusCommand()
        {
            return _commandStrategy.GetConsensusCommand();
        }
    }
}