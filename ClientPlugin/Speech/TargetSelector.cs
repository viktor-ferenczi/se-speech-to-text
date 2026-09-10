using System;
using System.Collections.Generic;
using ClientPlugin.Settings;
using ClientPlugin.Settings.Elements;
using ClientPlugin.Settings.Layouts;
using Sandbox.Graphics.GUI;

namespace ClientPlugin.Speech;

// The dialog behind the Selector key: one checkbox per configured target. The
// chat modes are exclusive and map onto PasteIntoChat + TargetChat, the mod
// channels and scripts toggle their entry in Config.DisabledTargets. Closing
// the dialog saves the config (SettingsScreen.OnRemoved).
internal class TargetSelector
{
    private readonly SettingsScreen screen;
    private readonly Simple layout;
    private List<List<Control>> rows = new List<List<Control>>();

    public TargetSelector()
    {
        layout = new Simple(() => rows);
        screen = new SettingsScreen("Speech Targets", RecreateControls, size: layout.SettingsPanelSize);
    }

    public void Open() => MyGuiSandbox.AddScreen(screen);

    // Called on every opening, so the rows always reflect the current config
    private List<MyGuiControlBase> RecreateControls()
    {
        rows = BuildRows(Config.Current);
        var controls = layout.RecreateControls();
        layout.LayoutControls();
        return controls;
    }

    private static List<List<Control>> BuildRows(Config config)
    {
        var rows = new List<List<Control>> { Separator("Chat") };

        var chatBoxes = new List<MyGuiControlCheckbox>();
        foreach (ChatTarget target in Enum.GetValues(typeof(ChatTarget)))
        {
            MyGuiControlCheckbox self = null;
            var row = Checkbox(Settings.Tools.Tools.GetLabelOrDefault(target.ToString()),
                () => config.PasteIntoChat && config.TargetChat == target,
                isChecked =>
                {
                    if (isChecked)
                    {
                        config.TargetChat = target;
                        config.PasteIntoChat = true;

                        // Their callbacks see TargetChat != their target, so they do nothing
                        foreach (var other in chatBoxes)
                        {
                            if (other != self)
                                other.IsChecked = false;
                        }
                    }
                    else if (config.TargetChat == target)
                    {
                        config.PasteIntoChat = false;
                    }
                });

            self = (MyGuiControlCheckbox)row[1].GuiControl;
            chatBoxes.Add(self);
            rows.Add(row);
        }

        rows.Add(Separator("Mod channels" + OffSuffix(config.SendToModsAndPlugins)));
        var channels = Targets.ModChannels(config);
        if (channels.Count == 0)
            rows.Add(Note("None configured"));

        foreach (var channel in channels)
        {
            var key = Targets.ModKey(channel);
            rows.Add(Checkbox(channel.ToString(),
                () => Targets.IsEnabled(config, key),
                enabled => Targets.SetEnabled(config, key, enabled)));
        }

        rows.Add(Separator("Scripts" + OffSuffix(config.SendToScripts)));
        var names = Targets.ScriptNames(config);
        if (names.Count == 0)
            rows.Add(Note("None configured"));

        foreach (var name in names)
        {
            var key = Targets.ScriptKey(name);
            rows.Add(Checkbox(name,
                () => Targets.IsEnabled(config, key),
                enabled => Targets.SetEnabled(config, key, enabled)));
        }

        return rows;
    }

    private static string OffSuffix(bool enabledInConfig) => enabledInConfig ? "" : " (off in config)";

    private static List<Control> Separator(string caption) =>
        new SeparatorAttribute(caption).GetControls(caption, null, null);

    private static List<Control> Checkbox(string label, Func<bool> getter, Action<bool> setter) =>
        new CheckboxAttribute(label).GetControls(label, () => getter(), value => setter((bool)value));

    private static List<Control> Note(string text) =>
        new List<Control> { new Control(new MyGuiControlLabel(text: text), fillFactor: 1f) };
}
