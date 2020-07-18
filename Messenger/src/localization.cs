namespace Oxide.Plugins
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
}