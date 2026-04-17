# SyZero.Log4Net

SyZero.Log4Net integrates Log4Net into Microsoft.Extensions.Logging for classic XML-driven logging pipelines.

## Install

```bash
dotnet add package SyZero.Log4Net
```

## Key Features

- Log4Net provider integration
- Multi-appender output support
- XML-based configuration flexibility
- Compatible with existing log4net.config setups

## Quick Start

```csharp
builder.Logging.AddSyZeroLog4Net();
```

## Common Configuration

Use a `log4net.config` file in your application root (or output directory) and keep appenders, rolling strategy, and file patterns there.

```xml
<log4net>
	<appender name="RollingFile" type="log4net.Appender.RollingFileAppender">
		<file value="logs/app.log" />
		<appendToFile value="true" />
		<rollingStyle value="Date" />
		<datePattern value="yyyyMMdd'.log'" />
		<layout type="log4net.Layout.PatternLayout">
			<conversionPattern value="%date [%thread] %-5level %logger - %message%newline" />
		</layout>
	</appender>
	<root>
		<level value="INFO" />
		<appender-ref ref="RollingFile" />
	</root>
</log4net>
```

## Notes

- log4net.config (File, Appender, RollingStyle, DatePattern, etc.)

## References

- Chinese documentation: [/modules/SyZero.Log4Net](/modules/SyZero.Log4Net)


