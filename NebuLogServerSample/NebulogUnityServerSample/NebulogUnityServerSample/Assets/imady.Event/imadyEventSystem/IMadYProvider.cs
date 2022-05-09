
using imady.Message;

namespace imady.Event
{
    public interface IMadYProvider<T> : IMadYEventObjectBase where T : MadYMessageBase
    {
        new void Subscribe(IMadYEventObjectBase observer);

    }

}