using System;
using System.Reflection;
using ClientPlugin.Settings;
using ClientPlugin.Settings.Layouts;
using ClientPlugin.Speech;
using HarmonyLib;
using Sandbox.Game.Gui;
using Sandbox.Game.World;
using Sandbox.Graphics.GUI;
using VRage.Input;
using VRage.Plugins;
using VRage.Utils;

// Define assembly version when compiled by Pulsar
#if !LOCAL_BUILD
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]
#endif

namespace ClientPlugin;

// ReSharper disable once UnusedType.Global
public class Plugin : IPlugin
{
    public const string Name = "SpeechToText";
    public static Plugin Instance { get; private set; }

    private SettingsGenerator settingsGenerator;
    private TargetSelector targetSelector;
    private SpeechListener listener;

    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.NoInlining)]
    public void Init(object gameInstance)
    {
        Instance = this;
        settingsGenerator = new SettingsGenerator();
        targetSelector = new TargetSelector();

        StartListener();
        Config.Current.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Config.ListenPort))
                StartListener();
        };

        var harmony = new Harmony(Name);
        harmony.PatchAll(Assembly.GetExecutingAssembly());
    }

    public void Dispose()
    {
        // IMPORTANT: Do NOT call harmony.UnpatchAll() here! It may break other plugins.
        listener?.Dispose();
        listener = null;
        Instance = null;
    }

    // Called on every simulation frame on the game thread
    public void Update()
    {
        ProcessSpeechRequests();
        HandleSelectorKey();
    }

    private void StartListener()
    {
        var port = Config.Current.ListenPort;
        if (listener != null && listener.Port == port)
            return;

        listener?.Dispose();
        listener = new SpeechListener(port);
        listener.Start();
    }

    private void ProcessSpeechRequests()
    {
        while (listener != null && listener.TryDequeue(out var request))
        {
            // Already answered by the listener, Handy pasted the text itself
            if (!request.TryClaim())
                continue;

            var handled = false;
            try
            {
                handled = SpeechDispatcher.TryHandle(request.Text);
            }
            catch (Exception e)
            {
                MyLog.Default.Error($"{Name}: Failed to handle the transcript: {e}");
            }
            finally
            {
                request.Complete(handled);
            }
        }
    }

    private void HandleSelectorKey()
    {
        var config = Config.Current;
        if (!config.Enabled || MySession.Static == null)
            return;

        // Not while typing into some textbox
        if (!(MyScreenManager.GetScreenWithFocus() is MyGuiScreenGamePlay))
            return;

        if (config.SelectorKey.HasPressed(MyInput.Static))
            targetSelector.Open();
    }

    // ReSharper disable once UnusedMember.Global
    public void OpenConfigDialog()
    {
        Instance.settingsGenerator.SetLayout<Simple>();
        MyGuiSandbox.AddScreen(Instance.settingsGenerator.Dialog);
    }
}
