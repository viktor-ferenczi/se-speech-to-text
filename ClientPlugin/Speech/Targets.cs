using System;
using System.Collections.Generic;
using System.Linq;

namespace ClientPlugin.Speech;

// The configured mod channels and script names as parsed from the config
// strings, plus the per-item on/off state the selector dialog keeps in
// Config.DisabledTargets (semicolon separated keys).
internal static class Targets
{
    private static readonly char[] Separators = { ';' };

    public static IReadOnlyList<long> ModChannels(Config config)
    {
        var channels = new List<long>();
        foreach (var item in Split(config.ModChannels))
        {
            if (long.TryParse(item, out var id))
                channels.Add(id);
        }

        return channels;
    }

    public static IReadOnlyList<string> ScriptNames(Config config) => Split(config.ScriptNames);

    public static string ModKey(long channel) => "mod:" + channel;

    public static string ScriptKey(string name) => "script:" + name;

    public static bool IsEnabled(Config config, string key) => !Split(config.DisabledTargets).Contains(key);

    public static void SetEnabled(Config config, string key, bool enabled)
    {
        var disabled = Split(config.DisabledTargets);
        if (enabled)
            disabled.Remove(key);
        else if (!disabled.Contains(key))
            disabled.Add(key);

        config.DisabledTargets = string.Join(";", disabled);
    }

    private static List<string> Split(string list)
    {
        if (string.IsNullOrWhiteSpace(list))
            return new List<string>();

        return list
            .Split(Separators, StringSplitOptions.RemoveEmptyEntries)
            .Select(item => item.Trim())
            .Where(item => item.Length > 0)
            .ToList();
    }
}
