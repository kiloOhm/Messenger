// Requires: GUICreator

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Messenger", "OHM", "0.1.0")]
    [Description("Template")]
    partial class Messenger : RustPlugin
    {
        private static Messenger PluginInstance;

        public Messenger()
        {
            PluginInstance = this;
        }

        #region helpers

        public string SomeTimeAgo(DateTime dateTime)
        {
            if (dateTime == DateTime.MinValue) return null;
            TimeSpan timeSince = DateTime.Now.Subtract(dateTime);

            if (timeSince.TotalDays < 1)
            {
                if (timeSince.TotalHours < 1)
                {
                    if (timeSince.TotalMinutes < 1) return $"{timeSince.TotalSeconds} seconds ago";
                    else return $"{timeSince.TotalMinutes} minutes ago";
                }
                else return $"{timeSince.TotalHours} hours ago";
            }
            else return $"{timeSince.TotalDays} days ago";
        }

        public BasePlayer findPlayer(string name, BasePlayer player)
        {
            if (string.IsNullOrEmpty(name)) return null;
            ulong id;
            ulong.TryParse(name, out id);
            List<BasePlayer> results = BasePlayer.allPlayerList.Where((p) => p.displayName.Contains(name, System.Globalization.CompareOptions.IgnoreCase) || p.userID == id).ToList();
            if (results.Count == 0)
            {
                if (player != null) player.ChatMessage(lang.GetMessage(msg.noPlayersFound.ToString(), this, player.UserIDString));
            }
            else if (results.Count == 1)
            {
                return results[0];
            }
            else if (player != null)
            {
                player.ChatMessage(lang.GetMessage(msg.multiplePlayersFound.ToString(), this, player.UserIDString));
                int i = 1;
                foreach (BasePlayer p in results)
                {
                    player.ChatMessage($"{i}. {p.displayName}[{p.userID}]");
                    i++;
                }
            }
            return null;
        }

        #endregion
    }
}﻿namespace Oxide.Plugins
{
    using Facepunch.Extend;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using UnityEngine;

    partial class Messenger : RustPlugin
    {
        partial void initCommands()
        {
            cmd.AddChatCommand("pm", this, nameof(pmCommand));
            cmd.AddChatCommand("dm", this, nameof(pmCommand));
            cmd.AddChatCommand("reply", this, nameof(replyCommand));
            cmd.AddChatCommand("r", this, nameof(replyCommand));
        }

        private void pmCommand(BasePlayer player, string command, string[] args)
        {
            if (!hasPermission(player, permissions.use))
            {
                PrintToChat(player, lang.GetMessage("noPermission", this, player.UserIDString));
                return;
            }

            if(args.Length < 2)
            {
                PrintToChat(player, lang.GetMessage(msg.invalidArguments.ToString(), this, player.UserIDString));
                return;
            }

            BasePlayer recipient = findPlayer(args[0], player);
            if (recipient == null) return;

            StringBuilder sb = new StringBuilder();
            foreach(string s in args.Skip(1))
            {
                sb.Append($" {s}");
            }
            string text = sb.ToString().Trim();

            List<BasePlayer> participants = new List<BasePlayer> { player, recipient };

            Conversation conversation = ConversationData.ConversationFactory(participants);

            Message message = new Message(player, text, conversation);

            conversation.Messages.Add(message);
            ConversationData.save();

            message.deliver();
        }

        private void replyCommand(BasePlayer player, string command, string[] args)
        {
            if (!hasPermission(player, permissions.use))
            {
                PrintToChat(player, lang.GetMessage("noPermission", this, player.UserIDString));
                return;
            }

            Profile profile = ProfileData.getProfile(player.userID);
            if (profile == null) return;
            Conversation conversation = ConversationData.getConversation(profile.lastConversation);
            if (conversation == null)
            {
                PrintToChat(player, lang.GetMessage(msg.noRecentMessages.ToString(), this, player.UserIDString));
                return;
            }

            StringBuilder sb = new StringBuilder();
            foreach (string s in args)
            {
                sb.Append($" {s}");
            }
            string text = sb.ToString().Trim();

            Message message = new Message(player, text, conversation);

            conversation.Messages.Add(message);
            ConversationData.save();

            message.deliver();
        }
    }
}﻿namespace Oxide.Plugins
{
    using Newtonsoft.Json;

    partial class Messenger : RustPlugin
    {
        private static ConfigData config;

        private class ConfigData
        {
            [JsonProperty(PropertyName = "Steam API key")]
            public string steamAPIKey;
        }

        private ConfigData getDefaultConfig()
        {
            return new ConfigData
            {
                steamAPIKey = "",
            };
        }

        protected override void LoadConfig()
        {
            base.LoadConfig();

            try
            {
                config = Config.ReadObject<ConfigData>();
            }
            catch
            {
                Puts("Config data is corrupted, replacing with default");
                config = new ConfigData();
            }

            SaveConfig();
        }

        protected override void SaveConfig() => Config.WriteObject(config);

        protected override void LoadDefaultConfig() => config = getDefaultConfig();
    }
}﻿namespace Oxide.Plugins
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
}﻿namespace Oxide.Plugins
{
    using ConVar;
    using Newtonsoft.Json;
    using Oxide.Core;
    using Oxide.Core.Configuration;
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Text;
    using UnityEngine;

    /*
     Make sure that you're not saving complex classes like BasePlayer or Item. Try to stick with primitive types.
     If you're saving your own classes, make sure they have a default constructor and that all properties you're saving are public.
     Take control of which/how properties get serialized by using the Newtonsoft.Json Attributes https://www.newtonsoft.com/json/help/html/SerializationAttributes.htm
    */

    partial class Messenger : RustPlugin
    {
        partial void initData()
        {
            ProfileData.init();
            ConversationData.init();
        }

        [JsonObject(MemberSerialization.OptIn)]
        public class ProfileData
        {
            private static DynamicConfigFile ProfileDataFile;
            private static ProfileData instance;
            private static bool initialized = false;

            [JsonProperty(PropertyName = "Profiles")]
            private Dictionary<ulong, Profile> Profiles { get; set; } = new Dictionary<ulong, Profile>();

            public ProfileData()
            {
            }

            public static void addProfile(Profile profile)
            {
                if (!initialized) init();
                instance.Profiles.Add(profile.SteamID, profile);
                save();
            }

            public static Profile getProfile(ulong SteamID)
            {
                if (!initialized) init();
                if (instance.Profiles.ContainsKey(SteamID)) return instance.Profiles[SteamID];
                else return null;
            }

            public static void removeProfile(ulong SteamID)
            {
                if (!initialized) init();
                if (instance.Profiles.ContainsKey(SteamID)) instance.Profiles.Remove(SteamID);
                save();
            }

            public static void init()
            {
                if (initialized) return;
                ProfileDataFile = Interface.Oxide.DataFileSystem.GetFile("Messenger/Profiles");
                load();
                initialized = true;
            }

            public static void save()
            {
                if (!initialized) init();
                try
                {
                    ProfileDataFile.WriteObject(instance);
                }
                catch (Exception E)
                {
                    StringBuilder sb = new StringBuilder($"saving {typeof(ProfileData).Name} failed. Are you trying to save complex classes like BasePlayer or Item? that won't work!\n");
                    sb.Append(E.Message);
                    PluginInstance.Puts(sb.ToString());
                }
            }

            public static void load()
            {
                try
                {
                    instance = ProfileDataFile.ReadObject<ProfileData>();
                }
                catch (Exception E)
                {
                    StringBuilder sb = new StringBuilder($"loading {typeof(ProfileData).Name} failed. Make sure that all classes you're saving have a default constructor!\n");
                    sb.Append(E.Message);
                    PluginInstance.Puts(sb.ToString());
                }
            }
        }

        [JsonObject(MemberSerialization.OptIn)]
        public class ConversationData
        {
            private static DynamicConfigFile ConversationDataFile;
            private static ConversationData instance;
            private static bool initialized = false;

            [JsonProperty(PropertyName = "Counter")]
            public static int counter { get; set; } = 1;

            [JsonProperty(PropertyName = "Conversations")]
            private Dictionary<uint, Conversation> Conversations { get; set; } = new Dictionary<uint, Conversation>();

            public ConversationData()
            {
            }

            public static Conversation ConversationFactory(List<BasePlayer> Participants)
            {
                //get Profiles
                List<Profile> profiles = new List<Profile>();
                foreach(BasePlayer player in Participants)
                {
                    Profile p = ProfileData.getProfile(player.userID);
                    if (p == null)
                    {
                        p = new Profile(player);
                        ProfileData.addProfile(p);
                    }
                    
                    profiles.Add(p);
                }
                if (profiles.Count != Participants.Count) return null;

                //check for existing Convo
                Conversation convo = null;
                foreach(Conversation c in instance.Conversations.Values)
                {
                    if (c.Participants.All(profiles.Select(p => p.SteamID).Contains)) convo = c;
                }
                if (convo == null) convo = new Conversation() { ID = (uint)counter, DM = (profiles.Count == 2), Participants = profiles.Select(p => p.SteamID).ToList() };
                foreach(Profile profile in profiles)
                {
                    if (!profile.ConversationIDs.Contains(convo.ID)) profile.ConversationIDs.Add(convo.ID);
                }

                addConversation(convo);
                return convo;
            }

            public static void addConversation(Conversation Conversation)
            {
                if (!initialized) init();
                if (!instance.Conversations.ContainsKey(Conversation.ID))
                {
                    instance.Conversations.Add(Conversation.ID, Conversation);
                    counter++;
                }
                
                save();
            } 

            public static Conversation getConversation(uint ID)
            {
                if (!initialized) init();
                if (instance.Conversations.ContainsKey(ID)) return instance.Conversations[ID];
                else return null;
            }

            public static List<Conversation> getConversationsByProfile(Profile profile)
            {
                if (profile == null) return null;
                List<Conversation> output = new List<Conversation>();
                foreach(Conversation c in instance.Conversations.Values)
                {
                    if (c.Participants.Contains(profile.SteamID)) output.Add(c);
                }
                if (output.Count != 0) return output;
                return null;
            }

            public static void removeConversation(uint ID)
            {
                if (!initialized) init();
                if (instance.Conversations.ContainsKey(ID)) instance.Conversations.Remove(ID);
                save();
            }

            public static void init()
            {
                if (initialized) return;
                ConversationDataFile = Interface.Oxide.DataFileSystem.GetFile("Messenger/Conversations");
                load();
                initialized = true;
            }

            public static void save()
            {
                if (!initialized) init();
                try
                {
                    ConversationDataFile.WriteObject(instance);
                }
                catch (Exception E)
                {
                    StringBuilder sb = new StringBuilder($"saving {typeof(ConversationData).Name} failed. Are you trying to save complex classes like BasePlayer or Item? that won't work!\n");
                    sb.Append(E.Message);
                    PluginInstance.Puts(sb.ToString());
                }
            }

            public static void load()
            {
                try
                {
                    instance = ConversationDataFile.ReadObject<ConversationData>();
                }
                catch (Exception E)
                {
                    StringBuilder sb = new StringBuilder($"loading {typeof(ConversationData).Name} failed. Make sure that all classes you're saving have a default constructor!\n");
                    sb.Append(E.Message);
                    PluginInstance.Puts(sb.ToString());
                }
            }
        }
    }
}﻿namespace Oxide.Plugins
{
    using UnityEngine;
    using static Oxide.Plugins.GUICreator;

    partial class Messenger : RustPlugin
    {
        partial void initGUI()
        {
            guiCreator = (GUICreator)Manager.GetPlugin("GUICreator");
        }

        private GUICreator guiCreator;

        private void UIPositionList(BasePlayer player)
        {
        }
    }
}﻿namespace Oxide.Plugins
{
    using System.Collections.Generic;

    partial class Messenger : RustPlugin
    {
        partial void initLang()
        {
            lang.RegisterMessages(messages, this);
        }

        enum msg { noPermission, invalidArguments, noPlayersFound, multiplePlayersFound, message, messageSent, noRecentMessages};

        private Dictionary<string, string> messages = new Dictionary<string, string>()
        {
            {msg.noPermission.ToString(), "You don't have permission to use this command!"},
            {msg.invalidArguments.ToString(), "Too few/many arguments given. Remember to use quotes(\"\") for names containing whitespaces!" },
            {msg.noPlayersFound.ToString(), "No players found!" },
            {msg.multiplePlayersFound.ToString(), "Multiple players found:" },
            {msg.message.ToString(), "Message from {0}: <color=#00ffff>{1}</color>" },
            {msg.messageSent.ToString(), "Message: <color=#00ffff>{0}</color>\ndelivered to: {1}" },
            {msg.noRecentMessages.ToString(), "No recent messages to reply to!" }
        };
    }
}﻿namespace Oxide.Plugins
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
}﻿namespace Oxide.Plugins
{
    using System;

    partial class Messenger : RustPlugin
    {
        partial void initData();

        partial void initCommands();

        partial void initLang();

        partial void initPermissions();

        partial void initGUI();

        private void Loaded()
        {
            initData();
            initCommands();
            initLang();
            initPermissions();
            initGUI();
        }

        void OnPlayerConnected(BasePlayer player)
        {
            if (player == null) return;
            Profile p = ProfileData.getProfile(player.userID);
            if (p != null)
            {
                p.Name = player.displayName;
            }
            ProfileData.save();
        }

        void OnPlayerSleepEnded(BasePlayer player)
        {
            return;
        }

        void OnPlayerDisconnected(BasePlayer player, string reason)
        {
            if (player == null) return;
            Profile p = ProfileData.getProfile(player.userID);
            if (p != null)
            {
                p.LastSeen = DateTime.Now;
            }
            ProfileData.save();
        }
    }
}﻿namespace Oxide.Plugins
{
    using System;

    partial class Messenger : RustPlugin
    {
        //permissions will be (lowercase class name).(perm)
        partial void initPermissions()
        {
            foreach (string perm in Enum.GetNames(typeof(permissions)))
            {
                permission.RegisterPermission($"{PluginInstance.Name}.{perm}", this);
            }
        }

        private enum permissions
        {
            use,
            admin
        }

        private bool hasPermission(BasePlayer player, permissions perm)
        {
            return player.IPlayer.HasPermission($"{PluginInstance.Name}.{Enum.GetName(typeof(permissions), perm)}");
        }

        private void grantPermission(BasePlayer player, permissions perm)
        {
            player.IPlayer.GrantPermission($"{PluginInstance.Name}.{Enum.GetName(typeof(permissions), perm)}");
        }

        private void revokePermission(BasePlayer player, permissions perm)
        {
            player.IPlayer.RevokePermission($"{PluginInstance.Name}.{Enum.GetName(typeof(permissions), perm)}");
        }
    }
}﻿namespace Oxide.Plugins
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
}﻿namespace Oxide.Plugins
{
    using Newtonsoft.Json;
    using Oxide.Core.Libraries;
    using System;

    partial class Messenger : RustPlugin
    {
        public class WebResponse
        {
            public SteamUser response;
        }
        
        public class SteamUser
        {
            public PlayerSummary[] players;
        }

        public class PlayerSummary
        {
            public string steamid;
            public string personaname;
            public string profileurl;
            public string avatarfull;
        }

        public void GetSteamUserData(ulong steamID, Action<PlayerSummary> callback)
        {
            if(string.IsNullOrEmpty(config.steamAPIKey))
            {
                Puts(lang.GetMessage("apiKey", this));
                return;
            }
            string url = "https://api.steampowered.com/ISteamUser/GetPlayerSummaries/v2/?key=" + config.steamAPIKey.ToString() + "&steamids=" + steamID;
            webrequest.Enqueue(url, null, (code, response) =>
            {
                if (code != 200 || response == null)
                {
                    Puts($"Couldn't get an answer from Steam!");
                    return;
                }
                WebResponse webResponse = JsonConvert.DeserializeObject<WebResponse>(response);
                if (webResponse?.response?.players == null)
                {
                    Puts("response is null");
                    callback(null);
                    return;
                }
                if(webResponse.response.players.Length == 0)
                {
                    Puts("response has no playerSummaries");
                    callback(null);
                    return;
                }
#if DEBUG
                Puts($"Got PlayerSummary: {webResponse?.response?.players[0]?.personaname ?? "null"} for steamID [{steamID}]");
#endif
                callback(webResponse.response.players[0]);
            }, this, RequestMethod.GET);
        }
    }
}