namespace Oxide.Plugins
{
    using Newtonsoft.Json;
    using System.Collections.Generic;

    partial class Messenger
    {
        [JsonObject(MemberSerialization.OptIn)]
        public class Conversation
        {
            [JsonProperty(PropertyName = "ID")]
            public uint ID { get; set; }

            [JsonProperty(PropertyName = "Direct Messages")]
            public bool DM { get; set; } = false;

            [JsonProperty(PropertyName = "Participants")]
            public List<ulong> Participants { get; set; }

            [JsonProperty(PropertyName = "Messages")]
            public List<Message> Messages { get; set; } = new List<Message>();

            public Conversation() { }
        }
    }
}