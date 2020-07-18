namespace Oxide.Plugins
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
}