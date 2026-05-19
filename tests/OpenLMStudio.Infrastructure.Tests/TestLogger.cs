global using System;
global using System.IO;
global using System.Threading;
global using System.Threading.Tasks;

using Microsoft.Extensions.Logging;

namespace OpenLMStudio.Infrastructure.Tests;

/// <summary>
/// A no-op ILogger implementation for unit tests.
/// </summary>
public class TestLogger<T> : ILogger<T>
{
    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
    public bool IsEnabled(LogLevel logLevel) => true;
    public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) { }
}