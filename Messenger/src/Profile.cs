namespace Oxide.Plugins
{
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;

    partial class Messenger
    {
        [JsonObject(MemberSerialization.OptIn)]
        public class Profile
        {
            [JsonProperty(PropertyName = "Steam ID")]
            public ulong SteamID { get; set; }

            [JsonProperty(PropertyName = "Name")]
            public string Name { get; set; }

            public BasePlayer Player => BasePlayer.FindByID(SteamID);

            [JsonProperty(PropertyName = "Image URL")]
            public string ImageUrl { get; set; }

            [JsonProperty(PropertyName = "Description")]
            public string Description { get; set; }

            [JsonProperty(PropertyName = "Last seen")]
            public DateTime LastSeen { get; set; } = DateTime.MinValue;

            public string Status { get
            {
                if (Player.IsAlive() && !Player.IsSleeping()) return "<color=#00ff00>Online</color>";
                else return $"<color=#ff0000>Last seen {PluginInstance.SomeTimeAgo(LastSeen)}</color>";
            } }

            [JsonProperty(PropertyName = "Conversation IDs")]
            public List<uint> ConversationIDs { get; set; } = new List<uint>();

            public List<Conversation> Conversations => ConversationData.getConversationsByProfile(this);

            public uint lastConversation { get; set; } = 0;

            public Profile() {}

            public Profile(BasePlayer player)
            {
                SteamID = player.userID;
                Name = player.displayName;
                PluginInstance.GetSteamUserData(SteamID, (ps) =>
                {
                    if (ps == null) return;
                    if (!string.IsNullOrEmpty(ps.avatarfull)) ImageUrl = ps.avatarfull;
                    ProfileData.save();
                });
            }
        }
    }
}