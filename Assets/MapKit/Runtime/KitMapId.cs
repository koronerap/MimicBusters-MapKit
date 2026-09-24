using System;
using System.IO;
using UnityEngine;

namespace Mimicraft.MapKit
{
    /// <summary>
    /// Map ids for maps that come from outside the build, and where their files live.
    ///
    /// A kit map's id travels in the same LobbySettingsData.MapId as a built-in map's, so the two are told
    /// apart by prefix: "local:&lt;name&gt;" is a folder under Maps/Local on this machine, "ws:&lt;id&gt;" a
    /// Steam Workshop item (not read yet). Built-in ids never contain a colon.
    ///
    /// The name part is the only piece of an id that ever becomes a path, and it is held to a character
    /// set that cannot express one: lower-case letters, digits, '-' and '_'. The resolved folder is still
    /// checked to sit under the root, because a rule about characters is one refactor away from being
    /// loosened by someone who did not know what it was for.
    /// </summary>
    public static class KitMapId
    {
        public const string LocalPrefix = "local:";
        public const string WorkshopPrefix = "ws:";

        /// <summary>FixedString32Bytes carries 29 bytes; "local:" takes six.</summary>
        public const int MaxLocalName = 23;

        public const string ManifestFile = "map.json";

        public static bool IsKit(string mapId) =>
            !string.IsNullOrEmpty(mapId)
            && (mapId.StartsWith(LocalPrefix, StringComparison.Ordinal)
                || mapId.StartsWith(WorkshopPrefix, StringComparison.Ordinal));

        public static string Local(string name) => LocalPrefix + name;

        public static bool IsValidLocalName(string name)
        {
            if (string.IsNullOrEmpty(name) || name.Length > MaxLocalName)
                return false;

            foreach (char c in name)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_';
                if (!ok)
                    return false;
            }

            return true;
        }

        /// <summary>A scene or folder name turned into a valid local name, for the packer.</summary>
        public static string SanitizeName(string raw)
        {
            var chars = (raw ?? "").ToLowerInvariant().ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                bool ok = (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9') || c == '-' || c == '_';
                if (!ok)
                    chars[i] = '-';
            }

            string name = new string(chars).Trim('-');
            if (name.Length > MaxLocalName)
                name = name.Substring(0, MaxLocalName);
            return name.Length == 0 ? "map" : name;
        }

        public static string MapsRoot => Path.Combine(Application.persistentDataPath, "Maps");

        public static string LocalRoot => Path.Combine(MapsRoot, "Local");

        /// <summary>The folder a local id points at, or null when the id is not a valid local one.</summary>
        public static string LocalFolder(string mapId)
        {
            if (mapId == null || !mapId.StartsWith(LocalPrefix, StringComparison.Ordinal))
                return null;

            string name = mapId.Substring(LocalPrefix.Length);
            if (!IsValidLocalName(name))
                return null;

            string root = Path.GetFullPath(LocalRoot);
            string folder = Path.GetFullPath(Path.Combine(root, name));
            string prefix = root.EndsWith(Path.DirectorySeparatorChar.ToString()) ? root : root + Path.DirectorySeparatorChar;

            return folder.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) ? folder : null;
        }

        /// <summary>Which bundle this player reads out of a map folder.</summary>
        public static string PlatformKey
        {
            get
            {
                switch (Application.platform)
                {
                    case RuntimePlatform.WindowsPlayer:
                    case RuntimePlatform.WindowsEditor:
                        return "windows";
                    case RuntimePlatform.LinuxPlayer:
                    case RuntimePlatform.LinuxEditor:
                        return "linux";
                    case RuntimePlatform.OSXPlayer:
                    case RuntimePlatform.OSXEditor:
                        return "macos";
                    default:
                        return "";
                }
            }
        }

        public static bool IsKnownPlatform(string key) => key == "windows" || key == "linux" || key == "macos";

        /// <summary>The one file name a platform's bundle may have. The manifest names its files, but a
        /// manifest is not trusted to choose them - it can only repeat these.</summary>
        public static string BundleFileName(string platformKey) => "map." + platformKey + ".bundle";
    }
}
