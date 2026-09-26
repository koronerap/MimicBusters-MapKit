// Pure C#, no UnityEngine: the same file is compiled into an offline .NET harness and run against real
// bundles, which is how it gets tested without an Editor. Keep it that way.
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Mimicraft.MapKit
{
    /// <summary>A bundle refused before Unity was allowed to open it. The message is the reason, in
    /// English, for logs and the author's validator.</summary>
    public sealed class KitBundleRejected : Exception
    {
        public KitBundleRejected(string reason) : base(reason) { }
    }

    /// <summary>What the inspector needs to be told by whoever owns the rules. An interface rather than a
    /// reference to KitMapRules so this file keeps compiling outside Unity.</summary>
    public interface IKitBundlePolicy
    {
        bool IsClassAllowed(int classId);
        bool IsScriptAllowed(string assembly, string fullName);
    }

    /// <summary>Everything the pre-load check learned about a bundle, without Unity having parsed a byte.
    /// </summary>
    public sealed class KitBundleInventory
    {
        public string UnityRevision = "";
        public uint ArchiveVersion;
        public long FileBytes;
        public long UncompressedBytes;
        public int BlockCount;
        public string Compression = "";

        public readonly List<string> Files = new List<string>();
        public readonly List<string> SceneFiles = new List<string>();

        /// <summary>Object count per Unity class id, across every serialized file in the bundle.</summary>
        public readonly SortedDictionary<int, int> ClassCounts = new SortedDictionary<int, int>();

        /// <summary>Object count per script, keyed "Assembly|Namespace.Class". MonoBehaviours AND
        /// ScriptableObjects both appear here - Unity stores both as class 114.</summary>
        public readonly SortedDictionary<string, int> Scripts = new SortedDictionary<string, int>(StringComparer.Ordinal);

        /// <summary>Scripts the bundle DEFINES (its MonoScript objects), whether or not anything uses them.
        /// A kit bundle carries exactly the ones its components use; anything extra is worth a look.</summary>
        public readonly SortedSet<string> MonoScripts = new SortedSet<string>(StringComparer.Ordinal);

        public readonly SortedDictionary<int, int> Layers = new SortedDictionary<int, int>();
        public int GameObjects;

        /// <summary>Type trees and objects read, across every serialized file - the numbers the
        /// inspection's own budgets are held to, reported so a real map shows how far below them it is.
        /// </summary>
        public int TypeCount;
        public long TypeTreeNodes;
        public long ObjectCount;
        public long NodeVisits;

        public int TextureCount;

        /// <summary>What the textures cost, each counted at the larger of what it declares and what its
        /// size and format imply - see KitBundleInspector.ReadTexture.</summary>
        public long TextureBytes;
        public string LargestTexture = "";
        public long LargestTextureBytes;

        /// <summary>The longest side of any texture, in texels.</summary>
        public long LargestTextureSide;

        /// <summary>Every texture as (bytes, description), for the report's largest-first list - the
        /// line an author needs when a map is over its texture budget.</summary>
        public readonly List<(long bytes, string what)> Textures = new List<(long, string)>();

        /// <summary>How the map is lit, as the game's shaders will have to draw it (plan §5.4): the fog
        /// mode when fog is on (-1 off), the lightmap mode when there are lightmaps (-1 none), and whether
        /// realtime GI was baked in.</summary>
        public int FogMode = -1;
        public int LightmapsMode = -1;
        public bool RealtimeGI;

        /// <summary>Sound clips (plan §16.5): how many, what they can hold in memory at most (a clip that
        /// decompresses on load counts as the PCM it unpacks to), the longest, and whatever about one is
        /// refused: codec, channels, rate, or data that is not the FSB5 FMOD expects.</summary>
        public int AudioClipCount;
        public long AudioBytes;
        public double LongestAudioSeconds;
        public readonly List<string> AudioProblems = new List<string>();

        public int MeshCount;
        public long MeshVertices;
        public long MeshTriangles;
        public string LargestMesh = "";
        public long LargestMeshVertices;

        /// <summary>Things the policy refused, with the GameObject they sit on where that is knowable.
        /// Capped per kind so a map made of a thousand cameras does not produce a thousand lines.</summary>
        public readonly List<string> Refused = new List<string>();

        public double Milliseconds;

        public bool Clean => Refused.Count == 0;
    }

    /// <summary>
    /// Reads a Unity AssetBundle (UnityFS container, serialized files, type trees) far enough to list what
    /// is inside it: every class, every script, every texture's size and every mesh's vertex count.
    ///
    /// It exists because loading a bundle is not a neutral act. The moment Unity activates a scene from
    /// one, every MonoBehaviour in it runs Awake with data the author chose - any type the game contains,
    /// not only the handful a map should use. Checking the scene after it loaded is checking after that
    /// code ran. This looks first: if anything outside the policy is in the file, Unity never sees it.
    ///
    /// It is a reader of hostile input, and written like one. Every count and size read from the file is
    /// bounded before it is used, nothing is allocated from a number the file supplied without a cap, a
    /// node-visit budget stops a type tree built to be walked forever, and any inconsistency is a refusal
    /// rather than a guess. Managed code cannot overrun a buffer; the worst a bad file can do here is make
    /// this throw, and every throw becomes KitBundleRejected.
    ///
    /// The format is the one Unity 6000.3 writes (archive version 6-8, serialized file version 22); the
    /// layouts follow the community readers that have tracked it for years (UnityPy, AssetStudio).
    /// </summary>
    public static class KitBundleInspector
    {
        // ------------------------------------------------------------------ limits
        const int MaxBlocksInfoBytes = 4 << 20;
        const int MaxBlocks = 1 << 15;
        const int MaxNodes = 64;
        const int MaxBlockBytes = 4 << 20;
        const long MaxUncompressedBytes = 2L << 30;
        const int MaxTypes = 8192;
        const int MaxTypeTreeNodes = 1 << 16;
        const int MaxStringBuffer = 4 << 20;
        const int MaxObjects = 1 << 21;
        const int MaxScriptTypes = 1 << 16;
        const int MaxExternals = 4096;
        const int MaxStringBytes = 1 << 16;
        const int MaxArrayCount = 1 << 28;
        // Phase 0's spike took 12,737 visits. Ten million is hundreds of real maps' worth, and under a
        // second of walking even on Mono.
        const long NodeVisitBudget = 10_000_000;

        // Totals for the whole inspection. The caps above hold one count at a time; a bundle may carry 64
        // files of 8,192 types each, and it is the sum that becomes memory (see Totals).
        const long MaxTotalTypeTreeNodes = 1 << 18;
        const long MaxTotalTypeTreeStringBytes = 16 << 20;
        const long MaxTotalObjects = 1 << 18;
        const int SamplesPerKind = 3;

        public static KitBundleInventory Inspect(string path, IKitBundlePolicy policy)
        {
            using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1 << 16))
                return Inspect(stream, policy);
        }

        public static KitBundleInventory Inspect(Stream stream, IKitBundlePolicy policy)
        {
            var clock = System.Diagnostics.Stopwatch.StartNew();
            var inventory = new KitBundleInventory { FileBytes = stream.Length };

            try
            {
                var archive = new Archive(stream);
                inventory.UnityRevision = archive.UnityRevision;
                inventory.ArchiveVersion = archive.FormatVersion;
                inventory.UncompressedBytes = archive.TotalUncompressed;
                inventory.BlockCount = archive.BlockCount;
                inventory.Compression = archive.CompressionSummary;

                var files = new Dictionary<string, SerializedFile>(StringComparer.Ordinal);
                var totals = new Totals();
                foreach (Node node in archive.Nodes)
                {
                    inventory.Files.Add($"{node.Path} ({node.Size} B{(node.IsSerialized ? ", serialized" : "")})");
                    if (!node.IsSerialized)
                        continue;

                    var file = new SerializedFile(archive, node, totals);
                    files[node.Path] = file;
                    if (node.Path.StartsWith("BuildPlayer-", StringComparison.Ordinal)
                        && !node.Path.EndsWith(".sharedAssets", StringComparison.Ordinal))
                        inventory.SceneFiles.Add(node.Path);
                }

                if (files.Count == 0)
                    throw new KitBundleRejected("the bundle contains no serialized file");

                inventory.TypeCount = totals.Types;
                inventory.TypeTreeNodes = totals.TypeTreeNodes;
                inventory.ObjectCount = totals.Objects;

                var walker = new Walker();
                foreach (SerializedFile file in files.Values)
                    Survey(file, files, walker, policy, inventory, archive);
                inventory.NodeVisits = walker.Visits;
            }
            catch (KitBundleRejected)
            {
                throw;
            }
            catch (Exception e)
            {
                // Whatever else a malformed file provokes - a read past a range, an arithmetic overflow, an
                // index out of bounds - means the same thing: this is not a bundle the kit wrote.
                throw new KitBundleRejected("corrupt bundle: " + e.GetType().Name + ": " + e.Message);
            }

            inventory.Milliseconds = clock.Elapsed.TotalMilliseconds;
            return inventory;
        }

        // ------------------------------------------------------------------ survey

        static void Survey(SerializedFile file, Dictionary<string, SerializedFile> files, Walker walker,
            IKitBundlePolicy policy, KitBundleInventory inventory, Archive archive)
        {
            var refusedSamples = new Dictionary<string, int>();

            // Scripts are resolved once per type, not per object: every distinct script gets its own
            // SerializedType (its type tree is the script's fields), so the type IS the script.
            var scriptOfType = new Dictionary<SerializedType, (string asm, string name)>();
            foreach (SerializedType type in file.Types)
            {
                if (type.ClassId != 114)
                    continue;

                scriptOfType[type] = ResolveScript(file, type, files, walker);
            }

            foreach (ObjectInfo obj in file.Objects)
            {
                int classId = obj.ClassId;
                inventory.ClassCounts.TryGetValue(classId, out int count);
                inventory.ClassCounts[classId] = count + 1;

                if (classId == 114 && obj.Type != null && scriptOfType.TryGetValue(obj.Type, out var script))
                {
                    string key = script.asm + "|" + script.name;
                    inventory.Scripts.TryGetValue(key, out int scriptCount);
                    inventory.Scripts[key] = scriptCount + 1;

                    if (!policy.IsScriptAllowed(script.asm, script.name))
                        Refuse(inventory, refusedSamples, "script " + script.name + " (" + script.asm + ")",
                            OwnerName(file, obj, files, walker));
                }
                else if (!policy.IsClassAllowed(classId))
                {
                    Refuse(inventory, refusedSamples, ClassName(classId) + " (" + classId + ")",
                        OwnerName(file, obj, files, walker));
                }

                switch (classId)
                {
                    case 1:
                        ReadGameObject(file, obj, walker, inventory);
                        break;
                    case 115:
                        var definition = ReadMonoScript(file, obj, walker);
                        inventory.MonoScripts.Add(definition.asm + "|" + definition.name);
                        break;
                    case 28:
                    case 89:
                    case 117:
                    case 187:
                    case 188:
                        ReadTexture(file, obj, walker, inventory);
                        break;
                    case 43:
                        ReadMesh(file, obj, walker, inventory);
                        break;
                    case 104:
                        ReadRenderSettings(file, obj, walker, inventory);
                        break;
                    case 157:
                        ReadLightmapSettings(file, obj, walker, inventory);
                        break;
                    case 83:
                        ReadAudioClip(file, obj, walker, inventory, archive);
                        break;
                }
            }
        }

        static void Refuse(KitBundleInventory inventory, Dictionary<string, int> samples, string what, string owner)
        {
            samples.TryGetValue(what, out int seen);
            samples[what] = seen + 1;

            if (seen < SamplesPerKind)
                inventory.Refused.Add(owner == null ? what : what + " on '" + owner + "'");
            else if (seen == SamplesPerKind)
                inventory.Refused.Add(what + ": more of these");
        }

        static (string asm, string name) ResolveScript(SerializedFile file, SerializedType type,
            Dictionary<string, SerializedFile> files, Walker walker)
        {
            if (type.ScriptTypeIndex < 0 || type.ScriptTypeIndex >= file.ScriptTypes.Count)
                return ("?", "unresolved script (no script type index)");

            (int fileIndex, long pathId) = file.ScriptTypes[type.ScriptTypeIndex];
            SerializedFile target = file.Resolve(fileIndex, files);
            if (target == null)
                return ("?", "unresolved script (external file " + file.ExternalName(fileIndex) + ")");

            if (!target.ByPathId.TryGetValue(pathId, out ObjectInfo monoScript) || monoScript.ClassId != 115)
                return ("?", "unresolved script (no MonoScript at " + pathId + ")");

            return ReadMonoScript(target, monoScript, walker);
        }

        static (string asm, string name) ReadMonoScript(SerializedFile file, ObjectInfo monoScript, Walker walker)
        {
            var wanted = new FieldRequest("m_ClassName", "m_Namespace", "m_AssemblyName");
            walker.Read(file, monoScript, wanted);

            string className = wanted.String("m_ClassName");
            string ns = wanted.String("m_Namespace");
            string asm = wanted.String("m_AssemblyName");
            if (string.IsNullOrEmpty(className))
                return ("?", "unresolved script (empty MonoScript)");

            // Unity stores the assembly with its extension ("Mimicraft.MapKit.dll").
            if (asm.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                asm = asm.Substring(0, asm.Length - 4);

            return (asm, string.IsNullOrEmpty(ns) ? className : ns + "." + className);
        }

        /// <summary>The name of the GameObject a component sits on, for messages. Every Component's first
        /// field is m_GameObject; anything else (an asset, a ScriptableObject) has no owner.</summary>
        static string OwnerName(SerializedFile file, ObjectInfo obj, Dictionary<string, SerializedFile> files,
            Walker walker)
        {
            if (obj.Type?.Nodes == null || obj.Type.Nodes.Length < 2 || obj.Type.Nodes[1].Name != "m_GameObject")
                return null;

            try
            {
                var request = new FieldRequest("m_GameObject.m_FileID", "m_GameObject.m_PathID");
                walker.Read(file, obj, request);

                SerializedFile target = file.Resolve((int)request.Long("m_GameObject.m_FileID"), files);
                if (target == null || !target.ByPathId.TryGetValue(request.Long("m_GameObject.m_PathID"), out ObjectInfo go))
                    return null;

                var name = new FieldRequest("m_Name");
                walker.Read(target, go, name);
                return name.String("m_Name");
            }
            catch (KitBundleRejected)
            {
                return null;
            }
        }

        static void ReadGameObject(SerializedFile file, ObjectInfo obj, Walker walker, KitBundleInventory inventory)
        {
            var request = new FieldRequest("m_Layer");
            walker.Read(file, obj, request);

            int layer = (int)request.Long("m_Layer");
            inventory.GameObjects++;
            inventory.Layers.TryGetValue(layer, out int count);
            inventory.Layers[layer] = count + 1;
        }

        static void ReadTexture(SerializedFile file, ObjectInfo obj, Walker walker, KitBundleInventory inventory)
        {
            var request = new FieldRequest("m_Name", "m_Width", "m_Height", "m_Depth", "m_CompleteImageSize",
                "m_ImageCount", "m_DataSize", "m_TextureFormat", "m_Format", "m_MipCount", "m_StreamData.size");
            walker.Read(file, obj, request);

            // Stored size is the honest number for compressed formats, which is what maps use: a BC7 texel
            // costs the same in VRAM as on disk. m_CompleteImageSize is one image; cubes carry six.
            long declared = request.Has("m_CompleteImageSize")
                ? request.Long("m_CompleteImageSize") * Math.Max(1, request.Long("m_ImageCount"))
                : request.Long("m_DataSize");
            if (declared <= 0)
                declared = request.Long("m_StreamData.size");

            // But declared is the file's word, and the GPU allocates by size and format. A crunched texture
            // declares a fraction of what it unpacks to; a hostile one can declare anything. So the budget
            // takes the larger of the two.
            long width = Math.Max(0, request.Long("m_Width"));
            long height = Math.Max(0, request.Long("m_Height"));
            long layers = Math.Max(1, Math.Max(request.Long("m_Depth"), request.Long("m_ImageCount")));
            long bits = obj.ClassId == 28 || obj.ClassId == 89 ? BitsPerTexel(request.Long("m_TextureFormat")) : WorstBitsPerTexel;
            long estimated = width * height * layers * bits / 8;
            if (request.Long("m_MipCount") > 1)
                estimated += estimated / 3;
            long bytes = Math.Max(declared, estimated);

            inventory.TextureCount++;
            inventory.TextureBytes += Math.Max(0, bytes);
            inventory.LargestTextureSide = Math.Max(inventory.LargestTextureSide, Math.Max(width, height));

            string what = $"'{request.String("m_Name")}' {request.Long("m_Width")}x{request.Long("m_Height")}"
                + (request.Has("m_Depth") ? "x" + request.Long("m_Depth") : "")
                + $" format {(request.Has("m_TextureFormat") ? request.Long("m_TextureFormat") : request.Long("m_Format"))}"
                + $" mips {request.Long("m_MipCount")}";
            inventory.Textures.Add((bytes, what));

            if (bytes > inventory.LargestTextureBytes)
            {
                inventory.LargestTextureBytes = bytes;
                inventory.LargestTexture = what;
            }
        }

        const int WorstBitsPerTexel = 128;

        /// <summary>Bits per texel of a TextureFormat, as the GPU holds it (a crunched format is counted as
        /// what it unpacks to). Formats a map has no reason to use are counted at the worst case, RGBAFloat,
        /// which errs toward refusing rather than toward a budget that can be talked down.</summary>
        static long BitsPerTexel(long format)
        {
            switch (format)
            {
                case 1: case 63: case 75: return 8;                         // Alpha8, R8
                case 2: case 7: case 9: case 13: case 15: case 21:          // 16-bit formats, R16, RHalf, YUY2
                case 62: case 76: case 79: return 16;                       // RG16, signed 16-bit
                case 3: case 77: return 24;                                 // RGB24
                case 4: case 5: case 14: case 16: case 18: case 22:         // 32-bit formats
                case 72: case 78: case 80: return 32;
                case 73: case 81: return 48;                                // RGB48
                case 17: case 19: case 74: case 82: return 64;              // RGBAHalf, RGFloat, RGBA64
                case 20: return 128;                                        // RGBAFloat
                case 10: case 26: case 28: return 4;                        // DXT1, BC4, DXT1 crunched
                case 12: case 24: case 25: case 27: case 29: return 8;      // DXT5, BC6H, BC7, BC5, DXT5 crunched
                default:
                    // ETC/EAC/ASTC/PVRTC are 8 bits or fewer; anything else is not a map texture.
                    return format >= 30 && format <= 71 ? 8 : WorstBitsPerTexel;
            }
        }

        static void ReadRenderSettings(SerializedFile file, ObjectInfo obj, Walker walker, KitBundleInventory inventory)
        {
            var request = new FieldRequest("m_Fog", "m_FogMode");
            walker.Read(file, obj, request);
            if (request.Long("m_Fog") != 0)
                inventory.FogMode = (int)request.Long("m_FogMode");
        }

        static void ReadLightmapSettings(SerializedFile file, ObjectInfo obj, Walker walker, KitBundleInventory inventory)
        {
            var request = new FieldRequest("m_Lightmaps", "m_LightmapsMode", "m_GISettings.m_EnableRealtimeLightmaps");
            walker.Read(file, obj, request);
            if (request.Long("m_Lightmaps") > 0)
                inventory.LightmapsMode = (int)request.Long("m_LightmapsMode");
            inventory.RealtimeGI |= request.Long("m_GISettings.m_EnableRealtimeLightmaps") != 0;
        }

        /// <summary>A sound clip's settings, and the header of its data: FMOD decodes the data, and like the
        /// PNG decoder behind a card picture it is not something to hand arbitrary bytes. Only the codecs
        /// Unity writes pass, one sound per clip, with sizes that stay inside the clip's own data.</summary>
        static void ReadAudioClip(SerializedFile file, ObjectInfo obj, Walker walker, KitBundleInventory inventory, Archive archive)
        {
            var request = new FieldRequest("m_Name", "m_LoadType", "m_Channels", "m_Frequency", "m_Length",
                "m_IsTrackerFormat", "m_CompressionFormat", "m_Resource.m_Source", "m_Resource.m_Offset", "m_Resource.m_Size");
            walker.Read(file, obj, request);

            string name = "'" + request.String("m_Name") + "'";
            long channels = request.Long("m_Channels");
            long frequency = request.Long("m_Frequency");
            double seconds = request.Double("m_Length");
            long size = request.Long("m_Resource.m_Size");
            if (double.IsNaN(seconds) || double.IsInfinity(seconds) || seconds < 0d)
                seconds = double.MaxValue;

            inventory.AudioClipCount++;
            inventory.LongestAudioSeconds = Math.Max(inventory.LongestAudioSeconds, seconds);

            // Decompress On Load keeps 16-bit PCM of the whole clip; the other load types keep what is stored.
            double pcm = Math.Min(seconds * Math.Max(0, Math.Min(frequency, 192000)) * Math.Max(0, Math.Min(channels, 8)) * 2d, 1e12);
            long resident = request.Long("m_LoadType") == 0 ? Math.Max(size, (long)pcm) : size;
            inventory.AudioBytes += Math.Max(0, resident);

            if (channels < 1 || channels > 2)
                inventory.AudioProblems.Add($"sound {name} has {channels} channels; maps use mono or stereo");
            if (frequency < 1 || frequency > 48000)
                inventory.AudioProblems.Add($"sound {name} is at {frequency} Hz; the limit is 48000");
            if (request.Long("m_IsTrackerFormat") != 0)
                inventory.AudioProblems.Add($"sound {name} is a tracker module; maps use ordinary clips");
            long compression = request.Long("m_CompressionFormat");
            if (compression < 0 || compression > 2)
                inventory.AudioProblems.Add($"sound {name} is compressed as format {compression}; maps use PCM, Vorbis or ADPCM");

            string header = CheckFsb5(archive, request.String("m_Resource.m_Source"), request.Long("m_Resource.m_Offset"), size);
            if (header != null)
                inventory.AudioProblems.Add($"sound {name}: {header}");
        }

        /// <summary>Why a clip's data is not a plain FSB5 sound, or null when it is one.</summary>
        static string CheckFsb5(Archive archive, string source, long offset, long size)
        {
            if (size <= 0)
                return "no audio data";

            // "archive:/CAB-.../CAB-....resource": the node is the last part of the path.
            string fileName = source ?? "";
            int slash = fileName.LastIndexOf('/');
            if (slash >= 0)
                fileName = fileName.Substring(slash + 1);

            Node node = archive.Nodes.Find(n => n.Path == fileName);
            if (node == null)
                return "its data is not in the bundle";
            if (offset < 0 || size > node.Size || offset > node.Size - size)
                return "its data runs past its file";

            const int HeaderV1 = 60;
            if (size < HeaderV1 + 4)
                return "its data is too short";

            var header = new byte[HeaderV1 + 4];
            archive.Read(node.Offset + offset, header, 0, header.Length);
            if (header[0] != (byte)'F' || header[1] != (byte)'S' || header[2] != (byte)'B' || header[3] != (byte)'5')
                return "its data is not FSB5";

            uint version = BitConverter.ToUInt32(header, 4);
            uint samples = BitConverter.ToUInt32(header, 8);
            uint sampleHeaders = BitConverter.ToUInt32(header, 12);
            uint names = BitConverter.ToUInt32(header, 16);
            uint data = BitConverter.ToUInt32(header, 20);
            uint codec = BitConverter.ToUInt32(header, 24);

            if (version > 1)
                return $"FSB5 version {version}";
            if (samples != 1)
                return $"{samples} sounds in one clip";

            long headerSize = version == 0 ? HeaderV1 + 4 : HeaderV1;
            if (headerSize + (long)sampleHeaders + names + data > size)
                return "its FSB5 sizes run past the clip's data";

            // PCM 8/16/24/32/float, IMA ADPCM, Vorbis, FADPCM: what Unity's importer writes.
            bool known = (codec >= 1 && codec <= 5) || codec == 7 || codec == 15 || codec == 16;
            return known ? null : $"FSB5 codec {codec}";
        }

        static void ReadMesh(SerializedFile file, ObjectInfo obj, Walker walker, KitBundleInventory inventory)
        {
            var request = new FieldRequest("m_Name", "m_IndexFormat", "m_IndexBuffer", "m_VertexData.m_VertexCount");
            walker.Read(file, obj, request);

            long vertices = request.Long("m_VertexData.m_VertexCount");
            long indexBytes = request.Long("m_IndexBuffer");
            long indices = request.Long("m_IndexFormat") == 1 ? indexBytes / 4 : indexBytes / 2;

            inventory.MeshCount++;
            inventory.MeshVertices += vertices;
            inventory.MeshTriangles += indices / 3;

            if (vertices > inventory.LargestMeshVertices)
            {
                inventory.LargestMeshVertices = vertices;
                inventory.LargestMesh = $"'{request.String("m_Name")}' {vertices} vertices, {indices / 3} triangles";
            }
        }

        // ------------------------------------------------------------------ report

        public static string Report(KitBundleInventory inventory)
        {
            var text = new StringBuilder();
            text.AppendLine($"Unity {inventory.UnityRevision}, archive v{inventory.ArchiveVersion}, "
                + $"{inventory.FileBytes / 1024} KB on disk, {inventory.UncompressedBytes / 1024} KB unpacked, "
                + $"{inventory.BlockCount} blocks ({inventory.Compression}), read in {inventory.Milliseconds:0} ms");
            text.AppendLine("files: " + string.Join(", ", inventory.Files));
            text.AppendLine($"read: {inventory.TypeCount} types, {inventory.TypeTreeNodes} type tree nodes, "
                + $"{inventory.ObjectCount} objects, {inventory.NodeVisits} field visits");
            text.AppendLine("scenes: " + (inventory.SceneFiles.Count == 0 ? "none" : string.Join(", ", inventory.SceneFiles)));
            text.AppendLine($"GameObjects {inventory.GameObjects}, layers "
                + string.Join(", ", Pairs(inventory.Layers)));
            text.AppendLine($"textures {inventory.TextureCount}, {inventory.TextureBytes / (1024 * 1024.0):0.0} MB"
                + (inventory.TextureCount > 0 ? $", largest {inventory.LargestTexture}" : ""));
            text.AppendLine($"meshes {inventory.MeshCount}, {inventory.MeshVertices} vertices, {inventory.MeshTriangles} triangles"
                + (inventory.MeshCount > 0 ? $", largest {inventory.LargestMesh}" : ""));
            if (inventory.AudioClipCount > 0)
                text.AppendLine($"sounds {inventory.AudioClipCount}, {inventory.AudioBytes / (1024 * 1024.0):0.0} MB in memory at most, "
                    + $"longest {inventory.LongestAudioSeconds:0.0} s");

            if (inventory.Textures.Count > 0)
            {
                var largest = new List<(long bytes, string what)>(inventory.Textures);
                largest.Sort((a, b) => b.bytes.CompareTo(a.bytes));
                text.AppendLine("largest textures:");
                for (int i = 0; i < Math.Min(10, largest.Count); i++)
                    text.AppendLine($"  {largest[i].bytes / (1024 * 1024.0),6:0.0} MB  {largest[i].what}");
            }

            text.AppendLine("classes:");
            foreach (var pair in inventory.ClassCounts)
                text.AppendLine($"  {pair.Value,6}  {ClassName(pair.Key)} ({pair.Key})");

            text.AppendLine("scripts:");
            foreach (var pair in inventory.Scripts)
                text.AppendLine($"  {pair.Value,6}  {pair.Key}");

            text.AppendLine("script definitions: " + (inventory.MonoScripts.Count == 0 ? "none" : string.Join(", ", inventory.MonoScripts)));

            if (inventory.Clean)
                text.AppendLine("policy: nothing refused");
            else
            {
                text.AppendLine($"policy: REFUSED ({inventory.Refused.Count})");
                foreach (string line in inventory.Refused)
                    text.AppendLine("  - " + line);
            }

            return text.ToString();
        }

        static IEnumerable<string> Pairs(SortedDictionary<int, int> map)
        {
            foreach (var pair in map)
                yield return pair.Key + ":" + pair.Value;
        }

        /// <summary>Names for the class ids a scene bundle is likely to hold, so a report reads as words.
        /// Only for display; the policy works on the ids.</summary>
        public static string ClassName(int classId)
        {
            switch (classId)
            {
                case 1: return "GameObject";
                case 4: return "Transform";
                case 20: return "Camera";
                case 21: return "Material";
                case 23: return "MeshRenderer";
                case 25: return "Renderer";
                case 28: return "Texture2D";
                case 29: return "OcclusionCullingSettings";
                case 33: return "MeshFilter";
                case 41: return "OcclusionPortal";
                case 43: return "Mesh";
                case 45: return "Skybox";
                case 48: return "Shader";
                case 49: return "TextAsset";
                case 54: return "Rigidbody";
                case 64: return "MeshCollider";
                case 65: return "BoxCollider";
                case 72: return "ComputeShader";
                case 74: return "AnimationClip";
                case 81: return "AudioListener";
                case 82: return "AudioSource";
                case 83: return "AudioClip";
                case 84: return "RenderTexture";
                case 89: return "Cubemap";
                case 90: return "Avatar";
                case 91: return "AnimatorController";
                case 95: return "Animator";
                case 96: return "TrailRenderer";
                case 102: return "TextMesh";
                case 104: return "RenderSettings";
                case 108: return "Light";
                case 111: return "Animation";
                case 114: return "MonoBehaviour";
                case 115: return "MonoScript";
                case 117: return "Texture3D";
                case 119: return "Projector";
                case 120: return "LineRenderer";
                case 128: return "Font";
                case 135: return "SphereCollider";
                case 136: return "CapsuleCollider";
                case 137: return "SkinnedMeshRenderer";
                case 142: return "AssetBundle";
                case 143: return "CharacterController";
                case 150: return "PreloadData";
                case 154: return "TerrainCollider";
                case 156: return "TerrainData";
                case 157: return "LightmapSettings";
                case 187: return "Texture2DArray";
                case 188: return "CubemapArray";
                case 192: return "OcclusionArea";
                case 196: return "NavMeshSettings";
                case 198: return "ParticleSystem";
                case 199: return "ParticleSystemRenderer";
                case 200: return "ShaderVariantCollection";
                case 205: return "LODGroup";
                case 212: return "SpriteRenderer";
                case 213: return "Sprite";
                case 215: return "ReflectionProbe";
                case 218: return "Terrain";
                case 220: return "LightProbeGroup";
                case 222: return "CanvasRenderer";
                case 223: return "Canvas";
                case 224: return "RectTransform";
                case 225: return "CanvasGroup";
                case 238: return "NavMeshData";
                case 258: return "LightProbes";
                case 259: return "LightProbeProxyVolume";
                case 320: return "PlayableDirector";
                case 328: return "VideoPlayer";
                case 329: return "VideoClip";
                case 363: return "OcclusionCullingData";
                case 1120: return "LightingDataAsset";
                case 850595691: return "LightingSettings";
                default: return "class";
            }
        }

        // ================================================================== reading

        sealed class Node
        {
            public long Offset;
            public long Size;
            public uint Flags;
            public string Path = "";

            // Serialized files carry flag 4; the .resS/.resource streams beside them do not.
            public bool IsSerialized => (Flags & 4) != 0;
        }

        /// <summary>The UnityFS container: a header, a (usually compressed) table of blocks and files, and
        /// the blocks themselves. Exposes the files as ranges of one uncompressed address space, and
        /// decompresses only the blocks a read actually touches.</summary>
        sealed class Archive
        {
            readonly Stream file;
            readonly uint[] blockUncompressed;
            readonly uint[] blockCompressed;
            readonly ushort[] blockFlags;
            readonly long[] blockUStart;
            readonly long[] blockCStart;

            public readonly List<Node> Nodes = new List<Node>();
            public readonly string UnityRevision;
            public readonly uint FormatVersion;
            public long TotalUncompressed;
            public int BlockCount => blockUncompressed.Length;
            public string CompressionSummary = "";

            int cachedBlock = -1;
            byte[] cachedData;
            int cachedLength;
            byte[] compressedScratch;

            public Archive(Stream file)
            {
                this.file = file;
                var reader = new BinReader(new StreamSource(file), bigEndian: true);

                string signature = reader.CString(16);
                if (signature != "UnityFS")
                    throw new KitBundleRejected("not a UnityFS bundle (signature '" + signature + "')");

                FormatVersion = reader.U32();
                if (FormatVersion < 6 || FormatVersion > 8)
                    throw new KitBundleRejected("unsupported archive version " + FormatVersion);

                reader.CString(32);                      // "5.x.x"
                UnityRevision = reader.CString(64);      // "6000.3.8f1"

                long size = reader.I64();
                if (size != file.Length)
                    throw new KitBundleRejected($"header says {size} bytes, file is {file.Length}");

                uint compressedInfo = reader.U32();
                uint uncompressedInfo = reader.U32();
                uint flags = reader.U32();

                const uint known = 0x3F | 0x40 | 0x80 | 0x200;
                if ((flags & ~known) != 0)
                    throw new KitBundleRejected($"unknown archive flags 0x{flags:X}");
                if (compressedInfo == 0 || compressedInfo > MaxBlocksInfoBytes || uncompressedInfo > MaxBlocksInfoBytes)
                    throw new KitBundleRejected("block table size out of range");

                if (FormatVersion >= 7)
                    reader.Align(16);

                long afterHeader = reader.Position;
                bool infoAtEnd = (flags & 0x80) != 0;
                long infoPosition = infoAtEnd ? file.Length - compressedInfo : afterHeader;
                if (infoPosition < afterHeader || infoPosition + compressedInfo > file.Length)
                    throw new KitBundleRejected("block table outside the file");

                reader.Position = infoPosition;
                byte[] packed = reader.Bytes((int)compressedInfo);
                byte[] info = Decompress(packed, (int)compressedInfo, (int)uncompressedInfo, (int)(flags & 0x3F));

                long dataStart = infoAtEnd ? afterHeader : infoPosition + compressedInfo;
                if ((flags & 0x200) != 0)
                    dataStart = (dataStart + 15) & ~15L;

                var table = new BinReader(new ArraySource(info, info.Length), bigEndian: true);
                table.Bytes(16);                         // hash of the uncompressed data, unused here
                int blocks = table.I32();
                if (blocks < 0 || blocks > MaxBlocks)
                    throw new KitBundleRejected("block count out of range: " + blocks);

                blockUncompressed = new uint[blocks];
                blockCompressed = new uint[blocks];
                blockFlags = new ushort[blocks];
                blockUStart = new long[blocks];
                blockCStart = new long[blocks];

                long u = 0, c = dataStart;
                var kinds = new SortedSet<string>();
                for (int i = 0; i < blocks; i++)
                {
                    blockUncompressed[i] = table.U32();
                    blockCompressed[i] = table.U32();
                    blockFlags[i] = table.U16();

                    int kind = blockFlags[i] & 0x3F;
                    kinds.Add(kind == 0 ? "none" : kind == 1 ? "LZMA" : kind == 2 ? "LZ4" : kind == 3 ? "LZ4HC" : "type " + kind);

                    // Here, for every block, rather than when a block is read: this reader only reads what
                    // it inspects, and Unity decompresses the rest - LZMA included - when it loads the map.
                    if (kind != 0 && kind != 2 && kind != 3)
                        throw new KitBundleRejected("unsupported block compression " + kind + " (the kit writes LZ4)");
                    if (kind != 0 && blockUncompressed[i] > MaxBlockBytes)
                        throw new KitBundleRejected("compressed block too large: " + blockUncompressed[i]);

                    // Read() does its arithmetic within a block in int. The total below allows 2 GB, which
                    // is one more byte than an int holds, so a stored block of exactly that size would turn
                    // negative there.
                    if (blockUncompressed[i] > int.MaxValue)
                        throw new KitBundleRejected("stored block too large: " + blockUncompressed[i]);
                    if (kind == 0 && blockCompressed[i] != blockUncompressed[i])
                        throw new KitBundleRejected("stored block with mismatched sizes");

                    blockUStart[i] = u;
                    blockCStart[i] = c;
                    u += blockUncompressed[i];
                    c += blockCompressed[i];
                    if (u > MaxUncompressedBytes)
                        throw new KitBundleRejected("bundle unpacks to more than " + (MaxUncompressedBytes >> 20) + " MB");
                }

                if (c > (infoAtEnd ? infoPosition : file.Length))
                    throw new KitBundleRejected("blocks run past the end of the file");

                TotalUncompressed = u;
                CompressionSummary = string.Join("+", kinds);

                int nodes = table.I32();
                if (nodes < 1 || nodes > MaxNodes)
                    throw new KitBundleRejected("file count out of range: " + nodes);

                for (int i = 0; i < nodes; i++)
                {
                    var node = new Node
                    {
                        Offset = table.I64(),
                        Size = table.I64(),
                        Flags = table.U32(),
                        Path = table.CString(256),
                    };

                    if (node.Offset < 0 || node.Size < 0 || node.Offset + node.Size > TotalUncompressed)
                        throw new KitBundleRejected("file '" + node.Path + "' lies outside the bundle");
                    if (node.Path.Length == 0 || node.Path.IndexOfAny(new[] { '/', '\\', ':' }) >= 0 || node.Path.Contains(".."))
                        throw new KitBundleRejected("bad file name in bundle: '" + node.Path + "'");

                    Nodes.Add(node);
                }
            }

            /// <summary>Copies uncompressed bytes [position, position+count) into buffer.</summary>
            public void Read(long position, byte[] buffer, int offset, int count)
            {
                while (count > 0)
                {
                    int block = FindBlock(position);
                    int within = (int)(position - blockUStart[block]);
                    int available = (int)blockUncompressed[block] - within;
                    int take = Math.Min(available, count);

                    if ((blockFlags[block] & 0x3F) == 0)
                    {
                        file.Position = blockCStart[block] + within;
                        ReadExactly(file, buffer, offset, take);
                    }
                    else
                    {
                        EnsureCached(block);
                        Buffer.BlockCopy(cachedData, within, buffer, offset, take);
                    }

                    position += take;
                    offset += take;
                    count -= take;
                }
            }

            int FindBlock(long position)
            {
                if (position < 0 || position >= TotalUncompressed)
                    throw new EndOfStreamException("read past the end of the bundle");

                int lo = 0, hi = blockUStart.Length - 1;
                while (lo < hi)
                {
                    int mid = (lo + hi + 1) >> 1;
                    if (blockUStart[mid] <= position)
                        lo = mid;
                    else
                        hi = mid - 1;
                }

                // Zero-length blocks share a start with the next one; skip forward to the one that has bytes.
                while (lo < blockUStart.Length - 1 && blockUncompressed[lo] == 0)
                    lo++;

                return lo;
            }

            void EnsureCached(int block)
            {
                if (cachedBlock == block)
                    return;

                int packedSize = (int)blockCompressed[block];
                if (packedSize > MaxBlockBytes * 2)
                    throw new KitBundleRejected("compressed block too large: " + packedSize);

                if (compressedScratch == null || compressedScratch.Length < packedSize)
                    compressedScratch = new byte[Math.Max(packedSize, 1 << 17)];

                file.Position = blockCStart[block];
                ReadExactly(file, compressedScratch, 0, packedSize);

                int size = (int)blockUncompressed[block];
                if (cachedData == null || cachedData.Length < size)
                    cachedData = new byte[Math.Max(size, 1 << 17)];

                int kind = blockFlags[block] & 0x3F;
                if (kind != 2 && kind != 3)
                    throw new KitBundleRejected("unsupported block compression " + kind + " (the kit writes LZ4)");

                int written = Lz4.Decode(compressedScratch, packedSize, cachedData, size);
                if (written != size)
                    throw new KitBundleRejected("block unpacked to the wrong size");

                cachedBlock = block;
                cachedLength = size;
            }

            static byte[] Decompress(byte[] packed, int packedSize, int size, int kind)
            {
                if (kind == 0)
                {
                    if (packedSize != size)
                        throw new KitBundleRejected("stored block table with mismatched sizes");
                    return packed;
                }

                if (kind != 2 && kind != 3)
                    throw new KitBundleRejected("unsupported block table compression " + kind);

                var output = new byte[size];
                if (Lz4.Decode(packed, packedSize, output, size) != size)
                    throw new KitBundleRejected("block table unpacked to the wrong size");
                return output;
            }
        }

        static void ReadExactly(Stream stream, byte[] buffer, int offset, int count)
        {
            while (count > 0)
            {
                int read = stream.Read(buffer, offset, count);
                if (read <= 0)
                    throw new EndOfStreamException();
                offset += read;
                count -= read;
            }
        }

        /// <summary>LZ4 block format decoder, bounds-checked on every step - the input is untrusted.</summary>
        static class Lz4
        {
            public static int Decode(byte[] src, int srcLength, byte[] dst, int dstLength)
            {
                int s = 0, d = 0;
                while (s < srcLength)
                {
                    int token = src[s++];

                    int literal = token >> 4;
                    if (literal == 15)
                        literal = ExtendLength(src, srcLength, ref s, literal, dstLength);

                    if (literal > srcLength - s || literal > dstLength - d)
                        throw new KitBundleRejected("corrupt LZ4 literal run");
                    Buffer.BlockCopy(src, s, dst, d, literal);
                    s += literal;
                    d += literal;

                    if (s >= srcLength)
                        break;

                    if (srcLength - s < 2)
                        throw new KitBundleRejected("corrupt LZ4 match offset");
                    int matchOffset = src[s] | (src[s + 1] << 8);
                    s += 2;
                    if (matchOffset == 0 || matchOffset > d)
                        throw new KitBundleRejected("corrupt LZ4 match offset");

                    int match = token & 15;
                    if (match == 15)
                        match = ExtendLength(src, srcLength, ref s, match, dstLength);
                    match += 4;
                    if (match > dstLength - d)
                        throw new KitBundleRejected("corrupt LZ4 match length");

                    int from = d - matchOffset;
                    if (matchOffset >= match)
                    {
                        Buffer.BlockCopy(dst, from, dst, d, match);
                        d += match;
                    }
                    else
                    {
                        for (int i = 0; i < match; i++)
                            dst[d++] = dst[from++];
                    }
                }

                return d;
            }

            static int ExtendLength(byte[] src, int srcLength, ref int s, int length, int limit)
            {
                int b;
                do
                {
                    if (s >= srcLength)
                        throw new KitBundleRejected("corrupt LZ4 length");
                    b = src[s++];
                    length += b;
                    if (length > limit + 255)
                        throw new KitBundleRejected("corrupt LZ4 length");
                }
                while (b == 255);

                return length;
            }
        }

        // ------------------------------------------------------------------ serialized files

        sealed class TypeNode
        {
            public string Type = "";
            public string Name = "";
            public int Level;
            public int Flags;
            public int MetaFlag;
            public int End;          // index of the first node after this one's subtree
        }

        sealed class SerializedType
        {
            public int ClassId;
            public short ScriptTypeIndex = -1;
            public TypeNode[] Nodes;
        }

        sealed class ObjectInfo
        {
            public long PathId;
            public long Start;       // absolute, in the archive's uncompressed address space
            public long Size;
            public int ClassId;
            public SerializedType Type;
        }

        sealed class SerializedFile
        {
            public readonly Archive Archive;
            public readonly string Name;
            public readonly bool BigEndianData;
            public readonly List<SerializedType> Types = new List<SerializedType>();
            public readonly List<ObjectInfo> Objects = new List<ObjectInfo>();
            public readonly Dictionary<long, ObjectInfo> ByPathId = new Dictionary<long, ObjectInfo>();
            public readonly List<(int fileIndex, long pathId)> ScriptTypes = new List<(int, long)>();
            public readonly List<string> Externals = new List<string>();

            readonly Totals totals;

            public SerializedFile(Archive archive, Node node, Totals totals)
            {
                Archive = archive;
                Name = node.Path;
                this.totals = totals;

                var reader = new BinReader(new ArchiveSource(archive, node.Offset, node.Size), bigEndian: true);

                reader.U32();                       // metadata size (pre-22 field, superseded below)
                reader.U32();                       // file size (pre-22)
                uint version = reader.U32();
                reader.U32();                       // data offset (pre-22)
                if (version != 22)
                    throw new KitBundleRejected($"serialized file '{Name}' is version {version}, expected 22");

                byte endianness = reader.U8();
                reader.Bytes(3);
                reader.U32();                       // metadata size
                long fileSize = reader.I64();
                long dataOffset = reader.I64();
                reader.I64();                       // unknown, zero

                if (fileSize != node.Size || dataOffset < 0 || dataOffset > node.Size)
                    throw new KitBundleRejected($"serialized file '{Name}' has inconsistent sizes");

                BigEndianData = endianness != 0;
                reader.BigEndian = BigEndianData;

                reader.CString(64);                 // unity version
                reader.I32();                       // target platform
                bool typeTree = reader.U8() != 0;
                if (!typeTree)
                    throw new KitBundleRejected($"'{Name}' was built without type trees; the kit keeps them on");

                int typeCount = reader.I32();
                if (typeCount < 0 || typeCount > MaxTypes)
                    throw new KitBundleRejected("type count out of range: " + typeCount);
                totals.Types += typeCount;
                for (int i = 0; i < typeCount; i++)
                    Types.Add(ReadType(reader, isRefType: false));

                int objectCount = reader.I32();
                if (objectCount < 0 || objectCount > MaxObjects)
                    throw new KitBundleRejected("object count out of range: " + objectCount);
                totals.SpendObjects(objectCount);

                for (int i = 0; i < objectCount; i++)
                {
                    reader.Align(4);
                    long pathId = reader.I64();
                    long start = reader.I64();
                    uint size = reader.U32();
                    int typeIndex = reader.I32();

                    if (typeIndex < 0 || typeIndex >= Types.Count)
                        throw new KitBundleRejected("object refers to a type that does not exist");
                    if (start < 0 || dataOffset + start + size > node.Size)
                        throw new KitBundleRejected("object lies outside its file");

                    var obj = new ObjectInfo
                    {
                        PathId = pathId,
                        Start = node.Offset + dataOffset + start,
                        Size = size,
                        Type = Types[typeIndex],
                        ClassId = Types[typeIndex].ClassId,
                    };

                    if (ByPathId.ContainsKey(pathId))
                        throw new KitBundleRejected("two objects share path id " + pathId);

                    Objects.Add(obj);
                    ByPathId.Add(pathId, obj);
                }

                int scriptCount = reader.I32();
                if (scriptCount < 0 || scriptCount > MaxScriptTypes)
                    throw new KitBundleRejected("script type count out of range: " + scriptCount);
                for (int i = 0; i < scriptCount; i++)
                {
                    int fileIndex = reader.I32();
                    reader.Align(4);
                    ScriptTypes.Add((fileIndex, reader.I64()));
                }

                int externalCount = reader.I32();
                if (externalCount < 0 || externalCount > MaxExternals)
                    throw new KitBundleRejected("external count out of range: " + externalCount);
                for (int i = 0; i < externalCount; i++)
                {
                    reader.CString(1024);           // empty
                    reader.Bytes(16);               // guid
                    reader.I32();                   // type
                    Externals.Add(reader.CString(1024));
                }

                // Reference types (SerializeReference) and user information follow. Nothing in the policy
                // needs them, and a file whose objects are all listed above has told us what it holds.
            }

            SerializedType ReadType(BinReader reader, bool isRefType)
            {
                var type = new SerializedType { ClassId = reader.I32() };
                reader.U8();                        // stripped
                type.ScriptTypeIndex = reader.I16();

                if ((isRefType && type.ScriptTypeIndex >= 0) || type.ClassId == 114)
                    reader.Bytes(16);               // script id
                reader.Bytes(16);                   // old type hash

                type.Nodes = ReadTypeTree(reader);

                int dependencies = reader.I32();
                if (dependencies < 0 || dependencies > MaxTypes)
                    throw new KitBundleRejected("type dependency count out of range");
                reader.Bytes(dependencies * 4);

                return type;
            }

            TypeNode[] ReadTypeTree(BinReader reader)
            {
                int count = reader.I32();
                int stringBytes = reader.I32();
                if (count < 1 || count > MaxTypeTreeNodes || stringBytes < 0 || stringBytes > MaxStringBuffer)
                    throw new KitBundleRejected("type tree size out of range");

                // Before anything is allocated for it.
                totals.SpendTypeTree(count, stringBytes);

                var nodes = new TypeNode[count];
                var typeOffsets = new uint[count];
                var nameOffsets = new uint[count];
                for (int i = 0; i < count; i++)
                {
                    reader.U16();                   // node version
                    int level = reader.U8();
                    int flags = reader.U8();
                    typeOffsets[i] = reader.U32();
                    nameOffsets[i] = reader.U32();
                    reader.I32();                   // byte size
                    reader.I32();                   // index
                    int meta = reader.I32();
                    reader.U64();                   // ref type hash (version >= 19)

                    nodes[i] = new TypeNode { Level = level, Flags = flags, MetaFlag = meta };
                }

                byte[] strings = reader.Bytes(stringBytes);
                for (int i = 0; i < count; i++)
                {
                    nodes[i].Type = TreeString(strings, typeOffsets[i]);
                    nodes[i].Name = TreeString(strings, nameOffsets[i]);
                }

                // Pre-order with levels; a subtree ends at the first later node that is not deeper.
                if (nodes[0].Level != 0)
                    throw new KitBundleRejected("type tree does not start at its root");
                for (int i = 0; i < count; i++)
                {
                    if (i > 0 && nodes[i].Level > nodes[i - 1].Level + 1)
                        throw new KitBundleRejected("type tree skips a level");

                    int end = i + 1;
                    while (end < count && nodes[end].Level > nodes[i].Level)
                        end++;
                    nodes[i].End = end;
                }

                return nodes;
            }

            public SerializedFile Resolve(int fileIndex, Dictionary<string, SerializedFile> files)
            {
                if (fileIndex == 0)
                    return this;
                if (fileIndex < 0 || fileIndex > Externals.Count)
                    return null;

                // "archive:/CAB-0123.../CAB-0123..." or "archive:/BuildPlayer-X/BuildPlayer-X.sharedAssets":
                // the part after the last slash is the file's name inside this bundle. Anything else
                // (Library/unity default resources, another bundle) is not in here and stays unresolved.
                string path = Externals[fileIndex - 1];
                if (!path.StartsWith("archive:/", StringComparison.Ordinal))
                    return null;

                string name = path.Substring(path.LastIndexOf('/') + 1);
                return files.TryGetValue(name, out SerializedFile file) ? file : null;
            }

            public string ExternalName(int fileIndex) =>
                fileIndex > 0 && fileIndex <= Externals.Count ? Externals[fileIndex - 1] : "#" + fileIndex;
        }

        /// <summary>
        /// What the inspection has read so far, across every serialized file, held to budgets.
        ///
        /// A type tree node is a 32-byte record in the file and an object here. Nothing below the totals
        /// stops a bundle of a few megabytes - LZ4 compresses repeated records to almost nothing - from
        /// unpacking to two gigabytes of valid-looking node records, tens of millions of objects on the
        /// heap of a game that is only trying to decide whether to open a map. Objects are the same story
        /// at 24 bytes each. The budgets sit far above any real map - phase 0's spike read 36 types,
        /// 4,608 type tree nodes and 1,984 objects - and keep what this reader holds to tens of megabytes.
        /// </summary>
        sealed class Totals
        {
            public int Types;
            public long TypeTreeNodes;
            public long TypeTreeStringBytes;
            public long Objects;

            public void SpendTypeTree(int nodes, int stringBytes)
            {
                TypeTreeNodes += nodes;
                TypeTreeStringBytes += stringBytes;
                if (TypeTreeNodes > MaxTotalTypeTreeNodes || TypeTreeStringBytes > MaxTotalTypeTreeStringBytes)
                    throw new KitBundleRejected("type trees too large in total");
            }

            public void SpendObjects(int count)
            {
                Objects += count;
                if (Objects > MaxTotalObjects)
                    throw new KitBundleRejected("too many objects in total: " + Objects);
            }
        }

        /// <summary>Unity's shared type-tree strings: an offset with the top bit set indexes this table
        /// instead of the file's own buffer. Offsets are cumulative, so the order is the contract.</summary>
        static readonly string CommonStrings =
            "AABB\0AnimationClip\0AnimationCurve\0AnimationState\0Array\0Base\0BitField\0bitset\0bool\0char\0" +
            "ColorRGBA\0Component\0data\0deque\0double\0dynamic_array\0FastPropertyName\0first\0float\0Font\0" +
            "GameObject\0Generic Mono\0GradientNEW\0GUID\0GUIStyle\0int\0list\0long long\0map\0Matrix4x4f\0" +
            "MdFour\0MonoBehaviour\0MonoScript\0m_ByteSize\0m_Curve\0m_EditorClassIdentifier\0" +
            "m_EditorHideFlags\0m_Enabled\0m_ExtensionPtr\0m_GameObject\0m_Index\0m_IsArray\0m_IsStatic\0" +
            "m_MetaFlag\0m_Name\0m_ObjectHideFlags\0m_PrefabInternal\0m_PrefabParentObject\0m_Script\0" +
            "m_StaticEditorFlags\0m_Type\0m_Version\0Object\0pair\0PPtr<Component>\0PPtr<GameObject>\0" +
            "PPtr<Material>\0PPtr<MonoBehaviour>\0PPtr<MonoScript>\0PPtr<Object>\0PPtr<Prefab>\0PPtr<Sprite>\0" +
            "PPtr<TextAsset>\0PPtr<Texture>\0PPtr<Texture2D>\0PPtr<Transform>\0Prefab\0Quaternionf\0Rectf\0" +
            "RectInt\0RectOffset\0second\0set\0short\0size\0SInt16\0SInt32\0SInt64\0SInt8\0staticvector\0string\0" +
            "TextAsset\0TextMesh\0Texture\0Texture2D\0Transform\0TypelessData\0UInt16\0UInt32\0UInt64\0UInt8\0" +
            "unsigned int\0unsigned long long\0unsigned short\0vector\0Vector2f\0Vector3f\0Vector4f\0" +
            "m_ScriptingClassIdentifier\0Gradient\0Type*\0int2_storage\0int3_storage\0BoundsInt\0" +
            "m_CorrespondingSourceObject\0m_PrefabInstance\0m_PrefabAsset\0FileSize\0Hash128\0RenderingLayerMask\0";

        static readonly Dictionary<uint, string> commonByOffset = BuildCommon();

        static Dictionary<uint, string> BuildCommon()
        {
            var map = new Dictionary<uint, string>();
            uint offset = 0;
            foreach (string s in CommonStrings.Split('\0'))
            {
                if (s.Length == 0)
                    continue;
                map[offset] = s;
                offset += (uint)s.Length + 1;
            }
            return map;
        }

        static string TreeString(byte[] local, uint offset)
        {
            if ((offset & 0x80000000u) != 0)
            {
                // An unknown common string is survivable - it can only be a name or a type this reader
                // does not need to understand, and a primitive it DID need would fail loudly later.
                return commonByOffset.TryGetValue(offset & 0x7FFFFFFFu, out string common) ? common : "?common" + (offset & 0x7FFFFFFFu);
            }

            if (offset >= local.Length)
                throw new KitBundleRejected("type tree string outside its buffer");

            int end = Array.IndexOf(local, (byte)0, (int)offset);
            if (end < 0)
                end = local.Length;
            return Encoding.UTF8.GetString(local, (int)offset, end - (int)offset);
        }

        // ------------------------------------------------------------------ object data

        /// <summary>Named fields wanted from one object, filled as the walk finds them. Paths are dotted
        /// field names below the root ("m_VertexData.m_VertexCount"); for an array the value is its
        /// element count.</summary>
        sealed class FieldRequest
        {
            readonly Dictionary<string, object> values = new Dictionary<string, object>(StringComparer.Ordinal);
            readonly HashSet<string> wanted;
            public int Remaining;

            public FieldRequest(params string[] paths)
            {
                wanted = new HashSet<string>(paths, StringComparer.Ordinal);
                Remaining = wanted.Count;
            }

            public bool Wants(string path) => wanted.Contains(path) && !values.ContainsKey(path);

            public void Set(string path, object value)
            {
                if (!Wants(path))
                    return;
                values[path] = value;
                Remaining--;
            }

            public bool Has(string path) => values.ContainsKey(path);

            public long Long(string path) =>
                values.TryGetValue(path, out object v) ? Convert.ToInt64(v) : 0;

            public double Double(string path) =>
                values.TryGetValue(path, out object v) ? Convert.ToDouble(v) : 0d;

            public string String(string path) =>
                values.TryGetValue(path, out object v) ? v as string ?? "" : "";
        }

        /// <summary>Walks one object's bytes by its type tree. Stops as soon as every wanted field is in
        /// hand, skips arrays of plain values without touching them, and shares one visit budget across
        /// the whole inspection.</summary>
        sealed class Walker
        {
            long visits;
            public long Visits => visits;
            BinReader reader;
            FieldRequest request;
            TypeNode[] nodes;

            sealed class Done : Exception { }
            static readonly Done done = new Done();

            public void Read(SerializedFile file, ObjectInfo obj, FieldRequest wanted)
            {
                if (obj.Type?.Nodes == null)
                    return;

                reader = new BinReader(new ArchiveSource(file.Archive, obj.Start, obj.Size), file.BigEndianData);
                request = wanted;
                nodes = obj.Type.Nodes;

                try
                {
                    int child = 1;
                    while (child < nodes[0].End)
                        child = Field(child, nodes[child].Name);
                }
                catch (Done)
                {
                }
            }

            int Field(int index, string path)
            {
                if (++visits > NodeVisitBudget)
                    throw new KitBundleRejected("type trees too large to inspect");

                TypeNode node = nodes[index];

                if ((node.Flags & 1) != 0)
                    return ArrayField(index, path);

                int size = PrimitiveSize(node.Type);
                if (size > 0 && node.End == index + 1)
                {
                    if (request.Wants(path))
                    {
                        request.Set(path, ReadPrimitive(node.Type));
                        if (request.Remaining == 0)
                            throw done;
                    }
                    else
                    {
                        reader.Skip(size);
                    }
                }
                else if (node.Type == "string")
                {
                    int length = reader.I32();
                    if (length < 0 || length > MaxStringBytes * 256)
                        throw new KitBundleRejected("string length out of range");

                    if (request.Wants(path) && length <= MaxStringBytes)
                    {
                        request.Set(path, Encoding.UTF8.GetString(reader.Bytes(length)));
                        reader.Align(4);
                        if (request.Remaining == 0)
                            throw done;
                    }
                    else
                    {
                        reader.Skip(length);
                        reader.Align(4);
                    }
                }
                else if (node.Type == "TypelessData")
                {
                    int length = reader.I32();
                    if (length < 0 || length > MaxArrayCount)
                        throw new KitBundleRejected("data block length out of range");
                    reader.Skip(length);
                    request.Set(path, (long)length);
                }
                else
                {
                    int child = index + 1;
                    while (child < node.End)
                        child = Field(child, path + "." + nodes[child].Name);
                }

                if ((node.MetaFlag & 0x4000) != 0)
                    reader.Align(4);

                return node.End;
            }

            int ArrayField(int index, string path)
            {
                TypeNode node = nodes[index];

                // An array node is always [size, data]; "path" is the owning field (the vector), because
                // the array node itself is named "Array" and callers ask for the field.
                int sizeIndex = index + 1;
                int dataIndex = sizeIndex < node.End ? nodes[sizeIndex].End : node.End;
                if (dataIndex >= node.End)
                    throw new KitBundleRejected("malformed array in type tree");

                int count = reader.I32();
                if (count < 0 || count > MaxArrayCount)
                    throw new KitBundleRejected("array length out of range");

                string owner = path.EndsWith(".Array", StringComparison.Ordinal) ? path.Substring(0, path.Length - 6) : path;
                request.Set(owner, (long)count);

                TypeNode element = nodes[dataIndex];
                int primitive = PrimitiveSize(element.Type);
                if (primitive > 0 && element.End == dataIndex + 1)
                {
                    reader.Skip((long)count * primitive);
                }
                else
                {
                    // Elements are walked, not inspected: nothing wanted lives inside an array element.
                    var saved = request;
                    request = Nothing;
                    for (int i = 0; i < count; i++)
                        Field(dataIndex, "");
                    request = saved;
                }

                if ((node.MetaFlag & 0x4000) != 0)
                    reader.Align(4);

                // Not while walking an element: Nothing has nothing remaining by definition, and stopping
                // there would abandon the enclosing object half read.
                if (!ReferenceEquals(request, Nothing) && request.Remaining == 0)
                    throw done;

                return node.End;
            }

            static readonly FieldRequest Nothing = new FieldRequest();

            object ReadPrimitive(string type)
            {
                switch (type)
                {
                    case "bool":
                    case "UInt8":
                    case "char":
                        return (long)reader.U8();
                    case "SInt8":
                        return (long)(sbyte)reader.U8();
                    case "SInt16":
                    case "short":
                        return (long)reader.I16();
                    case "UInt16":
                    case "unsigned short":
                        return (long)reader.U16();
                    case "SInt32":
                    case "int":
                        return (long)reader.I32();
                    case "UInt32":
                    case "unsigned int":
                    case "Type*":
                        return (long)reader.U32();
                    case "float":
                        return (double)BitConverter.Int32BitsToSingle(reader.I32());
                    case "SInt64":
                    case "long long":
                        return reader.I64();
                    case "UInt64":
                    case "unsigned long long":
                    case "FileSize":
                        return (long)reader.U64();
                    case "double":
                        return BitConverter.Int64BitsToDouble(reader.I64());
                    default:
                        throw new KitBundleRejected("unexpected primitive " + type);
                }
            }

            static int PrimitiveSize(string type)
            {
                switch (type)
                {
                    case "bool":
                    case "char":
                    case "UInt8":
                    case "SInt8":
                        return 1;
                    case "SInt16":
                    case "short":
                    case "UInt16":
                    case "unsigned short":
                        return 2;
                    case "SInt32":
                    case "int":
                    case "UInt32":
                    case "unsigned int":
                    case "float":
                    case "Type*":
                        return 4;
                    case "SInt64":
                    case "long long":
                    case "UInt64":
                    case "unsigned long long":
                    case "FileSize":
                    case "double":
                        return 8;
                    default:
                        return 0;
                }
            }
        }

        // ------------------------------------------------------------------ byte sources

        interface ISource
        {
            long Length { get; }
            void Read(long position, byte[] buffer, int offset, int count);
        }

        sealed class StreamSource : ISource
        {
            readonly Stream stream;
            public StreamSource(Stream stream) => this.stream = stream;
            public long Length => stream.Length;

            public void Read(long position, byte[] buffer, int offset, int count)
            {
                stream.Position = position;
                ReadExactly(stream, buffer, offset, count);
            }
        }

        sealed class ArraySource : ISource
        {
            readonly byte[] data;
            readonly int length;

            public ArraySource(byte[] data, int length)
            {
                this.data = data;
                this.length = length;
            }

            public long Length => length;

            public void Read(long position, byte[] buffer, int offset, int count) =>
                Buffer.BlockCopy(data, (int)position, buffer, offset, count);
        }

        sealed class ArchiveSource : ISource
        {
            readonly Archive archive;
            readonly long start;
            readonly long length;

            public ArchiveSource(Archive archive, long start, long length)
            {
                this.archive = archive;
                this.start = start;
                this.length = length;
            }

            public long Length => length;

            public void Read(long position, byte[] buffer, int offset, int count) =>
                archive.Read(start + position, buffer, offset, count);
        }

        /// <summary>Endian-aware reader over a bounded source. Every read checks the bound first, so a
        /// size taken from the file can never walk out of the range it belongs to.</summary>
        sealed class BinReader
        {
            readonly ISource source;
            readonly byte[] scratch = new byte[8];
            public bool BigEndian;
            public long Position;

            public BinReader(ISource source, bool bigEndian)
            {
                this.source = source;
                BigEndian = bigEndian;
            }

            void Fill(int count)
            {
                Need(count);
                source.Read(Position, scratch, 0, count);
                Position += count;
            }

            void Need(long count)
            {
                if (count < 0 || Position + count > source.Length)
                    throw new EndOfStreamException("read past the end of a bounded range");
            }

            public byte U8()
            {
                Fill(1);
                return scratch[0];
            }

            public ushort U16()
            {
                Fill(2);
                return BigEndian ? (ushort)((scratch[0] << 8) | scratch[1]) : (ushort)(scratch[0] | (scratch[1] << 8));
            }

            public short I16() => (short)U16();

            public uint U32()
            {
                Fill(4);
                return BigEndian
                    ? ((uint)scratch[0] << 24) | ((uint)scratch[1] << 16) | ((uint)scratch[2] << 8) | scratch[3]
                    : scratch[0] | ((uint)scratch[1] << 8) | ((uint)scratch[2] << 16) | ((uint)scratch[3] << 24);
            }

            public int I32() => (int)U32();

            public ulong U64()
            {
                Fill(8);
                ulong value = 0;
                if (BigEndian)
                {
                    for (int i = 0; i < 8; i++)
                        value = (value << 8) | scratch[i];
                }
                else
                {
                    for (int i = 7; i >= 0; i--)
                        value = (value << 8) | scratch[i];
                }
                return value;
            }

            public long I64() => (long)U64();

            public byte[] Bytes(int count)
            {
                Need(count);
                var bytes = new byte[count];
                source.Read(Position, bytes, 0, count);
                Position += count;
                return bytes;
            }

            public void Skip(long count)
            {
                Need(count);
                Position += count;
            }

            public void Align(int alignment)
            {
                long aligned = (Position + alignment - 1) / alignment * alignment;
                Need(aligned - Position);
                Position = aligned;
            }

            public string CString(int maxLength)
            {
                var bytes = new List<byte>();
                while (true)
                {
                    byte b = U8();
                    if (b == 0)
                        break;
                    if (bytes.Count >= maxLength)
                        throw new KitBundleRejected("string longer than " + maxLength + " bytes");
                    bytes.Add(b);
                }
                return Encoding.UTF8.GetString(bytes.ToArray());
            }
        }
    }
}
