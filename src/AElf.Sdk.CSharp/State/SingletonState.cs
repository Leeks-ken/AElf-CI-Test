using AElf.Types;
using Google.Protobuf;

namespace AElf.Sdk.CSharp.State;

public class SingletonState : StateBase
{
}

/// <summary>
///     Represents single values of a given type, for use in smart contract state.
/// </summary>
public class SingletonState<TEntity> : SingletonState
{
    private TEntity _originalValue;
    private TEntity _value;
    internal bool Loaded;
    internal bool Modified => !Equals(_originalValue, _value);

    public TEntity Value
    {
        get
        {
            if (!Loaded) Load();

            return _value;
        }
        set
        {
            if (!Loaded) Load();

            _value = value;
        }
    }

    internal override void Clear()
    {
        Loaded = false;
        _originalValue = default;
        _value = _originalValue;
    }

    internal override TransactionExecutingStateSet GetChanges()
    {
        var stateSet = new TransactionExecutingStateSet();
        var key = Path.ToStateKey(Context.Self);
        if (Modified) stateSet.Writes[key] = ByteString.CopyFrom(SerializationHelper.Serialize(_value));

        if (Loaded) stateSet.Reads[key] = true;

        return stateSet;
    }

    private void Load()
    {
        var bytes = Provider.Get(Path);
        _originalValue = SerializationHelper.Deserialize<TEntity>(bytes);
        _value = SerializationHelper.Deserialize<TEntity>(bytes);
        Loaded = true;
    }
}