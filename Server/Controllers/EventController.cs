using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Linq;
using System.Threading.Tasks;
using CitizenFX.Core;
using Dark5.SDK.Core.Diagnostics;
using Dark5.SDK.Core.Events;
using Dark5.SDK.Server.Communications;
using Dark5.SDK.Server.Controllers;
using Dark5.SDK.Server.Events;
using Dark5.Server.Diagnostics;
using Dark5.Server.Events;
using Dark5.Server.Rpc;

namespace Dark5.Server.Controllers
{
	public class EventController : Controller
	{
		private readonly ICommunicationManager comms;

		public EventController(ILogger logger, ICommunicationManager comms) : base(logger)
		{
			this.comms = comms;
		}

		public override Task Loaded()
		{
			// Client log mirroring
			this.comms.Event(CoreEvents.LogMirror).FromClients().On<DateTime, LogLevel, string, string>(OnLogMirror); // TODO: Const

			// Rebroadcast raw FiveM server events as wrapped server events
			RpcManager.OnRaw(FiveMServerEvents.PlayerConnecting, new Action<Player, string, CallbackDelegate, ExpandoObject>(OnPlayerConnectingRaw));
			RpcManager.OnRaw(FiveMServerEvents.PlayerDropped, new Action<Player, string>(OnPlayerDroppedRaw));

			RpcManager.OnRaw(FiveMServerEvents.ResourceStart, new Action<string>(OnResourceStartRaw));
			RpcManager.OnRaw(FiveMServerEvents.ResourceStop, new Action<string>(OnResourceStopRaw));

			// Deprecated by FiveM
			RpcManager.OnRaw(FiveMServerEvents.RconCommand, new Action<string, List<object>>(OnRconCommandRaw));

			// Requires FiveM server version >= 1543
			RpcManager.OnRaw(FiveMServerEvents.Explosion, new Action<string, dynamic>(OnExplosionEventRaw));

			return base.Loaded();
		}

		private static void OnLogMirror(ICommunicationMessage e, DateTime dt, LogLevel level, string prefix, string message)
		{
			new Logger(LogLevel.Trace, $"Client#{e.Client.Handle}|{prefix}").Log(message, level);
		}

		private void OnPlayerConnectingRaw([FromSource] Player player, string playerName, dynamic drop, dynamic deferrals)
		{
			this.Logger.Trace($"Triggered: {FiveMServerEvents.PlayerConnecting}");

			this.comms.Event(ServerEvents.PlayerConnecting).ToServer().Emit(new Client(player.Handle), new ConnectionDeferrals(deferrals));
		}

		private void OnPlayerDroppedRaw([FromSource] Player player, string reason)
		{
			this.Logger.Trace($"Triggered: {FiveMServerEvents.PlayerDropped}");

			this.comms.Event(ServerEvents.PlayerDropped).ToServer().Emit(new Client(player.Handle), reason);
		}

		private void OnResourceStartRaw(string resourceName)
		{
			this.Logger.Trace($"Triggered: {FiveMServerEvents.ResourceStart}");

			this.comms.Event(ServerEvents.ResourceStart).ToServer().Emit(resourceName);
		}

		private void OnResourceStopRaw(string resourceName)
		{
			this.Logger.Trace($"Triggered: {FiveMServerEvents.ResourceStop}");

			this.comms.Event(ServerEvents.ResourceStop).ToServer().Emit(resourceName);
		}

		private void OnRconCommandRaw(string command, List<object> args)
		{
			this.Logger.Trace($"Triggered: {FiveMServerEvents.RconCommand}");

			this.comms.Event(ServerEvents.RconCommand).ToServer().Emit(command, args.Select(a => a.ToString()).ToArray());
		}

		private void OnExplosionEventRaw(string source, dynamic args)
		{
			this.Logger.Trace($"Triggered: {FiveMServerEvents.Explosion}");

			this.comms.Event(ServerEvents.Explosion).ToServer().Emit(source, new ExplosionEvent(args));
		}
	}
}
