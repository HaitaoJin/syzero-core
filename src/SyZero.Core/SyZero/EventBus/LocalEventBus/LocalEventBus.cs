using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace SyZero.EventBus.LocalEventBus
{
    /// <summary>
    /// 本地事件总线实现（基于内存和本地文件）
    /// 适用于开发测试环境或单机部署场景
    /// </summary>
    public class LocalEventBus : IEventBus, IDisposable, IAsyncDisposable
    {
        private readonly ConcurrentDictionary<string, List<Type>> _subscriptions = new ConcurrentDictionary<string, List<Type>>();
        private readonly ConcurrentDictionary<string, List<Type>> _dynamicSubscriptions = new ConcurrentDictionary<string, List<Type>>();
        private readonly ConcurrentDictionary<string, Func<object>> _handlerFactories = new ConcurrentDictionary<string, Func<object>>();
        private readonly ConcurrentQueue<EventWrapper> _eventQueue = new ConcurrentQueue<EventWrapper>();
        private readonly ConcurrentQueue<EventWrapper> _deadLetterQueue = new ConcurrentQueue<EventWrapper>();
        private readonly LocalEventBusOptions _options;
        private readonly string _subscriptionFilePath;
        private readonly string _eventFilePath;
        private readonly string _deadLetterFilePath;
        private readonly object _subscriptionLock = new object();
        private readonly object _fileLock = new object();
        private FileSystemWatcher _subscriptionWatcher;
        private FileSystemWatcher _eventWatcher;
        private Timer _cleanupTimer;
        private Timer _processTimer;
        private bool _disposed = false;

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="options">配置选项</param>
        public LocalEventBus(LocalEventBusOptions options = null)
        {
            _options = options ?? new LocalEventBusOptions();
            _subscriptionFilePath = _options.GetSubscriptionFilePath();
            _eventFilePath = _options.GetEventFilePath();
            _deadLetterFilePath = _options.GetDeadLetterFilePath();

            if (_options.EnableFilePersistence)
            {
                LoadSubscriptionsFromFile();
                LoadEventsFromFile();
                LoadDeadLetterFromFile();

                if (_options.EnableFileWatcher)
                {
                    InitFileWatcher();
                }
            }

            // 启动事件处理定时器
            if (_options.EnableAsync)
            {
                _processTimer = new Timer(
                    _ => RunBackgroundTask(ProcessEventsAsync, "事件处理定时器"),
                    null,
                    TimeSpan.FromSeconds(1),
                    TimeSpan.FromSeconds(1));
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

        #region 订阅管理

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
                    Console.WriteLine($"SyZero.EventBus: 事件处理器 {handlerType.Name} 已订阅事件 {eventName}");
                    return;
                }

                handlers.Add(handlerType);
                _handlerFactories[$"{eventName}_{handlerType.Name}"] = handlerFactory;
            }

            Console.WriteLine($"SyZero.EventBus: 事件处理器 {handlerType.Name} 订阅事件 {eventName}");

            if (_options.EnableFilePersistence)
            {
                SaveSubscriptionsToFile();
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
                    Console.WriteLine($"SyZero.EventBus: 动态事件处理器 {handlerType.Name} 已订阅事件 {eventName}");
                    return;
                }

                handlers.Add(handlerType);
            }

            Console.WriteLine($"SyZero.EventBus: 动态事件处理器 {handlerType.Name} 订阅事件 {eventName}");

            if (_options.EnableFilePersistence)
            {
                SaveSubscriptionsToFile();
            }
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

                    Console.WriteLine($"SyZero.EventBus: 事件处理器 {handlerType.Name} 取消订阅事件 {eventName}");

                    if (_options.EnableFilePersistence)
                    {
                        SaveSubscriptionsToFile();
                    }
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

                    Console.WriteLine($"SyZero.EventBus: 动态事件处理器 {handlerType.Name} 取消订阅事件 {eventName}");

                    if (_options.EnableFilePersistence)
                    {
                        SaveSubscriptionsToFile();
                    }
                }
            }
        }

        public void Clear()
        {
            lock (_subscriptionLock)
            {
                _subscriptions.Clear();
                _dynamicSubscriptions.Clear();
                _handlerFactories.Clear();
            }

            Console.WriteLine("SyZero.EventBus: 清空所有订阅");

            if (_options.EnableFilePersistence)
            {
                SaveSubscriptionsToFile();
            }
        }

        public bool IsSubscribed<T>()
        {
            var eventName = GetEventKey<T>();
            return IsSubscribed(eventName);
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
                foreach (var key in _subscriptions.Keys)
                {
                    events.Add(key);
                }
                foreach (var key in _dynamicSubscriptions.Keys)
                {
                    events.Add(key);
                }
                return events.ToArray();
            }
        }

        #endregion

        #region 事件发布

        public void Publish(EventBase @event)
        {
            var eventName = @event.GetType().Name;
            PublishInternal(eventName, @event);
        }

        public async Task PublishAsync(EventBase @event)
        {
            var eventName = @event.GetType().Name;
            await PublishInternalAsync(eventName, @event);
        }

        public void Publish(string eventName, object eventData)
        {
            PublishInternal(eventName, eventData);
        }

        public async Task PublishAsync(string eventName, object eventData)
        {
            await PublishInternalAsync(eventName, eventData);
        }

        public void PublishBatch(IEnumerable<EventBase> events)
        {
            foreach (var @event in events)
            {
                Publish(@event);
            }
        }

        public async Task PublishBatchAsync(IEnumerable<EventBase> events)
        {
            var tasks = events.Select(e => PublishAsync(e));
            await Task.WhenAll(tasks);
        }

        private void PublishInternal(string eventName, object eventData)
        {
            if (!IsSubscribed(eventName))
            {
                Console.WriteLine($"SyZero.EventBus: 事件 {eventName} 没有订阅者");
                return;
            }

            if (_options.EnableAsync)
            {
                // 异步模式：加入队列
                var wrapper = new EventWrapper
                {
                    EventName = eventName,
                    EventData = eventData,
                    CreateTime = DateTime.UtcNow,
                    RetryCount = 0
                };
                _eventQueue.Enqueue(wrapper);

                if (_options.EnableFilePersistence)
                {
                    SaveEventsToFile();
                }
            }
            else
            {
                // 同步模式：立即处理
                RunSync(() => ProcessEventAsync(eventName, eventData));
            }
        }

        private async Task PublishInternalAsync(string eventName, object eventData)
        {
            if (!IsSubscribed(eventName))
            {
                Console.WriteLine($"SyZero.EventBus: 事件 {eventName} 没有订阅者");
                return;
            }

            await ProcessEventAsync(eventName, eventData);
        }

        #endregion

        #region 事件处理

        private async Task ProcessEventAsync(string eventName, object eventData)
        {
            var tasks = new List<Task>();

            // 处理普通订阅
            var handlers = GetTypedHandlers(eventName);
            if (handlers.Count > 0)
            {
                foreach (var handlerType in handlers)
                {
                    tasks.Add(InvokeTypedHandlerAsync(eventName, eventData, handlerType));
                }
            }

            // 处理动态订阅
            var dynamicHandlers = GetDynamicHandlers(eventName);
            if (dynamicHandlers.Count > 0)
            {
                foreach (var handlerType in dynamicHandlers)
                {
                    tasks.Add(InvokeDynamicHandlerAsync(eventName, eventData, handlerType));
                }
            }

            await Task.WhenAll(tasks);
        }

        private async Task ProcessEventsAsync()
        {
            while (_eventQueue.TryDequeue(out var wrapper))
            {
                try
                {
                    await ExecuteWithTimeoutAsync(() => ProcessEventAsync(wrapper.EventName, wrapper.EventData));
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SyZero.EventBus: 处理事件队列时发生错误: {ex.Message}");

                    // 重试机制
                    if (_options.EnableRetry && wrapper.RetryCount < _options.RetryCount)
                    {
                        wrapper.RetryCount++;
                        wrapper.LastRetryTime = DateTime.UtcNow;
                        _eventQueue.Enqueue(wrapper);
                        await Task.Delay(TimeSpan.FromSeconds(_options.RetryIntervalSeconds));
                    }
                    else if (_options.EnableDeadLetterQueue)
                    {
                        // 加入死信队列
                        _deadLetterQueue.Enqueue(wrapper);
                        SaveDeadLetterToFile();
                    }
                }
            }

            if (_options.EnableFilePersistence)
            {
                SaveEventsToFile();
            }
        }

        #endregion

        #region 文件持久化

        private void LoadSubscriptionsFromFile()
        {
            lock (_fileLock)
            {
                try
                {
                    if (!File.Exists(_subscriptionFilePath))
                    {
                        return;
                    }

                    var json = File.ReadAllText(_subscriptionFilePath, Encoding.UTF8);
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        return;
                    }

                    var data = JsonSerializer.Deserialize<SubscriptionData>(json);
                    if (data != null)
                    {
                        // 注意：从文件加载时，处理器工厂无法恢复，需要重新订阅
                        Console.WriteLine($"SyZero.EventBus: 从文件加载订阅信息，共 {data.Subscriptions?.Count ?? 0} 个事件");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SyZero.EventBus: 从文件加载订阅信息失败: {ex.Message}");
                }
            }
        }

        private void SaveSubscriptionsToFile()
        {
            lock (_fileLock)
            {
                try
                {
                    var directory = Path.GetDirectoryName(_subscriptionFilePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    Dictionary<string, List<string>> subscriptions;
                    Dictionary<string, List<string>> dynamicSubscriptions;
                    lock (_subscriptionLock)
                    {
                        subscriptions = _subscriptions.ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value.Select(t => t.FullName).ToList());
                        dynamicSubscriptions = _dynamicSubscriptions.ToDictionary(
                            kvp => kvp.Key,
                            kvp => kvp.Value.Select(t => t.FullName).ToList());
                    }

                    var data = new SubscriptionData
                    {
                        Subscriptions = subscriptions,
                        DynamicSubscriptions = dynamicSubscriptions
                    };

                    var json = JsonSerializer.Serialize(data, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(_subscriptionFilePath, json, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SyZero.EventBus: 保存订阅信息到文件失败: {ex.Message}");
                }
            }
        }

        private void LoadEventsFromFile()
        {
            lock (_fileLock)
            {
                try
                {
                    if (!File.Exists(_eventFilePath))
                    {
                        return;
                    }

                    var json = File.ReadAllText(_eventFilePath, Encoding.UTF8);
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        return;
                    }

                    var events = JsonSerializer.Deserialize<List<EventWrapper>>(json);
                    if (events != null)
                    {
                        foreach (var evt in events)
                        {
                            _eventQueue.Enqueue(evt);
                        }
                        Console.WriteLine($"SyZero.EventBus: 从文件加载待处理事件，共 {events.Count} 个");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SyZero.EventBus: 从文件加载事件失败: {ex.Message}");
                }
            }
        }

        private void SaveEventsToFile()
        {
            lock (_fileLock)
            {
                try
                {
                    var directory = Path.GetDirectoryName(_eventFilePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    var events = _eventQueue.ToList();
                    var json = JsonSerializer.Serialize(events, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(_eventFilePath, json, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SyZero.EventBus: 保存事件到文件失败: {ex.Message}");
                }
            }
        }

        private void LoadDeadLetterFromFile()
        {
            lock (_fileLock)
            {
                try
                {
                    if (!File.Exists(_deadLetterFilePath))
                    {
                        return;
                    }

                    var json = File.ReadAllText(_deadLetterFilePath, Encoding.UTF8);
                    if (string.IsNullOrWhiteSpace(json))
                    {
                        return;
                    }

                    var events = JsonSerializer.Deserialize<List<EventWrapper>>(json);
                    if (events != null)
                    {
                        foreach (var evt in events)
                        {
                            _deadLetterQueue.Enqueue(evt);
                        }
                        Console.WriteLine($"SyZero.EventBus: 从文件加载死信事件，共 {events.Count} 个");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SyZero.EventBus: 从文件加载死信事件失败: {ex.Message}");
                }
            }
        }

        private void SaveDeadLetterToFile()
        {
            lock (_fileLock)
            {
                try
                {
                    var directory = Path.GetDirectoryName(_deadLetterFilePath);
                    if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                    {
                        Directory.CreateDirectory(directory);
                    }

                    var events = _deadLetterQueue.ToList();
                    var json = JsonSerializer.Serialize(events, new JsonSerializerOptions { WriteIndented = true });
                    File.WriteAllText(_deadLetterFilePath, json, Encoding.UTF8);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"SyZero.EventBus: 保存死信事件到文件失败: {ex.Message}");
                }
            }
        }

        #endregion

        #region 文件监听

        private void InitFileWatcher()
        {
            try
            {
                // 订阅文件监听
                var subscriptionDir = Path.GetDirectoryName(_subscriptionFilePath);
                var subscriptionFileName = Path.GetFileName(_subscriptionFilePath);

                if (!string.IsNullOrEmpty(subscriptionDir) && !string.IsNullOrEmpty(subscriptionFileName))
                {
                    if (!Directory.Exists(subscriptionDir))
                    {
                        Directory.CreateDirectory(subscriptionDir);
                    }

                    _subscriptionWatcher = new FileSystemWatcher(subscriptionDir, subscriptionFileName)
                    {
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
                    };
                    _subscriptionWatcher.Changed += OnSubscriptionFileChanged;
                    _subscriptionWatcher.EnableRaisingEvents = true;
                }

                // 事件文件监听
                var eventDir = Path.GetDirectoryName(_eventFilePath);
                var eventFileName = Path.GetFileName(_eventFilePath);

                if (!string.IsNullOrEmpty(eventDir) && !string.IsNullOrEmpty(eventFileName))
                {
                    if (!Directory.Exists(eventDir))
                    {
                        Directory.CreateDirectory(eventDir);
                    }

                    _eventWatcher = new FileSystemWatcher(eventDir, eventFileName)
                    {
                        NotifyFilter = NotifyFilters.LastWrite | NotifyFilters.Size
                    };
                    _eventWatcher.Changed += OnEventFileChanged;
                    _eventWatcher.EnableRaisingEvents = true;
                }

                Console.WriteLine("SyZero.EventBus: 文件监听已启动");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SyZero.EventBus: 初始化文件监听失败: {ex.Message}");
            }
        }

        private void OnSubscriptionFileChanged(object sender, FileSystemEventArgs e)
        {
            // 延迟一点，避免文件正在写入
            Thread.Sleep(100);
            LoadSubscriptionsFromFile();
        }

        private void OnEventFileChanged(object sender, FileSystemEventArgs e)
        {
            // 延迟一点，避免文件正在写入
            Thread.Sleep(100);
            LoadEventsFromFile();
        }

        #endregion

        #region 清理过期事件

        private async Task CleanExpiredEventsAsync()
        {
            try
            {
                var expireTime = DateTime.UtcNow.AddSeconds(-_options.EventExpireSeconds);
                var eventsToKeep = new List<EventWrapper>();

                while (_eventQueue.TryDequeue(out var wrapper))
                {
                    if (wrapper.CreateTime > expireTime)
                    {
                        eventsToKeep.Add(wrapper);
                    }
                }

                foreach (var evt in eventsToKeep)
                {
                    _eventQueue.Enqueue(evt);
                }

                if (_options.EnableFilePersistence)
                {
                    SaveEventsToFile();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SyZero.EventBus: 清理过期事件失败: {ex.Message}");
            }
        }

        #endregion

        #region 辅助方法

        private string GetEventKey<T>()
        {
            return typeof(T).Name;
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

        private async Task InvokeTypedHandlerAsync(string eventName, object eventData, Type handlerType)
        {
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

        private static async Task InvokeDynamicHandlerAsync(string eventName, object eventData, Type handlerType)
        {
            if (!(Activator.CreateInstance(handlerType) is IDynamicEventHandler handler))
            {
                return;
            }

            await handler.HandleAsync(eventName, eventData);
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
                    Console.WriteLine($"SyZero.EventBus: {operationName}异常: {ex.Message}");
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

            _subscriptionWatcher?.Dispose();
            _eventWatcher?.Dispose();
            _cleanupTimer?.Dispose();
            _processTimer?.Dispose();

            if (_options.EnableFilePersistence)
            {
                await Task.Run(() => {
                    SaveSubscriptionsToFile();
                    SaveEventsToFile();
                    SaveDeadLetterToFile();
                });
            }

            _disposed = true;
        }

        #endregion

        #region 内部类

        private class EventWrapper
        {
            public string EventName { get; set; }
            public object EventData { get; set; }
            public DateTime CreateTime { get; set; }
            public int RetryCount { get; set; }
            public DateTime? LastRetryTime { get; set; }
        }

        private class SubscriptionData
        {
            public Dictionary<string, List<string>> Subscriptions { get; set; }
            public Dictionary<string, List<string>> DynamicSubscriptions { get; set; }
        }

        #endregion
    }
}
