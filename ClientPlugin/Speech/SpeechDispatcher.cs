using System;
using System.Linq;
using Sandbox.Game.Gui;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
using Sandbox.ModAPI;
using VRage;
using VRage.Utils;

namespace ClientPlugin.Speech;

// Game thread side: decides whether a transcript belongs to the game and fans
// it out to the configured targets.
internal static class SpeechDispatcher
{
    // True if the plugin took the text, in which case Handy must not paste it.
    // The decision is made before anything is done with the text, so a failing
    // target cannot turn it into a double paste.
    public static bool TryHandle(string text)
    {
        var config = Config.Current;
        if (!IsOurs(config))
            return false;

        MyLog.Default.Info($"{Plugin.Name}: Transcript received ({text.Length} chars)");

        if (config.PasteIntoChat)
            Try("paste into the chat", () => ChatPaste.Paste(text, config.TargetChat, config.AutomaticallySendChat));

        // The same string goes to mods and to the programmable blocks
        var message = config.MessagePrefix + text;

        if (config.SendToModsAndPlugins)
            Try("send to mods", () => SendToMods(config, message));

        if (config.SendToScripts)
            Try("run the scripts", () => RunScripts(config, message));

        return true;
    }

    private static bool IsOurs(Config config)
    {
        if (!config.Enabled)
            return false;

        if (!config.PasteIntoChat && !config.SendToModsAndPlugins && !config.SendToScripts)
            return false;

        var session = MySession.Static;
        if (session == null || !session.Ready)
            return false;

        // Dictating into another window
        if (!MyVRage.Platform.Windows.Window.IsActive)
            return false;

        var focused = MyScreenManager.GetScreenWithFocus();

        // The chat is ours to fill in, wherever the caret happens to be
        if (ChatPaste.IsChatScreen(focused))
            return true;

        if (focused is MyGuiScreenGamePlay)
            return true;

        // The terminal is part of the world, so the text belongs to the game
        // there too - it is where the player watches a script's output. The
        // exception is its textboxes (search, block name, custom data): with
        // one of those focused the player is typing, so the plain paste from
        // the speech to text application is what they want.
        if (focused is MyGuiScreenTerminal)
            return !(focused.FocusedControl is MyGuiControlTextbox);

        // Any other menu: leave the text to whatever has the keyboard
        return false;
    }

    private static void SendToMods(Config config, string message)
    {
        foreach (var channel in Targets.ModChannels(config))
        {
            if (Targets.IsEnabled(config, Targets.ModKey(channel)))
                MyAPIUtilities.Static.SendModMessage(channel, message);
        }
    }

    private static void RunScripts(Config config, string message)
    {
        var names = Targets.ScriptNames(config).Where(name => Targets.IsEnabled(config, Targets.ScriptKey(name)));
        var count = ScriptRunner.Run(names, config.ScriptScope, message);
        if (count == 0)
            MyLog.Default.Info($"{Plugin.Name}: No programmable block matched the configured script names");
    }

    private static void Try(string what, Action action)
    {
        try
        {
            action();
        }
        catch (Exception e)
        {
            MyLog.Default.Error($"{Plugin.Name}: Failed to {what}: {e}");
        }
    }
}
