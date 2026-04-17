using log4net;
using log4net.Config;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SyZero;
using SyZero.Log4Net;
using Xunit;

namespace SyZero.Tests;

[Collection("Log4Net")]
public sealed class Log4NetTests
{
    [Fact]
    public void Log4NetLoggerProvider_CreateLogger_UsesCategorySpecificLoggers()
    {
        var repositoryName = $"provider-{Guid.NewGuid():N}";
        var configPath = CreateConfigFile(
            """
            <log4net>
              <appender name="Console" type="log4net.Appender.ConsoleAppender">
                <layout type="log4net.Layout.PatternLayout">
                  <conversionPattern value="%message%newline" />
                </layout>
              </appender>
              <logger name="Category.A">
                <level value="INFO" />
                <appender-ref ref="Console" />
              </logger>
              <root>
                <level value="OFF" />
              </root>
            </log4net>
            """);

        try
        {
            var repository = LogManager.CreateRepository(repositoryName);
            XmlConfigurator.Configure(repository, new FileInfo(configPath));

            using var provider = new Log4NetLoggerProvider(repository);

            var loggerA = provider.CreateLogger("Category.A");
            var loggerB = provider.CreateLogger("Category.B");

            Assert.True(loggerA.IsEnabled(LogLevel.Information));
            Assert.False(loggerB.IsEnabled(LogLevel.Information));
            Assert.NotNull(LogManager.Exists(repositoryName, "Category.A"));
            Assert.NotNull(LogManager.Exists(repositoryName, "Category.B"));
        }
        finally
        {
            CleanupRepository(repositoryName, configPath);
        }
    }

    [Fact]
    public void AddSyZeroLog4Net_ThrowsWhenConfigFileDoesNotExist()
    {
        var missingFile = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"), "log4net.config");
        var services = new ServiceCollection();

        var exception = Assert.Throws<FileNotFoundException>(() =>
            services.AddLogging(builder => builder.AddSyZeroLog4Net(options =>
            {
                options.ConfigFile = missingFile;
                options.RepositoryName = $"missing-{Guid.NewGuid():N}";
            })));

        Assert.Equal(Path.GetFullPath(missingFile), exception.FileName);
    }

    [Fact]
    public void AddSyZeroLog4Net_ConfiguresNamedRepositoryAndEnablesLogging()
    {
        var repositoryName = $"extension-{Guid.NewGuid():N}";
        var configPath = CreateConfigFile(
            """
            <log4net>
              <appender name="Console" type="log4net.Appender.ConsoleAppender">
                <layout type="log4net.Layout.PatternLayout">
                  <conversionPattern value="%message%newline" />
                </layout>
              </appender>
              <root>
                <level value="INFO" />
                <appender-ref ref="Console" />
              </root>
            </log4net>
            """);

        try
        {
            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                builder.ClearProviders();
                builder.AddSyZeroLog4Net(options =>
                {
                    options.ConfigFile = configPath;
                    options.RepositoryName = repositoryName;
                    options.Watch = false;
                });
            });

            using var provider = services.BuildServiceProvider();
            var loggerFactory = provider.GetRequiredService<ILoggerFactory>();
            var logger = loggerFactory.CreateLogger("Category.Extension");
            var repository = LogManager.GetRepository(repositoryName);

            Assert.True(repository.Configured);
            Assert.True(logger.IsEnabled(LogLevel.Information));
            Assert.NotNull(LogManager.Exists(repositoryName, "Category.Extension"));
        }
        finally
        {
            CleanupRepository(repositoryName, configPath);
        }
    }

    private static string CreateConfigFile(string log4NetSection)
    {
        var directory = Path.Combine(Path.GetTempPath(), "syzero-log4net-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        var configPath = Path.Combine(directory, "log4net.config");
        File.WriteAllText(
            configPath,
            $$"""
            <?xml version="1.0" encoding="utf-8" ?>
            {{log4NetSection}}
            """);

        return configPath;
    }

    private static void CleanupRepository(string repositoryName, string configPath)
    {
        if (!string.IsNullOrWhiteSpace(repositoryName))
        {
            LogManager.ShutdownRepository(repositoryName);
        }

        var directory = Path.GetDirectoryName(configPath);
        if (!string.IsNullOrWhiteSpace(directory) && Directory.Exists(directory))
        {
            Directory.Delete(directory, true);
        }
    }
}
