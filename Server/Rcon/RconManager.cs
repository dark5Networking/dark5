using System;
using System.Collections.Generic;
using System.Linq;
using CitizenFX.Core.Native;
using Dark5.SDK.Core.Diagnostics;
using Dark5.SDK.Core.Plugins;
using Dark5.SDK.Plugins.Configuration;
using Dark5.SDK.Server.Communications;
using Dark5.SDK.Server.Controllers;
using Dark5.SDK.Server.Events;
using Dark5.SDK.Server.Rcon;
using Dark5.Server.Diagnostics;

namespace Dark5.Server.Rcon
{
	public class RconManager : IRconManager
	{
		private readonly Dictionary<string, Delegate> callbacks = new Dictionary<string, Delegate>();

		public Dictionary<Name, List<Controller>> Controllers { get; set; } = new Dictionary<Name, List<Controller>>();

		public RconManager(ICommunicationManager comms)
		{
			comms.Event(ServerEvents.RconCommand).FromServer().On<string, string[]>(OnCommand);
		}

		public void Register(string command, Action callback)
		{
			this.callbacks.Add(command.ToLowerInvariant(), new Action<object[]>(a => callback()));
		}

		public void Register(string command, Action<string> callback)
		{
			this.callbacks.Add(command.ToLowerInvariant(), new Action<object[]>(a => callback(string.Join(" ", a))));
		}

		public void Register(string command, Action<IEnumerable<string>> callback)
		{
			this.callbacks.Add(command.ToLowerInvariant(), new Action<object[]>(a => callback(a.Select(p => p.ToString()))));
		}

		private void OnCommand(ICommunicationMessage e, string command, string[] objArgs)
		{
			new Logger(LogLevel.Trace, "Rcon").Debug($"{command} {string.Join(" ", objArgs)}");

			if (this.callbacks.ContainsKey(command.ToLowerInvariant()))
			{
				this.callbacks[command].DynamicInvoke(objArgs.Cast<object>().ToArray());
				API.CancelEvent();
				return;
			}

			if (command.ToLowerInvariant() != "reload") return;

			try
			{
				var args = objArgs.Select(a => new Name(a)).ToList();
				if (args.Count == 0) args = this.Controllers.Keys.ToList();

				foreach (var pluginName in args)
				{
					if (!this.Controllers.ContainsKey(pluginName)) continue;

					foreach (var controller in this.Controllers[pluginName])
					{
						var controllerType = controller.GetType();

						if (controllerType.BaseType != null && controllerType.BaseType.IsGenericType && controllerType.BaseType.GetGenericTypeDefinition() == typeof(ConfigurableController<>))
						{
							controllerType.GetMethods().FirstOrDefault(m => m.DeclaringType == controllerType && m.Name == "Reload")?.Invoke(controller, new[] { ConfigurationManager.InitializeConfig(pluginName, controllerType.BaseType.GetGenericArguments()[0]) });
						}
						else
						{
							controller.Reload();
						}
					}
				}
			}
			catch (Exception)
			{
				// TODO
			}
			finally
			{
				API.CancelEvent();
			}
		}
	}
}
