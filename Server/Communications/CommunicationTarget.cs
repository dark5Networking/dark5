using Dark5.SDK.Server.Communications;
using Dark5.Server.Events;

namespace Dark5.Server.Communications
{
	public class CommunicationTarget : ICommunicationTarget
	{
		private readonly EventManager eventManager;

		public string Event { get; }

		public CommunicationTarget(EventManager eventManager, string @event)
		{
			this.eventManager = eventManager;
			this.Event = @event;
		}

		public ICommunicationTransmitClient ToClient(IClient client) => new CommunicationTransmitClient(this.Event, client);

		public ICommunicationReceiveClient FromClient(IClient client) => new CommunicationReceiveClient(this.Event, client);

		public ICommunicationTransmitClient ToClients() => new CommunicationTransmitClient(this.Event);

		public ICommunicationReceiveClient FromClients() => new CommunicationReceiveClient(this.Event);

		public ICommunicationTransmitServer ToServer() => new CommunicationTransmitServer(this.Event, this.eventManager);

		public ICommunicationReceiveServer FromServer() => new CommunicationReceiveServer(this.Event, this.eventManager);
	}
}
