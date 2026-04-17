using System.Reflection;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using RabbitMQ.Client;
using SyZero;
using SyZero.EventBus;
using SyZero.RabbitMQ;
using Xunit;

namespace SyZero.Tests;

public class RabbitMQTests
{
    [Fact]
    public void Subscribe_StartsConsumerOnlyOnce()
    {
        using var harness = CreateHarness();

        harness.Bus.Subscribe<TestEvent, RecordingHandler>(() => new RecordingHandler(_ => Task.CompletedTask));
        harness.Bus.SubscribeDynamic<RecordingDynamicHandler>(nameof(TestEvent));

        Assert.Equal(1, harness.BasicConsumeCount);
    }

    [Fact]
    public void Unsubscribe_RemovesBindingOnlyAfterLastSubscription()
    {
        using var harness = CreateHarness();

        harness.Bus.Subscribe<TestEvent, RecordingHandler>(() => new RecordingHandler(_ => Task.CompletedTask));
        harness.Bus.SubscribeDynamic<RecordingDynamicHandler>(nameof(TestEvent));

        harness.ResetInteractions();
        harness.Bus.Unsubscribe<TestEvent, RecordingHandler>();
        Assert.Equal(0, harness.QueueUnbindCount);

        harness.Bus.UnsubscribeDynamic<RecordingDynamicHandler>(nameof(TestEvent));
        Assert.Equal(1, harness.QueueUnbindCount);
    }

    [Fact]
    public void Dispose_DeletesGeneratedQueue()
    {
        var harness = CreateHarness(new RabbitMQEventBusOptions
        {
            EnableDeadLetter = false
        });

        harness.Dispose();

        harness.ChannelMock.Verify(channel => channel.QueueDelete(It.IsAny<string>(), false, false), Times.Once);
    }

    [Fact]
    public async Task Consumer_DoesNotAckOrNack_WhenAutoAckEnabled()
    {
        var handledValues = new List<int>();
        using var harness = CreateHarness(new RabbitMQEventBusOptions
        {
            QueueName = "auto-ack-queue",
            AutoAck = true,
            EnableDeadLetter = false
        });

        harness.Bus.Subscribe<TestEvent, RecordingHandler>(() => new RecordingHandler(evt =>
        {
            handledValues.Add(evt.Value);
            return Task.CompletedTask;
        }));

        await DeliverAsync(harness.CapturedConsumer, nameof(TestEvent), new TestEvent { Value = 7 });

        Assert.Equal(new[] { 7 }, handledValues);
        harness.ChannelMock.Verify(channel => channel.BasicAck(It.IsAny<ulong>(), It.IsAny<bool>()), Times.Never);
        harness.ChannelMock.Verify(channel => channel.BasicNack(It.IsAny<ulong>(), It.IsAny<bool>(), It.IsAny<bool>()), Times.Never);
    }

    [Fact]
    public async Task ProcessEvent_CreatesDynamicHandler_WhenItIsNotRegisteredInDi()
    {
        var values = new List<int>();
        RecordingDynamicHandler.Callback = (_, eventData) =>
        {
            values.Add(ReadValue(eventData));
            return Task.CompletedTask;
        };

        using var harness = CreateHarness();
        harness.Bus.SubscribeDynamic<RecordingDynamicHandler>(nameof(TestEvent));

        try
        {
            await InvokePrivateAsync(harness.Bus, "ProcessEvent", nameof(TestEvent), JsonSerializer.Serialize(new { Value = 11 }));
        }
        finally
        {
            RecordingDynamicHandler.Callback = null;
        }

        Assert.Equal(new[] { 11 }, values);
    }

    [Fact]
    public void AddRabbitMQEventBus_BindsConfiguration_AndNormalizesOptions()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [$"{RabbitMQEventBusOptions.SectionName}:HostName"] = "",
                [$"{RabbitMQEventBusOptions.SectionName}:Port"] = "0",
                [$"{RabbitMQEventBusOptions.SectionName}:QueueName"] = "orders",
                [$"{RabbitMQEventBusOptions.SectionName}:RequestedHeartbeat"] = "0"
            })
            .Build();

        var services = new ServiceCollection();
        services.AddRabbitMQEventBus(options => options.RetryCount = 0, configuration);

        using var provider = services.BuildServiceProvider();
        var options = provider.GetRequiredService<RabbitMQEventBusOptions>();

        Assert.Equal("localhost", options.HostName);
        Assert.Equal(5672, options.Port);
        Assert.Equal("orders", options.QueueName);
        Assert.Equal((ushort)60, options.RequestedHeartbeat);
        Assert.Equal(3, options.RetryCount);
    }

    [Fact]
    public void TryConnect_ReturnsExistingConnection_WhenAlreadyConnected()
    {
        var connectionMock = new Mock<IConnection>();
        connectionMock.SetupGet(connection => connection.IsOpen).Returns(true);
        connectionMock.SetupGet(connection => connection.Endpoint).Returns(new AmqpTcpEndpoint("localhost"));

        var factoryMock = new Mock<IConnectionFactory>();
        factoryMock.Setup(factory => factory.CreateConnection()).Returns(connectionMock.Object);

        using var connection = new RabbitMQPersistentConnection(
            factoryMock.Object,
            Mock.Of<ILogger<RabbitMQPersistentConnection>>(),
            retryCount: 1);

        Assert.True(connection.TryConnect());
        Assert.True(connection.TryConnect());
        factoryMock.Verify(factory => factory.CreateConnection(), Times.Once);
    }

    private static BusHarness CreateHarness(RabbitMQEventBusOptions? options = null, IServiceCollection? services = null)
    {
        var channelMock = new Mock<IModel>();
        channelMock.SetupGet(channel => channel.IsOpen).Returns(true);
        channelMock.Setup(channel => channel.CreateBasicProperties()).Returns(new Mock<IBasicProperties>().Object);

        var connectionMock = new Mock<IConnection>();
        connectionMock.SetupGet(connection => connection.IsOpen).Returns(true);
        connectionMock.SetupGet(connection => connection.Endpoint).Returns(new AmqpTcpEndpoint("localhost"));
        connectionMock.Setup(connection => connection.CreateModel()).Returns(channelMock.Object);

        var factoryMock = new Mock<IConnectionFactory>();
        factoryMock.Setup(factory => factory.CreateConnection()).Returns(connectionMock.Object);

        var persistentConnection = new RabbitMQPersistentConnection(
            factoryMock.Object,
            Mock.Of<ILogger<RabbitMQPersistentConnection>>(),
            retryCount: 1);

        var serviceProvider = (services ?? new ServiceCollection()).BuildServiceProvider();
        var busOptions = options ?? new RabbitMQEventBusOptions
        {
            QueueName = "test-queue",
            EnableDeadLetter = false
        };

        IBasicConsumer? capturedConsumer = null;
        var basicConsumeCount = 0;
        var queueUnbindCount = 0;

        channelMock.Setup(channel => channel.BasicConsume(
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<string>(),
                It.IsAny<bool>(),
                It.IsAny<bool>(),
                It.IsAny<IDictionary<string, object>>(),
                It.IsAny<IBasicConsumer>()))
            .Callback<string, bool, string, bool, bool, IDictionary<string, object>, IBasicConsumer>((_, _, _, _, _, _, consumer) =>
            {
                basicConsumeCount++;
                capturedConsumer = consumer;
            })
            .Returns("consumer-tag");
        channelMock.Setup(channel => channel.QueueUnbind(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<IDictionary<string, object>>()))
            .Callback(() => queueUnbindCount++);

        var bus = new RabbitMQEventBus(
            persistentConnection,
            Mock.Of<ILogger<RabbitMQEventBus>>(),
            serviceProvider,
            busOptions);

        return new BusHarness(bus, channelMock, () => capturedConsumer, () => basicConsumeCount, () => queueUnbindCount);
    }

    private static async Task DeliverAsync(IBasicConsumer? consumer, string eventName, object payload)
    {
        Assert.NotNull(consumer);

        var method = consumer!.GetType().GetMethod("HandleBasicDeliver", BindingFlags.Instance | BindingFlags.Public)
            ?? throw new MissingMethodException(consumer.GetType().FullName, "HandleBasicDeliver");

        var parameters = method.GetParameters();
        var arguments = new object?[parameters.Length];
        var body = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload));

        for (var i = 0; i < parameters.Length; i++)
        {
            var parameter = parameters[i];
            arguments[i] = parameter.Name switch
            {
                "consumerTag" => "consumer-tag",
                "deliveryTag" => 1UL,
                "redelivered" => false,
                "exchange" => "syzero_event_bus",
                "routingKey" => eventName,
                "basicProperties" => new Mock<IBasicProperties>().Object,
                "properties" => new Mock<IBasicProperties>().Object,
                "body" => CreateBodyArgument(parameter.ParameterType, body),
                _ => parameter.ParameterType.IsValueType ? Activator.CreateInstance(parameter.ParameterType) : null
            };
        }

        var result = method.Invoke(consumer, arguments);
        if (result is Task task)
        {
            await task;
        }
    }

    private static object CreateBodyArgument(Type parameterType, byte[] body)
    {
        if (parameterType == typeof(ReadOnlyMemory<byte>))
        {
            return new ReadOnlyMemory<byte>(body);
        }

        if (parameterType == typeof(byte[]))
        {
            return body;
        }

        throw new NotSupportedException($"Unsupported body parameter type: {parameterType.FullName}");
    }

    private static async Task InvokePrivateAsync(object instance, string methodName, params object[] arguments)
    {
        var method = instance.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new MissingMethodException(instance.GetType().FullName, methodName);
        await (Task)(method.Invoke(instance, arguments) ?? throw new InvalidOperationException($"{methodName} returned null."));
    }

    private static int ReadValue(object? eventData)
    {
        if (eventData is JsonElement jsonElement && jsonElement.TryGetProperty("Value", out var value))
        {
            return value.GetInt32();
        }

        return (int)(eventData?.GetType().GetProperty("Value")?.GetValue(eventData)
            ?? throw new InvalidOperationException("Missing Value property."));
    }

    private sealed class BusHarness : IDisposable
    {
        private readonly Func<IBasicConsumer?> _consumerAccessor;
        private readonly Func<int> _basicConsumeCountAccessor;
        private readonly Func<int> _queueUnbindCountAccessor;

        public BusHarness(
            RabbitMQEventBus bus,
            Mock<IModel> channelMock,
            Func<IBasicConsumer?> consumerAccessor,
            Func<int> basicConsumeCountAccessor,
            Func<int> queueUnbindCountAccessor)
        {
            Bus = bus;
            ChannelMock = channelMock;
            _consumerAccessor = consumerAccessor;
            _basicConsumeCountAccessor = basicConsumeCountAccessor;
            _queueUnbindCountAccessor = queueUnbindCountAccessor;
        }

        public RabbitMQEventBus Bus { get; }

        public Mock<IModel> ChannelMock { get; }

        public IBasicConsumer? CapturedConsumer => _consumerAccessor();

        public int BasicConsumeCount => _basicConsumeCountAccessor();

        public int QueueUnbindCount => _queueUnbindCountAccessor();

        public void ResetInteractions()
        {
            ChannelMock.Invocations.Clear();
        }

        public void Dispose()
        {
            Bus.Dispose();
        }
    }

    private sealed class TestEvent : EventBase
    {
        public int Value { get; set; }
    }

    private sealed class RecordingHandler : IEventHandler<TestEvent>
    {
        private readonly Func<TestEvent, Task> _callback;

        public RecordingHandler(Func<TestEvent, Task> callback)
        {
            _callback = callback;
        }

        public Task HandleAsync(TestEvent @event)
        {
            return _callback(@event);
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
}
