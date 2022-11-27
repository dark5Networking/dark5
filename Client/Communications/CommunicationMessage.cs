using System;
using JetBrains.Annotations;
using Dark5.Client.Events;
using Dark5.Client.Rpc;
using Dark5.SDK.Client.Communications;

namespace Dark5.Client.Communications
{
	[PublicAPI]
	public class CommunicationMessage : ICommunicationMessage
	{
		private readonly EventManager eventManager;

		private readonly bool networked;

		public Guid Id { get; } = Guid.NewGuid();

		public string Event { get; }

		public CommunicationMessage(string @event)
		{
			this.Event = @event;
		}

		public CommunicationMessage(string @event, EventManager eventManager) : this(@event)
		{
			this.eventManager = eventManager;
		}

		public CommunicationMessage(string @event, Guid id, bool networked) : this(@event)
		{
			this.Id = id;
			this.networked = networked;
		}

		public void Reply(params object[] payloads)
		{
			if (this.networked)
			{
				RpcManager.Emit($"{this.Id}:{this.Event}", payloads);
			}
			else
			{
				this.eventManager.Emit($"{this.Id}:{this.Event}", payloads);
			}
		}
	}
}
