/// <summary>
/// SafeExchange
/// </summary>

namespace SafeExchange.Client.Web.Components;

using System;
using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

public sealed class TelemetryLogger : ILogger
{
    private readonly string categoryName;
    private readonly IServiceProvider serviceProvider;

    public TelemetryLogger(string categoryName, IServiceProvider serviceProvider)
    {
        this.categoryName = categoryName;
        this.serviceProvider = serviceProvider;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull
    {
        return null;
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        // Warnings and above are routed to telemetry. Info/Debug stay in
        // the console to keep volume bounded and limit leakage of noisy
        // implementation detail into the backend. Upgrade the threshold
        // via appsettings Logging:LogLevel if a given subsystem needs
        // more detail.
        return logLevel >= LogLevel.Warning;
    }

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!this.IsEnabled(logLevel))
        {
            return;
        }

        var telemetryService = this.serviceProvider.GetService<TelemetryService>();
        if (telemetryService is null || !telemetryService.IsEnabled)
        {
            return;
        }

        var message = formatter(state, exception);
        var properties = new Dictionary<string, string>
        {
            ["category"] = this.categoryName,
            ["eventId"] = eventId.Id.ToString()
        };

        if (exception is not null)
        {
            _ = telemetryService.TrackExceptionAsync(exception, properties);
        }
        else
        {
            var severity = MapSeverity(logLevel);
            _ = telemetryService.TrackTraceAsync(message, severity, properties);
        }
    }

    private static LogSeverityLevel MapSeverity(LogLevel level) => level switch
    {
        LogLevel.Trace => LogSeverityLevel.Verbose,
        LogLevel.Debug => LogSeverityLevel.Verbose,
        LogLevel.Information => LogSeverityLevel.Information,
        LogLevel.Warning => LogSeverityLevel.Warning,
        LogLevel.Error => LogSeverityLevel.Error,
        LogLevel.Critical => LogSeverityLevel.Critical,
        _ => LogSeverityLevel.Information
    };
}
