using System.Collections.Generic;
using System.Linq;
using Mimicraft.Gameplay;

namespace Mimicraft.EditorTools
{
    /// <summary>
    /// What each marker kind and each map mode means, and which kinds each mode reads: the words the
    /// marker drawers and the MapMarker inspector show, in Turkish and English (KitEditorLanguage).
    ///
    /// The "reads" lists are the game's code, written down: MapLoader for Standard (and Companion and
    /// Infestation, whose controllers answer Standard) and Deathmatch, PracticeRoundManager, and
    /// GarticRoundManager. When one of those starts reading another kind, this list has to follow, or
    /// the inspector tells an author a marker is unused when it is not.
    /// </summary>
    public static class MapMarkerGuide
    {
        public readonly struct Entry
        {
            public readonly int Bit;
            public readonly string Name;
            readonly string tr, en;

            public Entry(int bit, string name, string tr, string en)
            {
                Bit = bit;
                Name = name;
                this.tr = tr;
                this.en = en;
            }

            public string Description => KitEditorLanguage.T(tr, en);
        }

        public static readonly Entry[] Modes =
        {
            new Entry((int)MapMarkerModes.Standard, "Standard",
                "Saklambaç: Standard, Companion ve Infestation.", "Prop hunt: Standard, Companion and Infestation."),
            new Entry((int)MapMarkerModes.Practice, "Practice",
                "Serbest yapım, round yok.", "Free building, no rounds."),
            new Entry((int)MapMarkerModes.Deathmatch, "Deathmatch",
                "Herkes silahlı, taraf yok.", "Everyone armed, no sides."),
            new Entry((int)MapMarkerModes.Gartic, "Gartic",
                "Guess the Mimic. Kit haritalarında henüz yok.", "Guess the Mimic. Not on kit maps yet."),
        };

        public static readonly Entry[] Kinds =
        {
            new Entry((int)MapMarkerKind.HiderSpawn, "HiderSpawn",
                "Modelciler round başında burada.", "Modelers start a round here."),
            new Entry((int)MapMarkerKind.HunterSpawn, "HunterSpawn",
                "Avcılar av başlayınca burada çıkar.", "Hunters come out here when the hunt starts."),
            new Entry((int)MapMarkerKind.LobbySpawn, "LobbySpawn",
                "Lobi dolarken herkes burada bekler.", "Everyone waits here while the lobby fills."),
            new Entry((int)MapMarkerKind.HunterDoor, "HunterDoor",
                "Hazırlıkta kapalı, av başlayınca açılır.", "Shut during preparation, open when the hunt starts."),
            new Entry((int)MapMarkerKind.ModelerSpawn, "ModelerSpawn",
                "Modelleyen oyuncunun yeri (Practice, Gartic).", "Where the modeling player stands (Practice, Gartic)."),
            new Entry((int)MapMarkerKind.ArenaSpawn, "ArenaSpawn",
                "1v1 düello noktası (Standard).", "A 1v1 duel position (Standard)."),
            new Entry((int)MapMarkerKind.HunterRoomSpawn, "HunterRoomSpawn",
                "Avcı odasında bekleme yeri; yalnızca oyunun Avcı odası sahnesinde okunur.",
                "A waiting spot in the Hunter room; read only in the game's Hunter room scene."),
        };

        // HunterRoomSpawn with Standard because MapLoader does read it for Standard, from the Hunter room
        // scene. A map's own are not read; the kind's description says so.
        const MapMarkerKind StandardReads = MapMarkerKind.HunterSpawn | MapMarkerKind.HiderSpawn | MapMarkerKind.LobbySpawn
            | MapMarkerKind.HunterDoor | MapMarkerKind.ArenaSpawn | MapMarkerKind.HunterRoomSpawn;
        const MapMarkerKind DeathmatchReads = MapMarkerKind.HunterSpawn | MapMarkerKind.HiderSpawn | MapMarkerKind.LobbySpawn;
        const MapMarkerKind PracticeReads = MapMarkerKind.ModelerSpawn | MapMarkerKind.LobbySpawn | MapMarkerKind.HiderSpawn;
        const MapMarkerKind GarticReads = MapMarkerKind.ModelerSpawn | MapMarkerKind.LobbySpawn | MapMarkerKind.HiderSpawn;

        /// <summary>The kinds a map mode reads from a map.</summary>
        public static MapMarkerKind Reads(MapMarkerModes mode)
        {
            MapMarkerKind kinds = MapMarkerKind.None;
            if ((mode & MapMarkerModes.Standard) != 0)
                kinds |= StandardReads;
            if ((mode & MapMarkerModes.Deathmatch) != 0)
                kinds |= DeathmatchReads;
            if ((mode & MapMarkerModes.Practice) != 0)
                kinds |= PracticeReads;
            if ((mode & MapMarkerModes.Gartic) != 0)
                kinds |= GarticReads;
            return kinds;
        }

        /// <summary>A mask's set names, or the words for all and none.</summary>
        public static string Summary(int value, Entry[] entries)
        {
            if (value == 0)
                return KitEditorLanguage.T("hiçbiri", "none");
            int known = entries.Aggregate(0, (mask, entry) => mask | entry.Bit);
            if ((value & known) == known)
                return KitEditorLanguage.T("hepsi", "all");
            return string.Join(", ", NamesIn(value, entries));
        }

        public static IEnumerable<string> NamesIn(int value, Entry[] entries) =>
            entries.Where(e => (value & e.Bit) != 0).Select(e => e.Name);
    }
}
