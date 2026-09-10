using System;
using ClientPlugin.Settings;
using ClientPlugin.Settings.Elements;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using ClientPlugin.Settings.Tools;
using ClientPlugin.Tools;
using VRage;
using VRage.Input;


namespace ClientPlugin;

public enum ChatTarget
{
    Faction,
    Global,
    LastUsed
}

public enum ScriptScope
{
    ControlledGridGroup,
    AllAccessibleGrids,
    ControlledGroupThenAll
}

public class Config : INotifyPropertyChanged
{
    #region Options

    public const string DefaultModChannels = "511505115";
    public const string DefaultScriptNames = "*[STT]";
    public const string DefaultMessagePrefix = "STT:";
    public const int DefaultListenPort = 5115;
    public const int DefaultVerdictTimeoutMs = 300;

    private bool enabled = true;
    private int listenPort = DefaultListenPort;
    private int verdictTimeoutMs = DefaultVerdictTimeoutMs;
    private string messagePrefix = DefaultMessagePrefix;
    private bool pasteIntoChat;
    private ChatTarget targetChat = ChatTarget.Faction;
    private bool automaticallySendChat;
    private bool sendToModsAndPlugins = true;
    private string modChannels = DefaultModChannels;
    private bool sendToScripts = true;
    private string scriptNames = DefaultScriptNames;
    private ScriptScope scriptScope = ScriptScope.ControlledGridGroup;
    private string disabledTargets = "";
    private Binding selectorKey = new Binding(MyKeys.S, ctrl: true, alt: true);

    #endregion

    #region User interface

    public readonly string Title = "Speech To Text";

    [Checkbox(label: "Enable", description: "Enables or disables the whole plugin")]
    public bool Enabled
    {
        get => enabled;
        set => SetField(ref enabled, value);
    }

    [Separator("Handy integration")]

    [Textbox(label: "Port",
        description: "Port Handy sends the transcribed text to, always on 127.0.0.1 only.\n"
                     + "Both paste methods below follow this setting; for the paste script\n"
                     + "it must also be set as HANDY_HOOK_PORT if changed from the default.")]
    public int ListenPort
    {
        get => listenPort;
        set => SetField(ref listenPort, value);
    }

    [Textbox(label: "Timeout (ms)",
        description: "How long Handy waits for the game to take the text.\n"
                     + "Keep it below Handy's own timeout, otherwise Handy gives up and\n"
                     + "pastes the text while the game is still handling it:\n"
                     + "400 ms for the webhook (configurable in Handy), 1 second for the script.")]
    public int VerdictTimeoutMs
    {
        get => verdictTimeoutMs;
        set => SetField(ref verdictTimeoutMs, value);
    }

    [Separator("Paste method: Webhook")]

    [Textbox(label: "Webhook URL", readOnly: true,
        description: "Endpoint this plugin listens on, following the port set above.\n"
                     + "Set it in Handy under Settings / Advanced / Paste Method / Webhook.\n"
                     + "Needs a Handy build that offers Webhook as a paste method.")]
    public string WebhookUrl => $"http://127.0.0.1:{listenPort}/handy";

    // A Delegate-typed property rather than a method, so the button keeps its
    // place next to the value it copies: SettingsGenerator lays properties out
    // in declaration order but appends every annotated method after them.
    [Button(label: "Copy webhook URL", description: "Copies the webhook URL to the clipboard")]
    public Action CopyWebhookUrl => () => MyVRage.Platform.System.Clipboard = WebhookUrl;

    [Separator("Paste method: External script")]

    [Textbox(label: "Paste script", readOnly: true,
        description: "Full path of the paste script shipped with this plugin.\n"
                     + "Set it in Handy under Settings / Advanced / Paste Method / External Script.\n"
                     + "Only needed if Handy has no Webhook paste method.")]
    public string PasteScriptPath => PasteScript.FullPath;

    [Button(label: "Copy script path", description: "Copies the paste script's path to the clipboard")]
    public Action CopyPasteScriptPath => () => MyVRage.Platform.System.Clipboard = PasteScriptPath;

    [Separator("Chat")]

    [Checkbox(label: "Paste into chat", description: "Pastes the transcribed text into the selected chat")]
    public bool PasteIntoChat
    {
        get => pasteIntoChat;
        set => SetField(ref pasteIntoChat, value);
    }

    [Dropdown(label: "Target chat", description: "Chat channel to paste the transcribed text into")]
    public ChatTarget TargetChat
    {
        get => targetChat;
        set => SetField(ref targetChat, value);
    }

    [Checkbox(label: "Automatically send the chat",
        description: "Sends the chat message into the chat channel.\n"
                     + "If disabled, then the focus is set on the chat for you to send it.")]
    public bool AutomaticallySendChat
    {
        get => automaticallySendChat;
        set => SetField(ref automaticallySendChat, value);
    }

    [Separator("Mods and scripts")]

    [Textbox(label: "Message prefix",
        description: "Put in front of the transcribed text sent to mods, plugins and scripts,\n"
                     + "so they can tell speech input apart from anything else")]
    public string MessagePrefix
    {
        get => messagePrefix;
        set => SetField(ref messagePrefix, value ?? "");
    }


    [Checkbox(label: "Send to mods and plugins",
        description: "Sends the transcribed text to one or more mod communication channels")]
    public bool SendToModsAndPlugins
    {
        get => sendToModsAndPlugins;
        set => SetField(ref sendToModsAndPlugins, value);
    }

    [Textbox(label: "Mod communication channels",
        description: "Semicolon separated list of decimal mod channel IDs")]
    public string ModChannels
    {
        get => modChannels;
        set => SetField(ref modChannels, value);
    }

    [Separator("Programmable blocks")]

    [Checkbox(label: "Send to scripts",
        description: "Runs the designated programmable blocks with the message prefix\n"
                     + "followed by the transcribed text as their run argument")]
    public bool SendToScripts
    {
        get => sendToScripts;
        set => SetField(ref sendToScripts, value);
    }

    [Textbox(label: "Script names",
        description: "Semicolon separated list of programmable block names,\n"
                     + "or suffixes like *[STT]")]
    public string ScriptNames
    {
        get => scriptNames;
        set => SetField(ref scriptNames, value);
    }

    [Dropdown(label: "Script scope",
        description: "Where to look for the programmable blocks:\n"
                     + "Controlled grid group: the grid you control and its subgrids\n"
                     + "All accessible grids: every loaded grid you have terminal access to\n"
                     + "Controlled group then all: the first one, falling back to the second")]
    public ScriptScope ScriptScope
    {
        get => scriptScope;
        set => SetField(ref scriptScope, value);
    }

    [Separator("Hotkeys")]

    [Keybind(label: "Selector key",
        description: "Opens the target selector dialog, where the configured targets\n"
                     + "(each chat mode, each mod channel, each script) can be enabled\n"
                     + "individually. Unbind by right clicking the button.")]
    public Binding SelectorKey
    {
        get => selectorKey;
        set => SetField(ref selectorKey, value);
    }

    // Target keys switched off in the selector dialog, semicolon separated.
    // Edited through that dialog only, hence no UI attribute; see Targets.
    public string DisabledTargets
    {
        get => disabledTargets;
        set => SetField(ref disabledTargets, value ?? "");
    }

    #endregion

    #region Property change notification boilerplate

    public static readonly Config Default = new Config();
    public static readonly Config Current = ConfigStorage.Load();

    public event PropertyChangedEventHandler PropertyChanged;

    protected virtual void OnPropertyChanged(string propertyName)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value)) return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    #endregion
}
