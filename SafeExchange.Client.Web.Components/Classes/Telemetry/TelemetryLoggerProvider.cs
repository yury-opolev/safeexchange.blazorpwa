/// <summary>
/// SafeExchange
/// </summary>

namespace SafeExchange.Client.Web.Components;

using System;
using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;

/// <summary>
/// Pipes the standard ILogger pipeline into TelemetryService. Added to
/// the Blazor logging pipeline so Console.WriteLine-free structured
/// logs flow to App Insights alongside exceptions.
/// </summary>
public sealed class TelemetryLoggerProvider : ILoggerProvider
{
    private readonly IServiceProvider serviceProvider;
    private readonly ConcurrentDictionary<string, TelemetryLogger> loggers = new();

    public TelemetryLoggerProvider(IServiceProvider serviceProvider)
    {
        this.serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public ILogger CreateLogger(string categoryName)
    {
        return this.loggers.GetOrAdd(categoryName, name => new TelemetryLogger(name, this.serviceProvider));
    }

    public void Dispose()
    {
        this.loggers.Clear();
    }
}
