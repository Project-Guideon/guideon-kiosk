using System;
using System.Collections.Generic;

namespace Guideon.Core
{
    /// <summary>
    /// 컴포넌트 간 직접 참조 없이 이벤트를 주고받는 전역 이벤트 버스.
    /// Subscribe → Publish → Unsubscribe 순으로 쓴다.
    /// </summary>
    public static class EventBus
    {
        private static readonly Dictionary<Type, List<Delegate>> _handlers = new();

        public static void Subscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            if (!_handlers.ContainsKey(type))
                _handlers[type] = new List<Delegate>();
            _handlers[type].Add(handler);
        }

        public static void Unsubscribe<T>(Action<T> handler)
        {
            var type = typeof(T);
            if (_handlers.TryGetValue(type, out var list))
                list.Remove(handler);
        }

        public static void Publish<T>(T eventData)
        {
            var type = typeof(T);
            if (!_handlers.TryGetValue(type, out var list)) return;

            foreach (var handler in list.ToArray())
                ((Action<T>)handler)?.Invoke(eventData);
        }

        /// <summary>
        /// 씬 전환 등 전체 초기화 시 모든 구독 해제. 필요할 때만 쓸 것.
        /// </summary>
        public static void Clear()
        {
            _handlers.Clear();
        }
    }
}
