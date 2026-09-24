using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using UnityEngine;

namespace Mimicraft.MapKit
{
    /// <summary>
    /// map.json: what a kit map says about itself. Written by the kit's packer, read by the game - and
    /// read as untrusted input, since anyone can write one by hand.
    ///
    /// Nothing in it is believed that can be checked instead. File names must be the fixed ones
    /// (KitMapId.BundleFileName), hashes and sizes are compared against the files themselves, and what
    /// the bundle CONTAINS is never taken from here at all - that is the inspector's job. The manifest
    /// is only trusted to say which hash each platform's bundle should have, and its own SHA-256 is the
    /// map's identity on the wire, which is what makes that trust sufficient.
    /// </summary>
    [Serializable]
    public sealed class KitMapManifest
    {
        /// <summary>1: one bundle per platform. 2: also a bundle for every platform (KitMapId.AnyPlatform),
        /// which is what the kit writes since 2026-09-24. The game reads both.</summary>
        public const int CurrentFormat = 2;
        public const int MinFormat = 1;
        public const int CurrentKitVersion = 1;
        public const int MinKitVersion = 1;
        public const int MaxBytes = 16 * 1024;

        public int formatVersion;
        public int kitVersion;
        public string unityVersion = "";
        public string name = "";
        public string description = "";
        public string author = "";
        public int modes;
        public string sceneName = "";
        public Bundle[] bundles = Array.Empty<Bundle>();

        [Serializable]
        public sealed class Bundle
        {
            public string platform = "";
            public string file = "";
            public string sha256 = "";
            public uint crc;
            public long size;
        }

        public Bundle BundleFor(string platform)
        {
            foreach (Bundle bundle in bundles ?? Array.Empty<Bundle>())
            {
                if (bundle != null && bundle.platform == platform)
                    return bundle;
            }
            return null;
        }

        /// <summary>The bundle a player on this platform opens: one built for it, if the map has one,
        /// otherwise the one for every platform. Null when there is neither.</summary>
        public Bundle BundleToLoad(string platform) => BundleFor(platform) ?? BundleFor(KitMapId.AnyPlatform);

        /// <summary>Reads and checks a folder's map.json. On success <paramref name="sha256"/> is the hex
        /// SHA-256 of the manifest's bytes - the map's identity.</summary>
        public static bool TryRead(string folder, out KitMapManifest manifest, out string sha256, out string why)
        {
            manifest = null;
            sha256 = "";

            string path = Path.Combine(folder, KitMapId.ManifestFile);
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                why = "no " + KitMapId.ManifestFile;
                return false;
            }

            if (info.Length > MaxBytes)
            {
                why = KitMapId.ManifestFile + " is larger than " + MaxBytes + " bytes";
                return false;
            }

            byte[] bytes = File.ReadAllBytes(path);
            sha256 = Sha256Hex(bytes);

            string text;
            try
            {
                text = new UTF8Encoding(false, true).GetString(bytes);
            }
            catch (DecoderFallbackException)
            {
                why = KitMapId.ManifestFile + " is not valid UTF-8";
                return false;
            }

            try
            {
                manifest = JsonUtility.FromJson<KitMapManifest>(text);
            }
            catch (Exception e)
            {
                why = KitMapId.ManifestFile + " is not valid JSON: " + e.Message;
                return false;
            }

            return manifest != null ? manifest.Validate(out why) : Fail("empty manifest", out why);
        }

        bool Validate(out string why)
        {
            if (formatVersion < MinFormat || formatVersion > CurrentFormat)
                return Fail("unknown manifest format " + formatVersion, out why);
            if (kitVersion < MinKitVersion)
                return Fail($"made with kit {kitVersion}; this game reads {MinKitVersion}-{CurrentKitVersion}, the author has to re-pack it", out why);
            if (kitVersion > CurrentKitVersion)
                return Fail($"made with kit {kitVersion}, newer than this game ({CurrentKitVersion}); update the game", out why);
            if (!Bounded(name, 1, 64) || !Bounded(description, 0, 512) || !Bounded(author, 0, 32)
                || !Bounded(sceneName, 1, 128) || !Bounded(unityVersion, 0, 32))
                return Fail("a text field is empty or too long", out why);
            // Format 2 adds the bundle for every platform, so four: it and the three platforms' own.
            int most = formatVersion >= 2 ? 4 : 3;
            if (bundles == null || bundles.Length < 1 || bundles.Length > most)
                return Fail($"must list one to {most} bundles", out why);

            for (int i = 0; i < bundles.Length; i++)
            {
                Bundle bundle = bundles[i];
                bool any = formatVersion >= 2 && bundle != null && bundle.platform == KitMapId.AnyPlatform;
                if (bundle == null || !(any || KitMapId.IsKnownPlatform(bundle.platform)))
                    return Fail("unknown platform in bundle list", out why);
                if (bundle.file != KitMapId.BundleFileName(bundle.platform))
                    return Fail("bundle file for " + bundle.platform + " must be " + KitMapId.BundleFileName(bundle.platform), out why);
                if (!IsHex(bundle.sha256, 64))
                    return Fail("bundle hash for " + bundle.platform + " is not SHA-256 hex", out why);
                if (bundle.size <= 0 || bundle.size > KitMapRules.MaxBundleBytes)
                    return Fail("bundle size for " + bundle.platform + " out of range", out why);

                for (int j = 0; j < i; j++)
                {
                    if (bundles[j].platform == bundle.platform)
                        return Fail("platform " + bundle.platform + " listed twice", out why);
                }
            }

            why = "";
            return true;
        }

        static bool Bounded(string s, int min, int max) => s != null && s.Length >= min && s.Length <= max;

        static bool IsHex(string s, int length)
        {
            if (s == null || s.Length != length)
                return false;
            foreach (char c in s)
            {
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                    return false;
            }
            return true;
        }

        static bool Fail(string reason, out string why)
        {
            why = reason;
            return false;
        }

        public static string Sha256Hex(byte[] bytes)
        {
            using (var sha = SHA256.Create())
                return Hex(sha.ComputeHash(bytes));
        }

        public static string Sha256File(string path)
        {
            using (var sha = SHA256.Create())
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 20))
                return Hex(sha.ComputeHash(stream));
        }

        static string Hex(byte[] hash)
        {
            var text = new StringBuilder(hash.Length * 2);
            foreach (byte b in hash)
                text.Append(b.ToString("x2"));
            return text.ToString();
        }
    }
}
