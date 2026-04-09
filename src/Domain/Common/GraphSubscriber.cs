using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace ESOrleansApproach.Domain.Common
{
    public sealed class SubscriptionToken
    {
        public object Source { get; init; }

        public IChangeNotifier TargetParent { get; init; }

        public EventHandler<EventBase> Handler { get; init; }
    }

    public static class GraphSubscriber
    {
        private static readonly ConditionalWeakTable<object, List<SubscriptionToken>> _subscriptions = new();
        private static readonly ConcurrentDictionary<Type, CachedGraphProperty[]> _propertyCache = new();

        private sealed class CachedGraphProperty
        {
            public Func<object, object> GetValue;

            public bool IsCollection;
        }

        public static void SubscribeGraph(object root)
        {
            ArgumentNullException.ThrowIfNull(root);

            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            RecursiveSubscribe(root, null, visited);
        }

        public static void UnsubscribeGraph(object root)
        {
            ArgumentNullException.ThrowIfNull(root);

            var visited = new HashSet<object>(ReferenceEqualityComparer.Instance);
            RecursiveUnsubscribe(root, visited);
        }

        private static CachedGraphProperty[] GetCachedProperties(Type type)
        {
            return _propertyCache.GetOrAdd(type, static ownerType =>
            {
                var props = ownerType.GetProperties(BindingFlags.Public | BindingFlags.Instance);
                var result = new List<CachedGraphProperty>(props.Length);

                foreach (var prop in props)
                {
                    if (!prop.CanRead || prop.GetIndexParameters().Length != 0)
                        continue;

                    var propertyType = prop.PropertyType;
                    if (propertyType.IsPrimitive || propertyType == typeof(string) || propertyType.IsValueType || propertyType.IsEnum)
                        continue;

                    result.Add(new CachedGraphProperty
                    {
                        GetValue = BuildGetter(ownerType, prop),
                        IsCollection = typeof(IEnumerable).IsAssignableFrom(propertyType)
                    });
                }

                return result.ToArray();
            });
        }

        private static Func<object, object> BuildGetter(Type ownerType, PropertyInfo property)
        {
            var param = Expression.Parameter(typeof(object), "obj");
            var body = Expression.Convert(Expression.Property(Expression.Convert(param, ownerType), property), typeof(object));
            return Expression.Lambda<Func<object, object>>(body, param).Compile();
        }

        private static void RecursiveSubscribe(object current, IChangeNotifier parent, HashSet<object> visited)
        {
            if (current is null)
                return;

            var currentType = current.GetType();
            if (currentType.IsPrimitive || currentType == typeof(string) || currentType.IsValueType)
                return;

            if (!visited.Add(current))
                return;

            if (current is IChangeNotifier childNotifier && parent is not null)
            {
                RemoveExistingToken(childNotifier, parent);

                EventHandler<EventBase> handler = (_, e) => parent.NotifyChanged(e);
                childNotifier.Changed += handler;
                AddSubscriptionToken(childNotifier, new SubscriptionToken
                {
                    Source = childNotifier,
                    TargetParent = parent,
                    Handler = handler
                });
            }

            var nextParent = current as IChangeNotifier ?? parent;
            foreach (var prop in GetCachedProperties(currentType))
            {
                object value;
                try
                {
                    value = prop.GetValue(current);
                }
                catch
                {
                    continue;
                }

                if (value is null)
                    continue;

                if (prop.IsCollection && value is IEnumerable enumerable)
                {
                    foreach (var item in enumerable)
                    {
                        if (item is not null)
                            RecursiveSubscribe(item, nextParent, visited);
                    }

                    continue;
                }

                RecursiveSubscribe(value, nextParent, visited);
            }
        }

        private static void RecursiveUnsubscribe(object current, HashSet<object> visited)
        {
            if (current is null)
                return;

            var currentType = current.GetType();
            if (currentType.IsPrimitive || currentType == typeof(string) || currentType.IsValueType)
                return;

            if (!visited.Add(current))
                return;

            if (_subscriptions.TryGetValue(current, out var list))
            {
                foreach (var token in list)
                {
                    if (token.Source is IChangeNotifier source && token.Handler is not null)
                    {
                        source.Changed -= token.Handler;
                    }
                }

                _subscriptions.Remove(current);
            }

            foreach (var prop in GetCachedProperties(currentType))
            {
                object value;
                try
                {
                    value = prop.GetValue(current);
                }
                catch
                {
                    continue;
                }

                if (value is null)
                    continue;

                if (prop.IsCollection && value is IEnumerable enumerable)
                {
                    foreach (var item in enumerable)
                    {
                        if (item is not null)
                            RecursiveUnsubscribe(item, visited);
                    }

                    continue;
                }

                RecursiveUnsubscribe(value, visited);
            }
        }

        private static void AddSubscriptionToken(object key, SubscriptionToken token)
        {
            lock (_subscriptions)
            {
                if (!_subscriptions.TryGetValue(key, out var list))
                {
                    list = [];
                    _subscriptions.Add(key, list);
                }

                list.Add(token);
            }
        }

        private static void RemoveExistingToken(IChangeNotifier child, IChangeNotifier parent)
        {
            if (!_subscriptions.TryGetValue(child, out var list))
                return;

            for (var i = list.Count - 1; i >= 0; i--)
            {
                var token = list[i];
                if (ReferenceEquals(token.TargetParent, parent) && token.Handler is not null)
                {
                    child.Changed -= token.Handler;
                    list.RemoveAt(i);
                }
            }

            if (list.Count == 0)
                _subscriptions.Remove(child);
        }
    }
}