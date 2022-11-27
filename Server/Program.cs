using System;
using System.Collections.Generic;
using System.Data.Entity.Migrations;
using System.Data.Entity.Migrations.Infrastructure;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CitizenFX.Core;
using CitizenFX.Core.Native;
using JetBrains.Annotations;
using Dark5.SDK.Core.Diagnostics;
using Dark5.SDK.Core.Events;
using Dark5.SDK.Core.Plugins;
using Dark5.SDK.Plugins;
using Dark5.SDK.Plugins.Configuration;
using Dark5.SDK.Server;
using Dark5.SDK.Server.Communications;
using Dark5.SDK.Server.Configuration;
using Dark5.SDK.Server.Controllers;
using Dark5.SDK.Server.Events;
using Dark5.SDK.Server.Migrations;
using Dark5.SDK.Server.Rcon;
using Dark5.Server.Communications;
using Dark5.Server.Configuration;
using Dark5.Server.Controllers;
using Dark5.Server.Diagnostics;
using Dark5.Server.Events;
using Dark5.Server.IoC;
using Dark5.Server.Rcon;
using Dark5.Server.Rpc;

namespace Dark5.Server
{
	[UsedImplicitly]
	public class Program : BaseScript
	{
		private readonly Dictionary<Name, List<Controller>> controllers = new Dictionary<Name, List<Controller>>();

		public Program()
		{
			Startup();
		}

		private async void Startup()
		{
			// Print exception messages in English
			Thread.CurrentThread.CurrentUICulture = CultureInfo.InvariantCulture;
			CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.InvariantCulture;

			// Set the AppDomain working directory to the current resource root
			Environment.CurrentDirectory = Path.GetFullPath(API.GetResourcePath(API.GetCurrentResourceName()));

			new Logger().Info($"Dark5 {typeof(Program).Assembly.GetCustomAttributes<AssemblyInformationalVersionAttribute>().First().InformationalVersion}");

			// TODO: Check and warn if local CitizenFX.Core.Server.dll is found

			var config = ConfigurationManager.Load<CoreConfiguration>("core.yml");

			// Use configured culture for output
			Thread.CurrentThread.CurrentCulture = config.Locale.Culture.First();
			CultureInfo.DefaultThreadCurrentCulture = config.Locale.Culture.First();

			ServerConfiguration.Locale = config.Locale;
			ServerLogConfiguration.Output = config.Log.Output;

			var logger = new Logger(config.Log.Core);

			API.SetGameType(config.Display.Game);
			API.SetMapName(config.Display.Map);

			// Setup RPC handlers
			RpcManager.Configure(config.Log.Comms, this.EventHandlers, this.Players);

			var events = new EventManager(config.Log.Comms);
			var comms = new CommunicationManager(events);
			var rcon = new RconManager(comms);

			// Load core controllers
			try
			{
				var dbController = new DatabaseController(new Logger(config.Log.Core, "Database"), ConfigurationManager.Load<DatabaseConfiguration>("database.yml"), comms);
				await dbController.Loaded();
				this.controllers.Add(new Name("Dark5/Database"), new List<Controller> { dbController });
			}
			catch (Exception ex)
			{
				logger.Error(ex, "Database connection error");
				logger.Warn("Fatal error, exiting");
				Environment.Exit(1);
			}

			var eventController = new EventController(new Logger(config.Log.Core, "FiveM"), comms);
			await eventController.Loaded();
			this.controllers.Add(new Name("Dark5/RawEvents"), new List<Controller> { eventController });

			var sessionController = new SessionController(new Logger(config.Log.Core, "Session"), ConfigurationManager.Load<SessionConfiguration>("session.yml"), comms);
			await sessionController.Loaded();
			this.controllers.Add(new Name("Dark5/Session"), new List<Controller> { sessionController });

			// Resolve dependencies
			var graph = DefinitionGraph.Load();

			// IoC
			var assemblies = new List<Assembly>();
			assemblies.AddRange(graph.Plugins.Where(p => p.Server?.Include != null).SelectMany(p => p.Server.Include.Select(i => Assembly.LoadFrom(Path.Combine("plugins", p.Name.Vendor, p.Name.Project, $"{i}.net.dll")))));
			assemblies.AddRange(graph.Plugins.Where(p => p.Server?.Main != null).SelectMany(p => p.Server.Main.Select(m => Assembly.LoadFrom(Path.Combine("plugins", p.Name.Vendor, p.Name.Project, $"{m}.net.dll")))));

			var registrar = new ContainerRegistrar();
			registrar.RegisterService<ILogger>(s => new Logger());
			registrar.RegisterInstance<IRconManager>(rcon);
			registrar.RegisterInstance<IBaseScriptProxy>(new BaseScriptProxy(this.EventHandlers, this.Exports, this.Players));
			registrar.RegisterInstance<ICommunicationManager>(comms);
			registrar.RegisterInstance<IClientList>(new ClientList(new Logger(config.Log.Core, "ClientList"), comms));
			registrar.RegisterPluginComponents(assemblies.Distinct());

			// DI
			var container = registrar.Build();

			var pluginDefaultLogLevel = config.Log.Plugins.ContainsKey("default") ? config.Log.Plugins["default"] : LogLevel.Info;

			// Load plugins into the AppDomain
			foreach (var plugin in graph.Plugins)
			{
				logger.Info($"Loading {plugin.FullName}");

				// Load include files
				foreach (var includeName in plugin.Server?.Include ?? new List<string>())
				{
					var includeFile = Path.Combine("plugins", plugin.Name.Vendor, plugin.Name.Project, $"{includeName}.net.dll");
					if (!File.Exists(includeFile)) throw new FileNotFoundException(includeFile);

					AppDomain.CurrentDomain.Load(File.ReadAllBytes(includeFile));
				}

				// Load main files
				foreach (var mainName in plugin.Server?.Main ?? new List<string>())
				{
					var mainFile = Path.Combine("plugins", plugin.Name.Vendor, plugin.Name.Project, $"{mainName}.net.dll");
					if (!File.Exists(mainFile)) throw new FileNotFoundException(mainFile);

					var asm = Assembly.LoadFrom(mainFile);

					var sdkVersion = asm.GetCustomAttribute<ServerPluginAttribute>();

					if (sdkVersion == null)
					{
						throw new Exception("Unable to load outdated SDK plugin"); // TODO
					}

					if (sdkVersion.Target != SDK.Server.SDK.Version)
					{
						throw new Exception("Unable to load outdated SDK plugin");
					}

					var types = Assembly.LoadFrom(mainFile).GetTypes().Where(t => !t.IsAbstract && t.IsClass).ToList();

					// Find migrations
					foreach (var migrationType in types.Where(t => t.BaseType != null && t.BaseType.IsGenericType && t.BaseType.GetGenericTypeDefinition() == typeof(MigrationConfiguration<>)))
					{
						var configuration = (DbMigrationsConfiguration)Activator.CreateInstance(migrationType);
						var migrator = new DbMigrator(configuration);

						if (!migrator.GetPendingMigrations().Any()) continue;

						if (!ServerConfiguration.AutomaticMigrations) throw new MigrationsPendingException($"Plugin {plugin.FullName} has pending migrations but automatic migrations are disabled");

						foreach (var migration in migrator.GetPendingMigrations())
						{
							new Logger(config.Log.Core, "Database").Debug($"[{mainName}] Running migration: {migration}");

							migrator.Update(migration);
						}
					}

					// Find controllers
					foreach (var controllerType in types.Where(t => t.IsSubclassOf(typeof(Controller)) || t.IsSubclassOf(typeof(ConfigurableController<>))))
					{
						var logLevel = config.Log.Plugins.ContainsKey(plugin.Name) ? config.Log.Plugins[plugin.Name] : pluginDefaultLogLevel;

						var constructorArgs = new List<object>
						{
							new Logger(logLevel, plugin.Name)
						};

						// Check if controller is configurable
						if (controllerType.BaseType != null && controllerType.BaseType.IsGenericType && controllerType.BaseType.GetGenericTypeDefinition() == typeof(ConfigurableController<>))
						{
							// Initialize the controller configuration
							constructorArgs.Add(ConfigurationManager.InitializeConfig(plugin.Name, controllerType.BaseType.GetGenericArguments()[0]));
						}

						// Resolve IoC arguments
						constructorArgs.AddRange(controllerType.GetConstructors()[0].GetParameters().Skip(constructorArgs.Count).Select(p => container.Resolve(p.ParameterType)));

						Controller controller = null;

						try
						{
							// Construct controller instance
							controller = (Controller)Activator.CreateInstance(controllerType, constructorArgs.ToArray());
						}
						catch (Exception ex)
						{
							// TODO: Dispose of controller

							logger.Error(ex, $"Unhandled exception in plugin {plugin.FullName}");
						}

						if (controller == null) continue;

						try
						{
							await controller.Loaded();

							if (!this.controllers.ContainsKey(plugin.Name)) this.controllers.Add(plugin.Name, new List<Controller>());
							this.controllers[plugin.Name].Add(controller);
						}
						catch (Exception ex)
						{
							// TODO: Dispose of controller

							logger.Error(ex, $"Unhandled exception loading plugin {plugin.FullName}");
						}
					}
				}
			}

			await Task.WhenAll(this.controllers.SelectMany(c => c.Value).Select(s => s.Started()));

			rcon.Controllers = this.controllers;

			comms.Event(CoreEvents.ClientPlugins).FromClients().OnRequest(e => e.Reply(graph.Plugins));

			logger.Debug($"{graph.Plugins.Count.ToString(CultureInfo.InvariantCulture)} plugin(s) loaded, {this.controllers.Count.ToString(CultureInfo.InvariantCulture)} controller(s) created");

			comms.Event(ServerEvents.ServerInitialized).ToServer().Emit();

			logger.Info("Server ready");
		}
	}
}
