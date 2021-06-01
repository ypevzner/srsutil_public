using Microsoft.Data.Entity;
using Microsoft.Data.Entity.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System;

namespace FDA.SRS.Database
{
	public static class DbContextExtensions
	{
		public static void LogToConsole(this DbContext context, LogLevel level)
		{
			var serviceProvider = context.GetInfrastructure<IServiceProvider>();
			var loggerFactory = serviceProvider.GetService<ILoggerFactory>();
			loggerFactory.AddProvider(new ConsoleLogger());
		}
	}
}
