using System;
using System.Data.Entity.Migrations;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using MySql.Data.MySqlClient;
using Dark5.SDK.Core.Diagnostics;
using Dark5.SDK.Server.Communications;
using Dark5.SDK.Server.Controllers;
using Dark5.SDK.Server.Events;
using Dark5.Server.Configuration;
using Dark5.Server.Models;
using Dark5.Server.Storage;
using ServerConfiguration = Dark5.SDK.Server.Configuration.ServerConfiguration;

namespace Dark5.Server.Controllers
{
	public class DatabaseController : ConfigurableController<DatabaseConfiguration>
	{
		private readonly BootHistory currentBoot;

		public DatabaseController(ILogger logger, DatabaseConfiguration configuration, ICommunicationManager comms) : base(logger, configuration)
		{
			// Set global database options
			ServerConfiguration.DatabaseConnection = this.Configuration.Connection.ToString();
			ServerConfiguration.AutomaticMigrations = this.Configuration.Migrations.Automatic;

			// Enable SQL query logging
			MySqlTrace.Switch.Level = SourceLevels.All;
			MySqlTrace.Listeners.Add(new ConsoleTraceListener());

			BootHistory lastBoot;

			using (var context = new StorageContext())
			{
				// Create database if needed
				if (!context.Database.Exists())
				{
					this.Logger.Info($"No existing database found, creating new database \"{this.Configuration.Connection.Database}\"");
				}

				var migrator = new DbMigrator(new Migrations.Configuration());
				foreach (var migration in migrator.GetPendingMigrations())
				{
					this.Logger.Debug($"Running migration: {migration}");

					migrator.Update(migration);
				}

				lastBoot = context.BootHistory.OrderByDescending(b => b.Created).FirstOrDefault() ?? new BootHistory();

				this.currentBoot = new BootHistory();
				context.BootHistory.Add(this.currentBoot);
				context.SaveChanges();
			}

			Task.Factory.StartNew(UpdateBootHistory);

			comms.Event(BootEvents.GetTime).FromServer().On(e => e.Reply(this.currentBoot.Created));
			comms.Event(BootEvents.GetLastTime).FromServer().On(e => e.Reply(lastBoot.Created));
			comms.Event(BootEvents.GetLastActiveTime).FromServer().On(e => e.Reply(lastBoot.LastActive));
		}

		private async Task UpdateBootHistory()
		{
			while (true)
			{
				try
				{
					using (var context = new StorageContext())
					using (var transaction = context.Database.BeginTransaction())
					{
						this.currentBoot.LastActive = DateTime.UtcNow;
						context.BootHistory.AddOrUpdate(this.currentBoot);
						await context.SaveChangesAsync();
						transaction.Commit();
					}
				}
				catch (Exception ex)
				{
					this.Logger.Error(ex);
				}
				finally
				{
					await Task.Delay(this.Configuration.BootHistory.UpdateFrequency);
				}
			}
		}
	}
}
