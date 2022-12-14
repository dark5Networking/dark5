using System;
using Dark5.SDK.Server.Communications;
using Dark5.Server.Rpc;

namespace Dark5.Server.Communications
{
	public class CommunicationReceiveClient : ICommunicationReceiveClient
	{
		private readonly string @event;
		private readonly IClient target;

		public CommunicationReceiveClient(string @event)
		{
			this.@event = @event;
		}

		public CommunicationReceiveClient(string @event, IClient client) : this(@event)
		{
			this.target = client;
		}

		public void On(Action<ICommunicationMessage> callback) => RpcManager.On(this.@event, this.target, callback);

		public void On<T>(Action<ICommunicationMessage, T> callback) => RpcManager.On(this.@event, this.target, callback);

		public void On<T1, T2>(Action<ICommunicationMessage, T1, T2> callback) => RpcManager.On(this.@event, this.target, callback);

		public void On<T1, T2, T3>(Action<ICommunicationMessage, T1, T2, T3> callback) => RpcManager.On(this.@event, this.target, callback);

		public void On<T1, T2, T3, T4>(Action<ICommunicationMessage, T1, T2, T3, T4> callback) => RpcManager.On(this.@event, this.target, callback);

		public void On<T1, T2, T3, T4, T5>(Action<ICommunicationMessage, T1, T2, T3, T4, T5> callback) => RpcManager.On(this.@event, this.target, callback);

		public void OnRequest(Action<ICommunicationMessage> callback) => RpcManager.OnRequest(this.@event, this.target, callback);

		public void OnRequest<T>(Action<ICommunicationMessage, T> callback) => RpcManager.OnRequest(this.@event, this.target, callback);

		public void OnRequest<T1, T2>(Action<ICommunicationMessage, T1, T2> callback) => RpcManager.OnRequest(this.@event, this.target, callback);

		public void OnRequest<T1, T2, T3>(Action<ICommunicationMessage, T1, T2, T3> callback) => RpcManager.OnRequest(this.@event, this.target, callback);

		public void OnRequest<T1, T2, T3, T4>(Action<ICommunicationMessage, T1, T2, T3, T4> callback) => RpcManager.OnRequest(this.@event, this.target, callback);

		public void OnRequest<T1, T2, T3, T4, T5>(Action<ICommunicationMessage, T1, T2, T3, T4, T5> callback) => RpcManager.OnRequest(this.@event, this.target, callback);
	}
}
