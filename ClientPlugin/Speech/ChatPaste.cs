using System;
using System.Reflection;
using HarmonyLib;
using Sandbox.Game.Gui;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
using Sandbox.ModAPI;
using VRage.Utils;
using VRageMath;

namespace ClientPlugin.Speech;

// Puts the transcript into the game's chat, or sends it right away. The chat
// screen (MyGuiScreenChat) is internal, hence the reflection; the rest is the
// public API the screen itself goes through when the player presses Enter.
internal static class ChatPaste
{
    private static readonly Type ScreenType = AccessTools.TypeByName("Sandbox.Game.Gui.MyGuiScreenChat");
    private static readonly ConstructorInfo Constructor = Lookup(t => AccessTools.Constructor(t, new[] { typeof(Vector2) }));
    private static readonly FieldInfo StaticField = Lookup(t => AccessTools.Field(t, "Static"));
    private static readonly PropertyInfo TextboxProperty = Lookup(t => AccessTools.Property(t, "ChatTextbox"));
    private static readonly MethodInfo SendMethod = Lookup(t => AccessTools.Method(t, "SendChatMessage", new[] { typeof(string) }));

    private static T Lookup<T>(Func<Type, T> lookup) where T : class => ScreenType == null ? null : lookup(ScreenType);

    public static bool IsAvailable => Constructor != null && StaticField != null && TextboxProperty != null && SendMethod != null;

    public static bool IsChatScreen(MyGuiScreenBase screen) => screen != null && ScreenType != null && ScreenType.IsInstanceOfType(screen);

    public static void Paste(string text, ChatTarget target, bool send)
    {
        if (!IsAvailable)
        {
            MyLog.Default.Warning($"{Plugin.Name}: The chat screen changed, cannot paste into the chat");
            return;
        }

        // The player is already typing: drop the text at the caret, leave the
        // channel they picked alone and let them send it themselves.
        var open = OpenTextbox();
        if (open != null)
        {
            open.InsertCharMultiple(true, text);
            return;
        }

        SelectChannel(target);

        if (send)
            Send(text);
        else
            OpenChat().InsertCharMultiple(true, text);
    }

    private static void SelectChannel(ChatTarget target)
    {
        var session = MySession.Static;
        switch (target)
        {
            case ChatTarget.Global:
                session.ChatSystem.ChangeChatChannel_Global();
                break;

            case ChatTarget.Faction:
                if (session.Factions.TryGetPlayerFaction(session.LocalPlayerId) != null)
                {
                    session.ChatSystem.ChangeChatChannel_Faction();
                }
                else
                {
                    MyLog.Default.Info($"{Plugin.Name}: Not in a faction, using the global chat instead");
                    session.ChatSystem.ChangeChatChannel_Global();
                }
                break;

            case ChatTarget.LastUsed:
                break;
        }
    }

    // What MyGuiScreenChat.OnInputFieldActivated does, minus the screen
    private static void Send(string text)
    {
        var session = MySession.Static;
        var player = session.LocalHumanPlayer;

        if (session.ChatSystem.CommandSystem.CanHandle(text))
        {
            MyHud.Chat.ShowMessage(player?.PlatformDisplayName ?? "", text);
            session.ChatSystem.CommandSystem.Handle(text);
            return;
        }

        var sendToOthers = true;
        MyAPIUtilities.Static.EnterMessage(player?.Id.SteamId ?? 0, text, ref sendToOthers);
        if (sendToOthers)
            SendMethod.Invoke(null, new object[] { text });
    }

    private static MyGuiControlTextbox OpenTextbox()
    {
        var screen = StaticField.GetValue(null);
        return screen == null ? null : (MyGuiControlTextbox)TextboxProperty.GetValue(screen);
    }

    // Opens the chat where the game puts it on pressing Enter. The screen is
    // only added on the next GUI update, but its textbox exists already.
    private static MyGuiControlTextbox OpenChat()
    {
        var hudPos = new Vector2(0.029f, 0.8f);
        hudPos = MyGuiScreenHudBase.ConvertHudToNormalizedGuiPosition(ref hudPos);

        var screen = (MyGuiScreenBase)Constructor.Invoke(new object[] { hudPos });
        MyGuiSandbox.AddScreen(screen);
        return (MyGuiControlTextbox)TextboxProperty.GetValue(screen);
    }
}
