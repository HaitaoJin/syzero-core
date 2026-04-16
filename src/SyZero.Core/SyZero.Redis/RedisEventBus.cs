using FreeRedis;
using Microsoft.Extensions.DependencyInjection;
using SyZero.EventBus;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace SyZero.Redis
{
    /// <summary>
    /// 基于 Redis Pub/Sub 的事件总线实现
    /// </summary>
    public class RedisEventBus : IEventBus, IDisposable
    {
        private readonly RedisClient _redis;
        private readonly IServiceProvider _serviceProvider;
        private readonly RedisEventBusOptions _options;
        private readonly ConcurrentDictionary<string, List<Type>> _subscriptions = new ConcurrentDictionary<string, List<Type>>();
        private readonly ConcurrentDictionary<string, List<Type>> _dynamicSubscriptions = new ConcurrentDictionary<string, List<Type>>();
        private readonly ConcurrentDictionary<string, Func<object>> _handlerFactories = new ConcurrentDictionary<string, Func<object>>();
        private readonly ConcurrentDictionary<string, IDisposable> _channelSubscriptions = new ConcurrentDictionary<string, IDisposable>();
        private readonly object _subscriptionLock = new object();
        private bool _disposed;

        public RedisEventBus(RedisClient redis, IServiceProvider serviceProvider, RedisEventBusOptions options = null)
        {
            _redis = redis ?? throw new ArgumentNullException(nameof(redis));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
            _options = options ?? new RedisEventBusOptions();
            _options.Validate();
        }

        public void Subscribe<T, TH>(Func<TH> handler)
            where TH : IEventHandler<T>
        {
            var eventName = GetEventKey<T>();
            DoSubscribe(typeof(TH), eventName, () => handler());
        }

        public void SubscribeDynamic<TH>(string eventName)
            where TH : IDynamicEventHandler
        {
            DoSubscribeDynamic(typeof(TH), eventName);
        }

        public void Unsubscribe<T, TH>()
            where TH : IEventHandler<T>
        {
            var eventName = GetEventKey<T>();
            var handlerType = typeof(TH);

            lock (_subscriptionLock)
            {
                if (_subscriptions.TryGetValue(eventName, out var handlers))
                {
                    handlers.Remove(handlerType);
                    _handlerFactories.TryRemove($"{eventName}_{handlerType.Name}", out _);

                    if (handlers.Count == 0)
                    {
                        _subscriptions.TryRemove(eventName, out _);
                    }

                    TryRemoveRedisSubscription(eventName);
                }
            }
        }

        public void UnsubscribeDynamic<TH>(string eventName)
            where TH : IDynamicEventHandler
        {
            var handlerType = typeof(TH);

            lock (_subscriptionLock)
            {
                if (_dynamicSubscriptions.TryGetValue(eventName, out var handlers))
                {
                    handlers.Remove(handlerType);

                    if (handlers.Count == 0)
                    {
                        _dynamicSubscriptions.TryRemove(eventName, out _);
                    }

                    TryRemoveRedisSubscription(eventName);
                }
            }
        }

        public void Publish(EventBase @event)
        {
            RunSync(() => PublishAsync(@event));
        }

        public async Task PublishAsync(EventBase @event)
        {
            if (@event == null)
            {
                throw new ArgumentNullException(nameof(@event));
            }

            await PublishAsync(@event.EventName, @event);
        }

        public void Publish(string eventName, object eventData)
        {
            RunSync(() => PublishAsync(eventName, eventData));
        }

        public Task PublishAsync(string eventName, object eventData)
        {
            if (string.IsNullOrWhiteSpace(eventName))
            {
                throw new ArgumentException("Event name is required.", nameof(eventName));
            }

            var envelope = new RedisEventEnvelope
            {
                EventName = eventName,
                EventTypeName = eventData?.GetType().AssemblyQualifiedName ?? eventData?.GetType().FullName,
                EventDataJson = eventData == null ? null : JsonSerializer.Serialize(eventData),
                PublishTime = DateTime.UtcNow
            };

            _redis.Publish(GetChannel(eventName), JsonSerializer.Serialize(envelope));
            return Task.CompletedTask;
        }

        public void PublishBatch(IEnumerable<EventBase> events)
        {
            RunSync(() => PublishBatchAsync(events));
        }

        public async Task PublishBatchAsync(IEnumerable<EventBase> events)
        {
            if (events == null)
            {
                return;
            }

            foreach (var @event in events)
            {
                await PublishAsync(@event);
            }
        }

        public void Clear()
        {
            lock (_subscriptionLock)
            {
                _subscriptions.Clear();
                _dynamicSubscriptions.Clear();
                _handlerFactories.Clear();

                foreach (var subscription in _channelSubscriptions.Values)
                {
                    subscription.Dispose();
                }

                _channelSubscriptions.Clear();
            }
        }

        public bool IsSubscribed<T>()
        {
            return IsSubscribed(GetEventKey<T>());
        }

        public bool IsSubscribed(string eventName)
        {
            lock (_subscriptionLock)
            {
                return _subscriptions.ContainsKey(eventName) || _dynamicSubscriptions.ContainsKey(eventName);
            }
        }

        public IEnumerable<string> GetSubscribedEvents()
        {
            lock (_subscriptionLock)
            {
                var events = new HashSet<string>();
                foreach (var eventName in _subscriptions.Keys)
                {
                    events.Add(eventName);
                }

                foreach (var eventName in _dynamicSubscriptions.Keys)
                {
                    events.Add(eventName);
                }

                return events.ToArray();
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            Clear();
        }

        private void DoSubscribe(Type handlerType, string eventName, Func<object> handlerFactory)
        {
            lock (_subscriptionLock)
            {
                if (!_subscriptions.TryGetValue(eventName, out var handlers))
                {
                    handlers = new List<Type>();
                    _subscriptions[eventName] = handlers;
                }

                if (handlers.Contains(handlerType))
                {
                    return;
                }

                handlers.Add(handlerType);
                _handlerFactories[$"{eventName}_{handlerType.Name}"] = handlerFactory;
                EnsureRedisSubscription(eventName);
            }
        }

        private void DoSubscribeDynamic(Type handlerType, string eventName)
        {
            lock (_subscriptionLock)
            {
                if (!_dynamicSubscriptions.TryGetValue(eventName, out var handlers))
                {
                    handlers = new List<Type>();
                    _dynamicSubscriptions[eventName] = handlers;
                }

                if (handlers.Contains(handlerType))
                {
                    return;
                }

                handlers.Add(handlerType);
                EnsureRedisSubscription(eventName);
            }
        }

        private void EnsureRedisSubscription(string eventName)
        {
            if (_channelSubscriptions.ContainsKey(eventName))
            {
                return;
            }

            var channel = GetChannel(eventName);
            var disposable = _redis.Subscribe(channel, (ch, msg) =>
            {
                RunBackgroundTask(() => ProcessMessageAsync(eventName, ConvertMessage(msg)), $"处理事件 {eventName}");
            });

            _channelSubscriptions[eventName] = disposable;
        }

        private void TryRemoveRedisSubscription(string eventName)
        {
            if (_subscriptions.ContainsKey(eventName) || _dynamicSubscriptions.ContainsKey(eventName))
            {
                return;
            }

            if (_channelSubscriptions.TryRemove(eventName, out var disposable))
            {
                disposable.Dispose();
                _redis.UnSubscribe(GetChannel(eventName));
            }
        }

        private async Task ProcessMessageAsync(string fallbackEventName, string message)
        {
            var envelope = DeserializeEnvelope(fallbackEventName, message);

            await ProcessTypedHandlersAsync(envelope);
            await ProcessDynamicHandlersAsync(envelope);
        }

        private async Task ProcessTypedHandlersAsync(RedisEventEnvelope envelope)
        {
            var handlerTypes = GetTypedHandlers(envelope.EventName);
            foreach (var handlerType in handlerTypes)
            {
                try
                {
                    var factoryKey = $"{envelope.EventName}_{handlerType.Name}";
                    if (!_handlerFactories.TryGetValue(factoryKey, out var factory))
                    {
                        continue;
                    }

                    var handler = factory();
                    if (handler == null)
                    {
                        continue;
                    }

                    var eventType = GetHandlerEventType(handlerType) ?? ResolveType(envelope.EventTypeName);
                    if (eventType == null)
                    {
                        continue;
                    }

                    var eventData = DeserializeEventData(envelope.EventDataJson, eventType);
                    var concreteType = typeof(IEventHandler<>).MakeGenericType(eventType);
                    if (concreteType.GetMethod("HandleAsync")?.Invoke(handler, new[] { eventData }) is Task taskResult)
                    {
                        await taskResult;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SyZero.Redis.EventBus: typed 处理器 {handlerType.Name} 执行失败: {ex.Message}");
                }
            }
        }

        private async Task ProcessDynamicHandlersAsync(RedisEventEnvelope envelope)
        {
            var handlerTypes = GetDynamicHandlers(envelope.EventName);
            if (handlerTypes.Count == 0)
            {
                return;
            }

            dynamic eventData = string.IsNullOrWhiteSpace(envelope.EventDataJson)
                ? null
                : JsonSerializer.Deserialize<dynamic>(envelope.EventDataJson);

            using var scope = _serviceProvider.CreateScope();
            foreach (var handlerType in handlerTypes)
            {
                try
                {
                    var handler = scope.ServiceProvider.GetService(handlerType) as IDynamicEventHandler
                                  ?? Activator.CreateInstance(handlerType) as IDynamicEventHandler;
                    if (handler != null)
                    {
                        await handler.HandleAsync(envelope.EventName, eventData);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SyZero.Redis.EventBus: dynamic 处理器 {handlerType.Name} 执行失败: {ex.Message}");
                }
            }
        }

        private List<Type> GetTypedHandlers(string eventName)
        {
            lock (_subscriptionLock)
            {
                return _subscriptions.TryGetValue(eventName, out var handlers)
                    ? handlers.ToList()
                    : new List<Type>();
            }
        }

        private List<Type> GetDynamicHandlers(string eventName)
        {
            lock (_subscriptionLock)
            {
                return _dynamicSubscriptions.TryGetValue(eventName, out var handlers)
                    ? handlers.ToList()
                    : new List<Type>();
            }
        }

        private string GetChannel(string eventName)
        {
            return $"{_options.ChannelPrefix}{eventName}";
        }

        private static string GetEventKey<T>()
        {
            return typeof(T).Name;
        }

        private static RedisEventEnvelope DeserializeEnvelope(string fallbackEventName, string message)
        {
            try
            {
                var envelope = JsonSerializer.Deserialize<RedisEventEnvelope>(message);
                if (envelope != null && !string.IsNullOrWhiteSpace(envelope.EventName))
                {
                    return envelope;
                }
            }
            catch
            {
            }

            return new RedisEventEnvelope
            {
                EventName = fallbackEventName,
                EventDataJson = message,
                PublishTime = DateTime.UtcNow
            };
        }

        private static Type GetHandlerEventType(Type handlerType)
        {
            return handlerType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>))
                ?.GetGenericArguments()[0];
        }

        private static Type ResolveType(string typeName)
        {
            if (string.IsNullOrWhiteSpace(typeName))
            {
                return null;
            }

            var type = Type.GetType(typeName);
            if (type != null)
            {
                return type;
            }

            return AppDomain.CurrentDomain.GetAssemblies()
                .Select(assembly => assembly.GetType(typeName))
                .FirstOrDefault(candidate => candidate != null);
        }

        private static object DeserializeEventData(string eventDataJson, Type eventType)
        {
            if (string.IsNullOrWhiteSpace(eventDataJson))
            {
                return null;
            }

            return JsonSerializer.Deserialize(eventDataJson, eventType);
        }

        private static string ConvertMessage(object message)
        {
            if (message is byte[] bytes)
            {
                return Encoding.UTF8.GetString(bytes);
            }

            return message?.ToString();
        }

        private static void RunSync(Func<Task> taskFactory)
        {
            taskFactory().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private static void RunBackgroundTask(Func<Task> taskFactory, string operationName)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await taskFactory();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SyZero.Redis.EventBus: {operationName}失败: {ex.Message}");
                }
            });
        }

        private sealed class RedisEventEnvelope
        {
            public string EventName { get; set; }

            public string EventTypeName { get; set; }

            public string EventDataJson { get; set; }

            public DateTime PublishTime { get; set; }
        }
    }
}
