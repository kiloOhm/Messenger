namespace Oxide.Plugins
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
}