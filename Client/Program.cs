using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CitizenFX.Core;
using JetBrains.Annotations;
using Dark5.Client.Commands;
using Dark5.Client.Communications;
using Dark5.Client.Diagnostics;
using Dark5.Client.Events;
using Dark5.Client.Interface;
using Dark5.Client.Rpc;
using Dark5.SDK.Client;
using Dark5.SDK.Client.Configuration;
using Dark5.SDK.Client.Services;
using Dark5.SDK.Core.Diagnostics;
using Dark5.SDK.Core.Events;
using Dark5.SDK.Core.Models.Player;
using Dark5.SDK.Core.Plugins;

namespace Dark5.Client
{
	[UsedImplicitly]
	public class Program : BaseScript
	{
		private readonly Logger logger = new Logger();
		private readonly List<Service> services = new List<Service>();

		/// <summary>
		/// Primary client entry point.
		/// Initializes a new instance of the <see cref="Program" /> class.
		/// </summary>
		public Program()
		{
			try
			{
				Startup();
			}
			catch (Exception ex)
			{
				this.logger.Error(ex, "Fatal core exception");
			}
		}

		private async void Startup()
		{
			// Print exception messages in English
			// TODO: Test
			Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;

			var asm = GetType().Assembly;

			// Setup RPC handlers
			RpcManager.Configure(this.EventHandlers);

			var ticks = new TickManager(c => this.Tick += c, c => this.Tick -= c);
			var events = new EventManager();
			var comms = new CommunicationManager(events);
			var commands = new CommandManager(comms);
			var nui = new NuiManager(this.EventHandlers);

			// Initial connection
			var config = await comms.Event(CoreEvents.ClientInitialize).ToServer().Request<User, Tuple<LogLevel, LogLevel>, Tuple<List<string>, string>>(asm.GetName().Version.ToString());

			ClientConfiguration.Log.ConsoleLogLevel = config.Item2.Item1;
			ClientConfiguration.Log.MirrorLogLevel = config.Item2.Item2;
			ClientConfiguration.Locale.Culture = config.Item3.Item1.Select(c => new CultureInfo(c)).ToList();
			ClientConfiguration.Locale.TimeZone = TimeZoneInfo.Utc; // TODO: ??? + store IANA timezone

			// Use configured culture for output
			// TODO: Test
			Thread.CurrentThread.CurrentCulture = ClientConfiguration.Locale.Culture.First();

			// Configure overlays
			nui.Emit(new
			{
				@event = "dark5:config",
				data = new
				{
					locale = ClientConfiguration.Locale.Culture.First().Name,
					currency = new RegionInfo(ClientConfiguration.Locale.Culture.First().Name).ISOCurrencySymbol,
					timezone = config.Item3.Item2
				}
			});

			// Forward raw FiveM events
			//this.EventHandlers.Add("gameEventTriggered", new Action<string, List<object>>((s, a) => events.Emit("gameEventTriggered", s, a)));
			//this.EventHandlers.Add("populationPedCreating", new Action<float, float, float, uint, object>((x, y, z, model, setters) => events.Emit("populationPedCreating", new PedSpawnOptions(x, y, z, model, setters))));
			//RpcManager.OnRaw("onClientResourceStart", new Action<Player, string>(OnClientResourceStartRaw));
			//RpcManager.OnRaw("onClientResourceStop", new Action<Player, string>(OnClientResourceStopRaw));
			//RpcManager.OnRaw("gameEventTriggered", new Action<Player, string, List<dynamic>>(OnGameEventTriggeredRaw));
			//RpcManager.OnRaw(FiveMClientEvents.PopulationPedCreating, new Action<float, float, float, uint, IPopulationPedCreatingSetter>(OnPopulationPedCreatingRaw));

			// Provide raw BaseScript types to services
			Service.EventHandlers = this.EventHandlers;
			Service.Exports = this.Exports;
			Service.Players = this.Players;

			var plugins = await comms.Event(CoreEvents.ClientPlugins).ToServer().Request<List<Plugin>>();

			foreach (var assembly in AppDomain.CurrentDomain.GetAssemblies())
			{
				if (assembly.GetCustomAttribute<ClientPluginAttribute>() == null) continue;

				var plugin = plugins.FirstOrDefault(p => p.Client?.Main?.FirstOrDefault(m => $"{m}.net" == assembly.GetName().Name) != null);

				if (plugin == null)
				{
					this.logger.Warn($"Skipping {assembly.GetName().Name}, no client plugin");
					continue;
				}

				this.logger.Info(plugin.FullName);

				foreach (var type in assembly.GetTypes().Where(t => !t.IsAbstract && t.IsSubclassOf(typeof(Service))))
				{
					var service = (Service)Activator.CreateInstance(type, new Logger($"Plugin|{type.Name}"), ticks, comms, commands, new OverlayManager(plugin.Name, nui), config.Item1);
					await service.Loaded();

					this.services.Add(service);
				}
			}

			this.logger.Info("Plugins loaded");

			await Task.WhenAll(this.services.Select(s => s.Started()));

			this.logger.Info("Plugins started");

			comms.Event(CoreEvents.ClientInitialized).ToServer().Emit();

			this.logger.Info("Client ready");

			foreach (var service in this.services) await service.HoldFocus();
		}
	}
}
