namespace Oxide.Plugins
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
}