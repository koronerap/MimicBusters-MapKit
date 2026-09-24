using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Mimicraft.EditorTools
{
    /// <summary>
    /// The game on this computer: where it is, where it keeps its maps, and starting it on one.
    ///
    /// The kit is a Unity project of its own, so Application.persistentDataPath here is the KIT's folder,
    /// not the game's. The game's is worked out from the company and product names it is built with, the
    /// way Unity does it for a player. Both the game and its folder can be set by hand, for a development
    /// build or a game somewhere the search does not look.
    /// </summary>
    public static class KitGame
    {
        // The game's PlayerSettings. They decide where its data folder is, so they are part of the kit's
        // contract with the game, like the map format.
        public const string Company = "DevAdam";
        public const string Product = "MimicBusters";

        /// <summary>What the game reads to open a map on launch: Mimicraft.MapKit.KitMapLaunch.Argument, in
        /// the game's own code, which the kit does not have.</summary>
        public const string LaunchArgument = "-kitmap";

        // The Steam apps that are the game, searched in this order: the game, its playtest, its demo.
        static readonly (uint id, string label)[] steamApps =
        {
            (5167420, "Mimic Busters"),
            (5168070, "Mimic Busters Playtest"),
            (5231380, "Mimic Busters Demo"),
        };

        const string GamePathKey = "MimicBusters.MapKit.GamePath";
        const string DataFolderKey = "MimicBusters.MapKit.DataFolder";

        /// <summary>The game's executable (an .app on macOS), as chosen or found. Empty until then.</summary>
        public static string GamePath
        {
            get => EditorPrefs.GetString(GamePathKey, "");
            set => EditorPrefs.SetString(GamePathKey, value ?? "");
        }

        /// <summary>The game's data folder when set by hand; empty for the usual one.</summary>
        public static string DataFolderOverride
        {
            get => EditorPrefs.GetString(DataFolderKey, "");
            set => EditorPrefs.SetString(DataFolderKey, value ?? "");
        }

        public static string DataFolder => string.IsNullOrEmpty(DataFolderOverride) ? DefaultDataFolder : DataFolderOverride;

        /// <summary>The game's Application.persistentDataPath on this computer.</summary>
        public static string DefaultDataFolder
        {
            get
            {
                string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
                switch (Application.platform)
                {
                    case RuntimePlatform.WindowsEditor:
                        return Path.Combine(home, "AppData", "LocalLow", Company, Product);
                    case RuntimePlatform.OSXEditor:
                        return Path.Combine(home, "Library", "Application Support", Company, Product);
                    default:
                        string config = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
                        if (string.IsNullOrEmpty(config))
                            config = Path.Combine(home, ".config");
                        return Path.Combine(config, "unity3d", Company, Product);
                }
            }
        }

        /// <summary>Where the game looks for local maps: KitMapId.LocalRoot, seen from outside the game.</summary>
        public static string LocalMapsFolder => Path.Combine(DataFolder, "Maps", "Local");

        /// <summary>Whether a path is a Unity player: an executable with its "_Data" folder beside it, or an
        /// .app on macOS.</summary>
        public static bool IsPlayer(string path)
        {
            if (string.IsNullOrEmpty(path))
                return false;
            if (Application.platform == RuntimePlatform.OSXEditor)
                return path.EndsWith(".app", StringComparison.OrdinalIgnoreCase) && Directory.Exists(path);
            return File.Exists(path)
                && Directory.Exists(Path.Combine(Path.GetDirectoryName(path), Path.GetFileNameWithoutExtension(path) + "_Data"));
        }

        // ------------------------------------------------------------------ finding it

        public sealed class Install
        {
            public string Label;
            public string Path;
        }

        /// <summary>The copies of the game Steam has installed, in every Steam library on this computer.</summary>
        public static List<Install> FindSteamInstalls()
        {
            var found = new List<Install>();
            foreach (string library in SteamLibraries())
            {
                foreach ((uint id, string label) in steamApps)
                {
                    string manifest = Path.Combine(library, "steamapps", $"appmanifest_{id}.acf");
                    if (!File.Exists(manifest))
                        continue;

                    Match folder = Regex.Match(ReadText(manifest), "\"installdir\"\\s+\"([^\"]+)\"");
                    if (!folder.Success)
                        continue;

                    string player = FindPlayer(Path.Combine(library, "steamapps", "common", Unescape(folder.Groups[1].Value)));
                    if (player != null && !found.Any(f => SamePath(f.Path, player)))
                        found.Add(new Install { Label = label, Path = player });
                }
            }
            return found;
        }

        static List<string> SteamLibraries()
        {
            string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            var roots = new List<string>();
            switch (Application.platform)
            {
                case RuntimePlatform.WindowsEditor:
                    string registered = WindowsSteamPath();
                    if (registered != null)
                        roots.Add(registered);
                    roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Steam"));
                    roots.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Steam"));
                    break;
                case RuntimePlatform.OSXEditor:
                    roots.Add(Path.Combine(home, "Library", "Application Support", "Steam"));
                    break;
                default:
                    roots.Add(Path.Combine(home, ".steam", "steam"));
                    roots.Add(Path.Combine(home, ".local", "share", "Steam"));
                    roots.Add(Path.Combine(home, ".var", "app", "com.valvesoftware.Steam", ".local", "share", "Steam"));
                    break;
            }

            var libraries = new List<string>();
            foreach (string root in roots.Where(Directory.Exists))
            {
                AddPath(libraries, root);
                string list = Path.Combine(root, "steamapps", "libraryfolders.vdf");
                if (!File.Exists(list))
                    continue;
                foreach (Match path in Regex.Matches(ReadText(list), "\"path\"\\s+\"([^\"]+)\""))
                    AddPath(libraries, Unescape(path.Groups[1].Value));
            }
            return libraries.Where(Directory.Exists).ToList();
        }

        /// <summary>Steam's folder from the registry, through reg.exe: the Registry class is not in the API
        /// level a Unity project compiles against.</summary>
        static string WindowsSteamPath()
        {
            try
            {
                var info = new ProcessStartInfo("reg", "query \"HKCU\\Software\\Valve\\Steam\" /v SteamPath")
                {
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                };
                using (Process reg = Process.Start(info))
                {
                    string output = reg.StandardOutput.ReadToEnd();
                    reg.WaitForExit(3000);
                    Match path = Regex.Match(output, "SteamPath\\s+REG_SZ\\s+(.+)");
                    return path.Success ? path.Groups[1].Value.Trim().Replace('/', '\\') : null;
                }
            }
            catch (Exception)
            {
                return null;
            }
        }

        static string FindPlayer(string folder)
        {
            if (!Directory.Exists(folder))
                return null;
            if (Application.platform == RuntimePlatform.OSXEditor)
                return Directory.GetDirectories(folder, "*.app").FirstOrDefault();
            string pattern = Application.platform == RuntimePlatform.WindowsEditor ? "*.exe" : "*.x86_64";
            return Directory.GetFiles(folder, pattern).FirstOrDefault(IsPlayer);
        }

        static string ReadText(string path)
        {
            try
            {
                return File.ReadAllText(path);
            }
            catch (Exception)
            {
                return "";
            }
        }

        // Steam's text files write a backslash as two.
        static string Unescape(string value) => value.Replace("\\\\", "\\");

        static void AddPath(List<string> list, string path)
        {
            if (!list.Any(p => SamePath(p, path)))
                list.Add(path);
        }

        static bool SamePath(string a, string b)
        {
            try
            {
                return string.Equals(Path.GetFullPath(a).TrimEnd('/', '\\'), Path.GetFullPath(b).TrimEnd('/', '\\'),
                    Application.platform == RuntimePlatform.LinuxEditor ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception)
            {
                return false;
            }
        }

        // ------------------------------------------------------------------ running it

        /// <summary>The game's processes, if it is running.</summary>
        public static Process[] Running()
        {
            string game = GamePath;
            if (string.IsNullOrEmpty(game))
                return Array.Empty<Process>();
            try
            {
                return Process.GetProcessesByName(Path.GetFileNameWithoutExtension(game));
            }
            catch (Exception)
            {
                return Array.Empty<Process>();
            }
        }

        /// <summary>Asks the game to quit, as its window's close button would, and ends it if it has not
        /// after a few seconds. True when nothing of it is left running.</summary>
        public static bool Close(Process[] running)
        {
            foreach (Process process in running)
            {
                try
                {
                    if (process.HasExited)
                        continue;
                    if (process.CloseMainWindow() && process.WaitForExit(8000))
                        continue;
                    process.Kill();
                    process.WaitForExit(3000);
                }
                catch (Exception)
                {
                    // Gone already, or not ours to end; HasExited below says which.
                }
            }

            return running.All(p =>
            {
                try
                {
                    return p.HasExited;
                }
                catch (Exception)
                {
                    return true;
                }
            });
        }

        /// <summary>Starts the game on a local map (its folder name under Maps/Local).</summary>
        public static bool Launch(string mapName, out string error)
        {
            string game = GamePath;
            if (!IsPlayer(game))
            {
                error = KitEditorLanguage.T("Oyun bulunamadı. Paketle ve Test Et penceresinde oyunu seç.",
                    "The game was not found. Choose it in the Pack and Test window.");
                return false;
            }

            try
            {
                ProcessStartInfo info = Application.platform == RuntimePlatform.OSXEditor
                    ? new ProcessStartInfo("open", $"-n \"{game}\" --args {LaunchArgument} {mapName}")
                    : new ProcessStartInfo(game, $"{LaunchArgument} {mapName}") { WorkingDirectory = Path.GetDirectoryName(game) };
                info.UseShellExecute = false;
                Process.Start(info);
                error = null;
                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }
    }
}
