using System;
using System.Collections.Generic;
using System.Linq;

namespace SDK_Manager_GUI.ViewModels
{
    public interface IMessage { }

    public interface IRecipient<TMessage> where TMessage : IMessage
    {
        void Receive(TMessage message);
    }

    public static class WeakMessenger
    {
        // 使用强引用保存 RecipientAction，避免被 GC 回收导致消息丢失
        // 仅对 recipient（目标对象）使用弱引用来判断是否存活
        private static readonly Dictionary<Type, List<IRecipientAction>> _recipients = new Dictionary<Type, List<IRecipientAction>>();
        private static readonly object _lock = new object();

        public static void Register<TMessage>(object recipient, Action<TMessage> action) where TMessage : IMessage
        {
            lock (_lock)
            {
                var type = typeof(TMessage);
                if (!_recipients.ContainsKey(type))
                    _recipients[type] = new List<IRecipientAction>();

                _recipients[type].Add(new RecipientAction<TMessage>(recipient, action));
            }
        }

        public static void Unregister<TMessage>(object recipient) where TMessage : IMessage
        {
            lock (_lock)
            {
                var type = typeof(TMessage);
                if (!_recipients.ContainsKey(type)) return;

                _recipients[type].RemoveAll(action =>
                    !action.IsAlive || action.GetTarget() == recipient);
            }
        }

        public static void UnregisterAll(object recipient)
        {
            lock (_lock)
            {
                foreach (var key in _recipients.Keys.ToList())
                {
                    _recipients[key].RemoveAll(action =>
                        !action.IsAlive || action.GetTarget() == recipient);
                }
            }
        }

        public static void Send<TMessage>(TMessage message) where TMessage : IMessage
        {
            List<IRecipientAction> recipientsCopy;
            lock (_lock)
            {
                var type = typeof(TMessage);
                if (!_recipients.ContainsKey(type)) return;

                recipientsCopy = _recipients[type].ToList();
            }

            var dead = new List<IRecipientAction>();
            foreach (var action in recipientsCopy)
            {
                if (action.IsAlive)
                    action.Execute(message);
                else
                    dead.Add(action);
            }

            // 清理已死亡的注册
            if (dead.Count > 0)
            {
                lock (_lock)
                {
                    var type = typeof(TMessage);
                    foreach (var d in dead)
                        _recipients[type].Remove(d);
                }
            }
        }

        private interface IRecipientAction
        {
            bool IsAlive { get; }
            object GetTarget();
            void Execute(object message);
        }

        private class RecipientAction<TMessage> : IRecipientAction where TMessage : IMessage
        {
            private readonly WeakReference _targetRef;
            private readonly Action<TMessage> _action;

            public RecipientAction(object target, Action<TMessage> action)
            {
                _targetRef = new WeakReference(target);
                _action = action;
            }

            public bool IsAlive => _targetRef.IsAlive;
            public object GetTarget() => _targetRef.Target;
            public void Execute(object message) => _action((TMessage)message);
        }
    }
}
