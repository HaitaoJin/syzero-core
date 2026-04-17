using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using SyZero.Cache;
using SyZero.EventBus.DBEventBus.Entity;
using SyZero.EventBus.DBEventBus.Repository;

namespace SyZero.EventBus.DBEventBus
{
    /// <summary>
    /// 数据库事件总线实现
    /// 适用于需要持久化事件信息到数据库的场景
    /// </summary>
    public class DBEventBus : IEventBus, IDisposable, IAsyncDisposable
    {
        private readonly IEventSubscriptionRepository _subscriptionRepository;
        private readonly IEventQueueRepository _eventQueueRepository;
        private readonly IDeadLetterEventRepository _deadLetterRepository;
        private readonly ILeaderElectionRepository _leaderRepository;
        private readonly ICache _cache;
        private readonly DBEventBusOptions _options;
        private readonly ConcurrentDictionary<string, Func<object>> _handlerFactories = new ConcurrentDictionary<string, Func<object>>();
        private readonly string _instanceId;
        private Timer _processTimer;
        private Timer _cleanupTimer;
        private Timer _leaderRenewTimer;
        private bool _isLeader = false;
        private bool _disposed = false;

        /// <summary>
        /// 当前实例是否为 Leader
        /// </summary>
        public bool IsLeader => _isLeader;

        /// <summary>
        /// 当前实例ID
        /// </summary>
        public string InstanceId => _instanceId;

        public DBEventBus(
            IEventSubscriptionRepository subscriptionRepository,
            IEventQueueRepository eventQueueRepository,
            IDeadLetterEventRepository deadLetterRepository,
            ICache cache,
            DBEventBusOptions options,
            ILeaderElectionRepository leaderRepository = null)
        {
            _subscriptionRepository = subscriptionRepository ?? throw new ArgumentNullException(nameof(subscriptionRepository));
            _eventQueueRepository = eventQueueRepository ?? throw new ArgumentNullException(nameof(eventQueueRepository));
            _deadLetterRepository = deadLetterRepository ?? throw new ArgumentNullException(nameof(deadLetterRepository));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
            _options = options ?? new DBEventBusOptions();
            _leaderRepository = leaderRepository;
            _instanceId = Guid.NewGuid().ToString("N");

            // 如果启用 Leader 选举，先尝试获取 Leader
            if (_options.EnableLeaderElection && _leaderRepository != null)
            {
                RunSync(TryAcquireLeadershipAsync);

                // 启动 Leader 续期定时器
                _leaderRenewTimer = new Timer(
                    _ => RunBackgroundTask(TryAcquireLeadershipAsync, "Leader 续期定时器"),
                    null,
                    TimeSpan.FromSeconds(_options.LeaderLockRenewIntervalSeconds),
                    TimeSpan.FromSeconds(_options.LeaderLockRenewIntervalSeconds));
            }
            else
            {
                // 不启用选举时，所有实例都是 Leader
                _isLeader = true;
            }

            // 启动事件处理定时器
            if (_options.EnableAsync)
            {
                _processTimer = new Timer(
                    _ => RunBackgroundTask(ProcessEventsAsync, "事件处理定时器"),
                    null,
                    TimeSpan.FromSeconds(_options.EventProcessIntervalSeconds),
                    TimeSpan.FromSeconds(_options.EventProcessIntervalSeconds));
            }

            // 启动清理定时器
            if (_options.AutoCleanExpiredEvents)
            {
                _cleanupTimer = new Timer(
                    _ => RunBackgroundTask(CleanExpiredEventsAsync, "清理定时器"),
                    null,
                    TimeSpan.FromSeconds(_options.AutoCleanIntervalSeconds),
                    TimeSpan.FromSeconds(_options.AutoCleanIntervalSeconds));
            }
        }

        #region Leader 选举

        private async Task TryAcquireLeadershipAsync()
        {
            if (_leaderRepository == null) return;

            try
            {
                const string leaderKey = "EventBus";
                var currentLock = await _leaderRepository.GetLeaderLockAsync(leaderKey);

                if (currentLock != null)
                {
                    // 如果是自己持有的锁，续期
                    if (currentLock.InstanceId == _instanceId)
                    {
                        await _leaderRepository.RenewLeaderLockAsync(leaderKey, _instanceId, _options.LeaderLockExpireSeconds);
                        _isLeader = true;
                        return;
                    }

                    // 检查锁是否过期
                    if (currentLock.ExpireTime > DateTime.UtcNow)
                    {
                        // 锁未过期，当前实例不是 Leader
                        _isLeader = false;
                        return;
                    }

                    // 锁已过期，可以抢占
                    Console.WriteLine($"SyZero.EventBus.DB: Leader 锁已过期 (原 Leader: {currentLock.InstanceId})，尝试获取...");
                }

                // 尝试获取锁
                var success = await _leaderRepository.TryAcquireLeaderLockAsync(leaderKey, _instanceId, _options.LeaderLockExpireSeconds);
                _isLeader = success;

                if (success)
                {
                    Console.WriteLine($"SyZero.EventBus.DB: 当前实例 [{_instanceId}] 成为 Leader");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SyZero.EventBus.DB: 获取 Leader 权限失败: {ex.Message}");
                _isLeader = false;
            }
        }

        private async Task ReleaseLeadershipAsync()
        {
            if (!_isLeader || _leaderRepository == null) return;

            try
            {
                const string leaderKey = "EventBus";
                await _leaderRepository.ReleaseLeaderLockAsync(leaderKey, _instanceId);
                Console.WriteLine($"SyZero.EventBus.DB: 实例 [{_instanceId}] 释放 Leader 权限");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SyZero.EventBus.DB: 释放 Leader 权限失败: {ex.Message}");
            }
            finally
            {
                _isLeader = false;
            }
        }

        #endregion

        #region 订阅管理

        public void Subscribe<T, TH>(Func<TH> handler)
            where TH : IEventHandler<T>
        {
            var eventName = GetEventKey<T>();
            RunSync(() => DoSubscribe(typeof(TH), eventName, () => handler(), false));
        }

        public void SubscribeDynamic<TH>(string eventName)
            where TH : IDynamicEventHandler
        {
            RunSync(() => DoSubscribe(typeof(TH), eventName, null, true));
        }

        private async Task DoSubscribe(Type handlerType, string eventName, Func<object> handlerFactory, bool isDynamic)
        {
            // 检查是否已订阅
            var existing = await _subscriptionRepository.GetByEventAndHandlerAsync(eventName, handlerType.FullName);
            if (existing != null)
            {
                Console.WriteLine($"SyZero.EventBus.DB: 事件处理器 {handlerType.Name} 已订阅事件 {eventName}");
                return;
            }

            // 添加订阅
            var entity = new EventSubscriptionEntity
            {
                EventName = eventName,
                HandlerTypeName = handlerType.FullName,
                IsDynamic = isDynamic,
                CreateTime = DateTime.UtcNow,
                UpdateTime = DateTime.UtcNow
            };

            await _subscriptionRepository.AddAsync(entity);

            if (handlerFactory != null)
            {
                _handlerFactories[$"{eventName}_{handlerType.Name}"] = handlerFactory;
            }

            // 清除缓存
            _cache.Remove($"EventBus:Subscriptions:{eventName}");

            Console.WriteLine($"SyZero.EventBus.DB: 事件处理器 {handlerType.Name} 订阅事件 {eventName}");
        }

        public void Unsubscribe<T, TH>()
            where TH : IEventHandler<T>
        {
            var eventName = GetEventKey<T>();
            var handlerType = typeof(TH);
            RunSync(() => DoUnsubscribe(eventName, handlerType));
        }

        public void UnsubscribeDynamic<TH>(string eventName)
            where TH : IDynamicEventHandler
        {
            var handlerType = typeof(TH);
            RunSync(() => DoUnsubscribe(eventName, handlerType));
        }

        private async Task DoUnsubscribe(string eventName, Type handlerType)
        {
            await _subscriptionRepository.RemoveAsync(eventName, handlerType.FullName);
            _handlerFactories.TryRemove($"{eventName}_{handlerType.Name}", out _);

            // 清除缓存
            _cache.Remove($"EventBus:Subscriptions:{eventName}");

            Console.WriteLine($"SyZero.EventBus.DB: 事件处理器 {handlerType.Name} 取消订阅事件 {eventName}");
        }

        public void Clear()
        {
            RunSync(() => _subscriptionRepository.ClearAsync());
            _handlerFactories.Clear();

            // 清除所有缓存
            var events = GetSubscribedEvents();
            foreach (var eventName in events)
            {
                _cache.Remove($"EventBus:Subscriptions:{eventName}");
            }

            Console.WriteLine("SyZero.EventBus.DB: 清空所有订阅");
        }

        public bool IsSubscribed<T>()
        {
            var eventName = GetEventKey<T>();
            return IsSubscribed(eventName);
        }

        public bool IsSubscribed(string eventName)
        {
            var cacheKey = $"EventBus:Subscriptions:{eventName}";
            if (_cache.Exist(cacheKey))
            {
                return _cache.Get<bool>(cacheKey);
            }

            var result = RunSync(() => _subscriptionRepository.HasSubscriptionsAsync(eventName));
            _cache.Set(cacheKey, result, _options.CacheExpirationSeconds);
            return result;
        }

        public IEnumerable<string> GetSubscribedEvents()
        {
            return RunSync(() => _subscriptionRepository.GetAllEventNamesAsync());
        }

        #endregion

        #region 事件发布

        public void Publish(EventBase @event)
        {
            var eventName = @event.GetType().Name;
            RunSync(() => PublishInternal(eventName, @event));
        }

        public async Task PublishAsync(EventBase @event)
        {
            var eventName = @event.GetType().Name;
            await PublishInternal(eventName, @event);
        }

        public void Publish(string eventName, object eventData)
        {
            RunSync(() => PublishInternal(eventName, eventData));
        }

        public async Task PublishAsync(string eventName, object eventData)
        {
            await PublishInternal(eventName, eventData);
        }

        public void PublishBatch(IEnumerable<EventBase> events)
        {
            RunSync(() => PublishBatchAsync(events));
        }

        public async Task PublishBatchAsync(IEnumerable<EventBase> events)
        {
            var tasks = events.Select(e => PublishAsync(e));
            await Task.WhenAll(tasks);
        }

        private async Task PublishInternal(string eventName, object eventData)
        {
            if (!IsSubscribed(eventName))
            {
                Console.WriteLine($"SyZero.EventBus.DB: 事件 {eventName} 没有订阅者");
                return;
            }

            if (_options.EnableAsync)
            {
                // 异步模式：加入数据库队列
                var entity = new EventQueueEntity
                {
                    EventName = eventName,
                    EventData = JsonSerializer.Serialize(eventData),
                    EventTypeName = eventData.GetType().FullName,
                    Status = 0, // 待处理
                    RetryCount = 0,
                    CreateTime = DateTime.UtcNow
                };

                await _eventQueueRepository.AddAsync(entity);
            }
            else
            {
                // 同步模式：立即处理
                await ProcessEventAsync(eventName, eventData);
            }
        }

        #endregion

        #region 事件处理

        private async Task ProcessEventAsync(string eventName, object eventData)
        {
            // 获取订阅者
            var subscriptions = await _subscriptionRepository.GetByEventNameAsync(eventName);
            if (subscriptions == null || !subscriptions.Any())
            {
                return;
            }

            var tasks = new List<Task>();

            foreach (var subscription in subscriptions)
            {
                tasks.Add(InvokeSubscriptionAsync(eventName, eventData, subscription));
            }

            await Task.WhenAll(tasks);
        }

        private async Task ProcessEventsAsync()
        {
            // 只有 Leader 实例才处理事件
            if (!_isLeader)
            {
                return;
            }

            try
            {
                // 获取待处理的事件
                var events = await _eventQueueRepository.GetPendingEventsAsync(_options.MaxEventsPerBatch);
                if (events == null || !events.Any())
                {
                    return;
                }

                foreach (var eventEntity in events)
                {
                    try
                    {
                        // 更新状态为处理中
                        eventEntity.Status = 1;
                        eventEntity.ProcessTime = DateTime.UtcNow;
                        await _eventQueueRepository.UpdateAsync(eventEntity);

                        // 反序列化事件数据
                        var eventType = Type.GetType(eventEntity.EventTypeName);
                        object eventData;
                        if (eventType != null)
                        {
                            eventData = JsonSerializer.Deserialize(eventEntity.EventData, eventType);
                        }
                        else
                        {
                            eventData = JsonSerializer.Deserialize<object>(eventEntity.EventData);
                        }

                        // 处理事件
                        await ExecuteWithTimeoutAsync(() => ProcessEventAsync(eventEntity.EventName, eventData));

                        // 更新状态为已完成
                        eventEntity.Status = 2;
                        eventEntity.CompleteTime = DateTime.UtcNow;
                        await _eventQueueRepository.UpdateAsync(eventEntity);

                        // 删除已处理的事件
                        await _eventQueueRepository.DeleteAsync(eventEntity.Id);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"SyZero.EventBus.DB: 处理事件 {eventEntity.EventName} 失败: {ex.Message}");

                        // 重试机制
                        if (_options.EnableRetry && eventEntity.RetryCount < _options.RetryCount)
                        {
                            eventEntity.RetryCount++;
                            eventEntity.LastRetryTime = DateTime.UtcNow;
                            eventEntity.Status = 0; // 重新标记为待处理
                            eventEntity.ErrorMessage = ex.Message;
                            await _eventQueueRepository.UpdateAsync(eventEntity);

                            await Task.Delay(TimeSpan.FromSeconds(_options.RetryIntervalSeconds));
                        }
                        else if (_options.EnableDeadLetterQueue)
                        {
                            // 加入死信队列
                            var deadLetter = new DeadLetterEventEntity
                            {
                                EventName = eventEntity.EventName,
                                EventData = eventEntity.EventData,
                                EventTypeName = eventEntity.EventTypeName,
                                RetryCount = eventEntity.RetryCount,
                                LastError = ex.Message,
                                OriginalCreateTime = eventEntity.CreateTime,
                                CreateTime = DateTime.UtcNow
                            };
                            await _deadLetterRepository.AddAsync(deadLetter);

                            // 从队列中删除
                            eventEntity.Status = 3; // 失败
                            eventEntity.ErrorMessage = ex.Message;
                            await _eventQueueRepository.UpdateAsync(eventEntity);
                            await _eventQueueRepository.DeleteAsync(eventEntity.Id);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SyZero.EventBus.DB: 处理事件队列时发生错误: {ex.Message}");
            }
        }

        #endregion

        #region 清理过期事件

        private async Task CleanExpiredEventsAsync()
        {
            // 只有 Leader 实例才清理
            if (!_isLeader)
            {
                return;
            }

            try
            {
                await _eventQueueRepository.CleanExpiredEventsAsync(_options.EventExpireSeconds);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SyZero.EventBus.DB: 清理过期事件失败: {ex.Message}");
            }
        }

        #endregion

        #region 辅助方法

        private string GetEventKey<T>()
        {
            return typeof(T).Name;
        }

        private static Type GetHandlerEventType(Type handlerType)
        {
            return handlerType.GetInterfaces()
                .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEventHandler<>))
                ?.GetGenericArguments()[0];
        }

        private static object ConvertEventData(object eventData, Type eventType)
        {
            if (eventData == null)
            {
                return null;
            }

            if (eventType.IsInstanceOfType(eventData))
            {
                return eventData;
            }

            return JsonSerializer.Deserialize(JsonSerializer.Serialize(eventData), eventType);
        }

        private async Task InvokeSubscriptionAsync(string eventName, object eventData, EventSubscriptionEntity subscription)
        {
            try
            {
                var handlerType = Type.GetType(subscription.HandlerTypeName);
                if (handlerType == null)
                {
                    return;
                }

                if (subscription.IsDynamic)
                {
                    if (Activator.CreateInstance(handlerType) is IDynamicEventHandler dynamicHandler)
                    {
                        await dynamicHandler.HandleAsync(eventName, eventData);
                    }

                    return;
                }

                var factoryKey = $"{eventName}_{handlerType.Name}";
                if (!_handlerFactories.TryGetValue(factoryKey, out var factory))
                {
                    return;
                }

                var handler = factory();
                var handlerEventType = GetHandlerEventType(handlerType);
                if (handler == null || handlerEventType == null)
                {
                    return;
                }

                var concreteType = typeof(IEventHandler<>).MakeGenericType(handlerEventType);
                var method = concreteType.GetMethod("HandleAsync");
                var handlerEventData = ConvertEventData(eventData, handlerEventType);
                if (method?.Invoke(handler, new[] { handlerEventData }) is Task taskResult)
                {
                    await taskResult;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SyZero.EventBus.DB: 处理事件 {eventName} 时发生错误: {ex.Message}");
                throw;
            }
        }

        private async Task ExecuteWithTimeoutAsync(Func<Task> taskFactory)
        {
            if (_options.EventHandlerTimeoutSeconds <= 0)
            {
                await taskFactory();
                return;
            }

            var processingTask = taskFactory();
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(_options.EventHandlerTimeoutSeconds));
            var completedTask = await Task.WhenAny(processingTask, timeoutTask);
            if (completedTask == timeoutTask)
            {
                throw new TimeoutException($"事件处理超时（{_options.EventHandlerTimeoutSeconds}秒）");
            }

            await processingTask;
        }

        private static void RunSync(Func<Task> taskFactory)
        {
            taskFactory().ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private static T RunSync<T>(Func<Task<T>> taskFactory)
        {
            return taskFactory().ConfigureAwait(false).GetAwaiter().GetResult();
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
                    Console.WriteLine($"SyZero.EventBus.DB: {operationName}异常: {ex.Message}");
                }
            });
        }

        #endregion

        #region IDisposable

        public void Dispose()
        {
            RunSync(async () => await DisposeAsync());
        }

        #endregion

        #region IAsyncDisposable

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
                return;

            _processTimer?.Dispose();
            _cleanupTimer?.Dispose();
            _leaderRenewTimer?.Dispose();

            await ReleaseLeadershipAsync();

            _disposed = true;
        }

        #endregion
    }
}
