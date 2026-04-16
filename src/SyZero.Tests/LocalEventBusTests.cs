using SyZero.EventBus;
using SyZero.EventBus.LocalEventBus;
using Xunit;

namespace SyZero.Tests;

public class LocalEventBusTests
{
    [Fact]
    public async Task PublishAsync_UsesHandlerDeclaredEventType_AndDispatchesDynamicHandlers()
    {
        var typedValues = new List<int>();
        var dynamicValues = new List<int>();
        RecordingDynamicHandler.Callback = (eventName, eventData) =>
        {
            dynamicValues.Add(ReadValue(eventData));
            return Task.CompletedTask;
        };

        await using var bus = new LocalEventBus(new LocalEventBusOptions
        {
            EnableAsync = false,
            EnableFilePersistence = false,
            EnableFileWatcher = false,
            AutoCleanExpiredEvents = false
        });

        bus.Subscribe<TestEvent, RecordingHandler>(() => new RecordingHandler(testEvent => typedValues.Add(testEvent.Value)));
        bus.SubscribeDynamic<RecordingDynamicHandler>(nameof(TestEvent));

        await bus.PublishAsync(nameof(TestEvent), new { Value = 7 });

        Assert.Equal(new[] { 7 }, typedValues);
        Assert.Equal(new[] { 7 }, dynamicValues);
    }

    [Fact]
    public async Task ProcessEventsAsync_TimesOutAndMovesEventToDeadLetterQueue()
    {
        var fileToken = Guid.NewGuid().ToString("N");
        var deadLetterPath = Path.Combine(Path.GetTempPath(), $"{fileToken}-dead.json");
        var eventPath = Path.Combine(Path.GetTempPath(), $"{fileToken}-events.json");
        var subscriptionPath = Path.Combine(Path.GetTempPath(), $"{fileToken}-subscriptions.json");

        await using var bus = new LocalEventBus(new LocalEventBusOptions
        {
            EnableAsync = true,
            EnableRetry = true,
            RetryCount = 1,
            RetryIntervalSeconds = 0,
            EnableDeadLetterQueue = true,
            EnableFilePersistence = true,
            EnableFileWatcher = false,
            AutoCleanExpiredEvents = false,
            EventHandlerTimeoutSeconds = 1,
            DeadLetterFilePath = deadLetterPath,
            EventFilePath = eventPath,
            SubscriptionFilePath = subscriptionPath
        });

        DisposeProcessTimer(bus);
        bus.Subscribe<TestEvent, SlowHandler>(() => new SlowHandler());
        bus.Publish(new TestEvent { Value = 1 });

        await InvokePrivateAsync(bus, "ProcessEventsAsync");

        Assert.True(File.Exists(deadLetterPath));
        Assert.Contains(nameof(TestEvent), await File.ReadAllTextAsync(deadLetterPath));
    }

    private static int ReadValue(object? eventData)
    {
        return (int)(eventData?.GetType().GetProperty("Value")?.GetValue(eventData)
            ?? throw new InvalidOperationException("Missing Value property."));
    }

    private static void DisposeProcessTimer(LocalEventBus bus)
    {
        var timerField = typeof(LocalEventBus).GetField("_processTimer", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        (timerField?.GetValue(bus) as Timer)?.Dispose();
    }

    private static async Task InvokePrivateAsync(object instance, string methodName)
    {
        var method = instance.GetType().GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
        await (Task)(method.Invoke(instance, null) ?? throw new InvalidOperationException($"{methodName} returned null."));
    }

    private sealed class TestEvent : EventBase
    {
        public int Value { get; set; }
    }

    private sealed class RecordingHandler : IEventHandler<TestEvent>
    {
        private readonly Action<TestEvent> _callback;

        public RecordingHandler(Action<TestEvent> callback)
        {
            _callback = callback;
        }

        public Task HandleAsync(TestEvent @event)
        {
            _callback(@event);
            return Task.CompletedTask;
        }
    }

    private sealed class RecordingDynamicHandler : IDynamicEventHandler
    {
        public static Func<string, object?, Task>? Callback { get; set; }

        public Task HandleAsync(string eventName, dynamic eventData)
        {
            return Callback?.Invoke(eventName, (object?)eventData) ?? Task.CompletedTask;
        }
    }

    private sealed class SlowHandler : IEventHandler<TestEvent>
    {
        public async Task HandleAsync(TestEvent @event)
        {
            await Task.Delay(TimeSpan.FromSeconds(2));
        }
    }
}
