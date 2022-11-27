using CitizenFX.Core.Native;
using Dark5.SDK.Core.Models;
using Dark5.SDK.Server.Events;

namespace Dark5.Server.Events
{
	public class ExplosionEvent : IExplosionEvent
	{
		public int OwnerNetId { get; }

		public int ExplosionType { get; }

		public float DamageScale { get; }

		public float CameraShake { get; }

		public Position Position { get; }

		public bool IsAudible { get; }

		public bool IsInvisible { get; }

		public ExplosionEvent(dynamic @event)
		{
			this.OwnerNetId = @event.ownerNetId;
			this.ExplosionType = @event.explosionType;
			this.DamageScale = @event.damageScale;
			this.CameraShake = @event.cameraShake;
			this.Position = new Position(@event.posX, @event.posY, @event.posZ);
			this.IsAudible = @event.isAudible;
			this.IsInvisible = @event.isInvisible;
		}

		// TODO: Test
		public void Cancel()
		{
			API.CancelEvent();
		}
	}
}
