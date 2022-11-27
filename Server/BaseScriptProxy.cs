using CitizenFX.Core;
using Dark5.SDK.Server;

namespace Dark5.Server
{
	internal class BaseScriptProxy : IBaseScriptProxy
	{
		public EventHandlerDictionary EventHandlers { get; }

		public ExportDictionary Exports { get; }

		public PlayerList Players { get; }

		public BaseScriptProxy(EventHandlerDictionary eventHandlers, ExportDictionary exports, PlayerList players)
		{
			this.EventHandlers = eventHandlers;
			this.Exports = exports;
			this.Players = players;
		}
	}
}
