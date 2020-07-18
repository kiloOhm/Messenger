namespace Oxide.Plugins
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
}