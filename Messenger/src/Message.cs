namespace Oxide.Plugins
{
    using Facepunch.Extend;
    using Newtonsoft.Json;
    using Oxide.Core.Libraries;
    using System;
    using System.Collections.Generic;
    using System.Text;
    partial class Messenger
    {
        [JsonObject(MemberSerialization.OptIn)]
        public class Message
        {
            [JsonProperty(PropertyName = "Timestamp")]
            public DateTime Timestamp { get; set; }

            [JsonProperty(PropertyName = "Sender SteamID")]
            public ulong SenderID { get; set; }

            [JsonProperty(PropertyName = "Sender Name")]
            public string SenderName { get; set; }

            public BasePlayer Sender => BasePlayer.FindByID(SenderID);

            [JsonProperty(PropertyName = "Conversation ID")]
            public uint ConversationID { get; set; }

            public Conversation Conversation => ConversationData.getConversation(ConversationID);

            [JsonProperty(PropertyName = "Content")]
            public string Content { get; set; }

            public Message() { }

            public Message(BasePlayer player, string content, Conversation conversation)
            {
                Timestamp = DateTime.Now;
                SenderID = player.userID;
                SenderName = player.displayName;
                ConversationID = conversation.ID;
                Content = content;
            }

            public void deliver()
            {
                StringBuilder sb1 = new StringBuilder();
                StringBuilder sb2 = new StringBuilder();
                foreach (ulong id in Conversation.Participants)
                {
                    Profile profile = ProfileData.getProfile(id);
                    BasePlayer player = profile.Player;
                    if (player == Sender) continue;
                    player.ChatMessage(string.Format(PluginInstance.lang.GetMessage(msg.message.ToString(), PluginInstance), SenderName, Content));
                    profile.lastConversation = Conversation.ID;
                    sb1.Append($" {player.displayName}[{player.userID}]");
                    sb2.Append($"\n{player.displayName}({profile.Status})");
                }
                string logEntry = $"{DateTime.Now.ToString("MM/dd/yyyy HH:mm:ss")} {SenderName}[{SenderID}] -> [{sb1.ToString().Trim()}]: \"{Content}\"";
                PluginInstance.Puts(logEntry);
                PluginInstance.LogToFile("Messages", logEntry , PluginInstance);
                PluginInstance.PrintToChat(Sender, PluginInstance.lang.GetMessage(msg.messageSent.ToString(), PluginInstance, Sender.UserIDString), Content, sb2);
                //Sender.ChatMessage($"Message: {Content} \ndelivered to {sb2}");
            }
        }
    }
}