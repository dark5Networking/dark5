using System.Collections.Generic;
using JetBrains.Annotations;
using Dark5.SDK.Core.Diagnostics;
using Dark5.SDK.Core.Models.Player;
using Dark5.SDK.Core.Plugins;

namespace Dark5.Server.Configuration
{
	[PublicAPI]
	public class ClientConfiguration
	{
		public User User { get; set; }

		public List<Plugin> Plugins { get; set; } = new List<Plugin>();

		public LogConfiguration Log { get; set; } = new LogConfiguration();

		[PublicAPI]
		public class LogConfiguration
		{
			public bool Mirror { get; set; } = true;

			public LogLevel Core { get; set; } = LogLevel.Info;

			public LogLevel Rpc { get; set; } = LogLevel.Info;

			public LogLevel Events { get; set; } = LogLevel.Info;

			public Dictionary<string, LogLevel> Plugins { get; set; } = new Dictionary<string, LogLevel>
			{
				{ "default", LogLevel.Info }
			};
		}
	}
}
