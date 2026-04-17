using log4net;
using log4net.Appender;
using log4net.Config;
using log4net.Core;
using log4net.Repository;
using Microsoft.Extensions.Logging;
using SyZero.Log4Net;
using System;
using System.IO;
using System.Linq;

namespace SyZero
{
    public static class SyZeroLog4NetExtension
    {
        /// <summary>
        /// 注册 Log4Net 日志提供程序。
        /// </summary>
        public static ILoggingBuilder AddSyZeroLog4Net(this ILoggingBuilder builder)
        {
            return builder.AddSyZeroLog4Net(_ => { });
        }

        /// <summary>
        /// 注册 Log4Net 日志提供程序并指定配置文件。
        /// </summary>
        public static ILoggingBuilder AddSyZeroLog4Net(this ILoggingBuilder builder, string configFile)
        {
            return builder.AddSyZeroLog4Net(options => options.ConfigFile = configFile);
        }

        /// <summary>
        /// 注册 Log4Net 日志提供程序并允许自定义选项。
        /// </summary>
        public static ILoggingBuilder AddSyZeroLog4Net(this ILoggingBuilder builder, Action<SyZeroLog4NetOptions> optionsAction)
        {
            if (builder == null)
            {
                throw new ArgumentNullException(nameof(builder));
            }

            var options = new SyZeroLog4NetOptions();
            optionsAction?.Invoke(options);
            options.Validate();

            var fileInfo = new FileInfo(Path.GetFullPath(options.ConfigFile));
            if (!fileInfo.Exists)
            {
                throw new FileNotFoundException($"The log4net configuration file was not found: {fileInfo.FullName}", fileInfo.FullName);
            }

            var repository = GetOrCreateRepository(options.RepositoryName);
            if (options.Watch)
            {
                XmlConfigurator.ConfigureAndWatch(repository, fileInfo);
            }
            else
            {
                XmlConfigurator.Configure(repository, fileInfo);
            }

#if DEBUG
            foreach (var appender in repository.GetAppenders().OfType<RollingFileAppender>())
            {
                appender.ImmediateFlush = true;
                appender.ActivateOptions();
            }
#endif

            builder.AddProvider(new Log4NetLoggerProvider(repository));
            return builder;
        }

        private static ILoggerRepository GetOrCreateRepository(string repositoryName)
        {
            if (string.IsNullOrWhiteSpace(repositoryName))
            {
                return LogManager.GetRepository();
            }

            try
            {
                return LogManager.GetRepository(repositoryName);
            }
            catch (LogException)
            {
                return LogManager.CreateRepository(repositoryName);
            }
        }
    }
}
