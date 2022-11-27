using JetBrains.Annotations;
using Dark5.SDK.Server.Migrations;
using Dark5.Server.Storage;

namespace Dark5.Server.Migrations
{
	[PublicAPI]
	internal sealed class Configuration : MigrationConfiguration<StorageContext> { }
}
