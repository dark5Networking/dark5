using System.Data.Entity;
using JetBrains.Annotations;
using Dark5.SDK.Core.Models.Player;
using Dark5.SDK.Server.Storage;
using Dark5.Server.Models;

namespace Dark5.Server.Storage
{
	[PublicAPI]
	public class StorageContext : EFContext<StorageContext>
	{
		public DbSet<User> Users { get; set; }

		public DbSet<Session> Sessions { get; set; }

		public DbSet<BootHistory> BootHistory { get; set; }

		protected override void OnModelCreating(DbModelBuilder modelBuilder)
		{
			base.OnModelCreating(modelBuilder);

			modelBuilder.Entity<User>().HasIndex(u => u.License).IsUnique();
		}
	}
}
