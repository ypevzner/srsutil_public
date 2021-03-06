using Microsoft.Extensions.Logging;
using System;

namespace FDA.SRS.Database
{
	public class ConsoleLogger : ILoggerProvider
	{
		public ILogger CreateLogger(string categoryName)
		{
			return new SimpleConsoleLogger();
		}

		public void Dispose()
		{
		}

		private class SimpleConsoleLogger : ILogger
		{
			public IDisposable BeginScope<TState>(TState state)
			{
				throw new NotImplementedException();
			}

			public bool IsEnabled(LogLevel logLevel)
			{
				return true;
			}

			public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception exception, Func<TState, Exception, string> formatter)
			{
				throw new NotImplementedException();
			}
		}
	}
}
