using System;

namespace ESOrleansApproach.Domain.Common
{
    public interface IChangeNotifier
    {
        event EventHandler<EventBase> Changed;

        void NotifyChanged(EventBase e);

        void Subscribe(StateBase state);

        void Unsubscribe(StateBase state);
    }
}