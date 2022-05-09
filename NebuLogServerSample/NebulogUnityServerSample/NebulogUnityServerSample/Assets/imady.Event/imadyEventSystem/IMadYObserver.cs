using System;
using imady.Message;

namespace imady.Event
{
    public interface IMadYObserver<T> : IMadYEventObjectBase where T : MadYMessageBase
    {
        void OnCompleted();

        void OnError(Exception ex);

        void OnNext(T message);
    }
}
