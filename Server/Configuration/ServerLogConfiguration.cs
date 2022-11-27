using JetBrains.Annotations;

namespace Dark5.Server.Configuration
{
	[PublicAPI]
	public static class ServerLogConfiguration
	{
		public static CoreConfiguration.LogOutputConfiguration Output { get; set; }
	}
}
