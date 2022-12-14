using System;
using System.ComponentModel.DataAnnotations;
using JetBrains.Annotations;
using Dark5.SDK.Core.Helpers;

namespace Dark5.Server.Models
{
	[PublicAPI]
	public class BootHistory
	{
		[Key]
		[Required]
		public Guid Id { get; set; }

		[Required]
		public DateTime Created { get; set; }

		public DateTime LastActive { get; set; }

		public BootHistory()
		{
			this.Id = GuidGenerator.GenerateTimeBasedGuid();
			this.Created = DateTime.UtcNow;
			this.LastActive = DateTime.UtcNow;
		}
	}
}
