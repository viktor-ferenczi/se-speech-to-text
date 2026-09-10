using System;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using VRage.Utils;

namespace ClientPlugin.Tools;

// Locates the Handy paste hook script deployed next to this assembly. Handy is
// pointed at it via Settings / Advanced / Paste Method / External Script, and
// the config dialog shows the path so it can be copied over.
public static class PasteScript
{
    private const string WindowsName = "handy-paste.cmd";
    private const string UnixName = "handy-paste.sh";

    private static readonly Lazy<string> resolved = new Lazy<string>(Resolve);

    // Full path of the script to configure in Handy, in the host's own path
    // format. Empty if the plugin folder cannot be determined.
    public static string FullPath => resolved.Value;

    private static string Resolve()
    {
        var directory = GetPluginDirectory();
        if (string.IsNullOrEmpty(directory))
            return "";

        // Handy runs on the host, so it is the host's platform that decides
        // which of the two scripts is executable, not the platform this
        // assembly appears to be running on.
        return RunningOnUnixHost()
            ? ToHostPath(Path.Combine(directory, UnixName))
            : Path.Combine(directory, WindowsName);
    }

    private static string GetPluginDirectory()
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();

            var location = assembly.Location;

#if NETFRAMEWORK
            // Empty for an assembly loaded from memory, hence the CodeBase fallback.
            if (string.IsNullOrEmpty(location) && !string.IsNullOrEmpty(assembly.CodeBase))
                location = new Uri(assembly.CodeBase).LocalPath;
#endif

            return string.IsNullOrEmpty(location) ? "" : Path.GetDirectoryName(location);
        }
        catch (Exception e)
        {
            MyLog.Default.Warning($"{Plugin.Name}: Failed to locate the plugin folder: {e.Message}");
            return "";
        }
    }

    private static bool RunningOnUnixHost()
    {
        var platform = Environment.OSVersion.Platform;
        if (platform == PlatformID.Unix || platform == PlatformID.MacOSX)
            return true;

        // Under Wine and Proton the runtime reports Windows, but the host is
        // Linux and so is the Handy install which has to run the script.
        try
        {
            return GetProcAddress(GetModuleHandle("ntdll.dll"), "wine_get_version") != IntPtr.Zero;
        }
        catch (Exception)
        {
            return false;
        }
    }

    // Wine maps the host's root directory to drive Z:, which is how the game
    // sees a plugin installed under the user's home. A path on any other drive
    // lives inside the Wine prefix and cannot be translated without knowing it,
    // so it is left alone.
    private static string ToHostPath(string path)
    {
        if (path.Length >= 2 && path[1] == ':' && (path[0] == 'Z' || path[0] == 'z'))
            return path.Substring(2).Replace('\\', '/');

        return path;
    }

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    private static extern IntPtr GetModuleHandle(string moduleName);

    [DllImport("kernel32.dll", CharSet = CharSet.Ansi)]
    private static extern IntPtr GetProcAddress(IntPtr module, string procName);
}
