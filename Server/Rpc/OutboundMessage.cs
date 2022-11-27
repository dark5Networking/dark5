using System;
using System.Collections.Generic;
using JetBrains.Annotations;
using Newtonsoft.Json;
using Dark5.SDK.Server.Communications;

namespace Dark5.Server.Rpc
{
	[PublicAPI]
	public class OutboundMessage
	{
		public Guid Id { get; set; }

		[JsonIgnore]
		public IClient Target { get; set; }

		public string Event { get; set; }

		public List<string> Payloads { get; set; } = new List<string>();

		public byte[] Pack() => RpcPacker.Pack(this);
	}
}
