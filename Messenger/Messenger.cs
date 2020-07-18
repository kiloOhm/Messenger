// Requires: GUICreator
//WIP!!!

using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("Messenger", "OHM", "0.1.0")]
    [Description("UI based ingame messenger")]
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
}