using UnityEngine;

namespace Mimicraft.Gameplay
{
    /// <summary>What a point is for. FLAGS, so one point can be several things.
    ///
    /// Most maps have somewhere that is obviously both the lobby and where Hiders start, or a corner
    /// that serves Practice and Gartic alike. With a single value that had to be two markers on the same
    /// spot, kept in step by hand - and the day they drift is the day somebody spawns in a wall in one
    /// mode only.
    ///
    /// Same reasoning as MapMarkerModes above, one field along: belonging to two things is not a third
    /// kind of belonging.</summary>
    [System.Flags]
    public enum MapMarkerKind
    {
        None = 0,
        HunterSpawn = 1 << 0,
        HiderSpawn = 1 << 1,
        HunterDoor = 1 << 2,

        /// <summary>Where players stand while the lobby is still filling up, before roles are handed
        /// out. Separate from the two round spawns because it answers a different question: those place
        /// a side for the hunt, this one just needs somewhere sensible for everyone to mill around
        /// together and be visible to each other. Optional - a map without any falls back to the Hider
        /// spawns, which is what every map did before this existed.</summary>
        LobbySpawn = 1 << 3,

        /// <summary>Gartic: where the player whose turn it is stands while they model. One spot, in
        /// front of everyone - the model IS their body, so where they stand is where the thing being
        /// guessed appears.</summary>
        ModelerSpawn = 1 << 4,

        /// <summary>Where a Hunter waits out Hazırlık, in the Hunter room - a separate scene sitting
        /// below the map, not a corner of the map itself.
        ///
        /// Separate from HunterSpawn because the two answer different questions and are in different
        /// places: this one is where you are kept while the Modelciler hide, that one is where you are
        /// let out when the hunt begins. Using one marker for both is what a locked door tries to
        /// paper over - and a door only works if there is nothing to see through, which is a promise
        /// geometry cannot keep as reliably as distance can.
        ///
        /// Optional. A mode with no Hunter room scene never looks for these, and Hunters simply start
        /// at their map spawns as they always did.</summary>
        HunterRoomSpawn = 1 << 5,

        /// <summary>A fighting position in the 1v1 arena - the thing Hunters do with Hazırlık instead
        /// of standing in the room (see DuelArena).
        ///
        /// TWO of them, and two is enough however many pairs are duelling: a pair is hidden from
        /// everybody but each other, so several duels share the one arena without meeting. Put them
        /// facing each other, far enough apart for a fight and inside the arena's geometry.
        ///
        /// They can live in the Hunter room scene or in the map; both are searched. Optional, and a map
        /// without them simply has no arena - the button says so.</summary>
        ArenaSpawn = 1 << 6,

        /// <summary>Gartic: where the computer stands that the modeller sits at while their body is on the
        /// stand. The GAME puts its own console there (GarticRoundManager) - the map carries only the point, so a
        /// kit map has it as well without a script of its own. The point is the console's root: the floor under
        /// it, facing the way it faces.
        ///
        /// One per map. With several the game uses the first by position; without one the modeller stays inside
        /// their body, as before there was a computer.</summary>
        GarticComputer = 1 << 7,
    }

    /// <summary>Which modes a marker belongs to.
    ///
    /// Flags rather than a single value with a "Shared" entry, because sharing is not a third kind of
    /// ownership - it is simply belonging to more than one mode. With a sentinel, a point used by two
    /// of three modes had no way to say so and had to be marked shared, which then also silently
    /// offered it to the third.
    ///
    /// The kinds themselves stay in one enum rather than one per mode: a marker is still just "a
    /// labelled point", and splitting the type would mean every collector handling two shapes of the
    /// same idea.</summary>
    [System.Flags]
    public enum MapMarkerModes
    {
        None = 0,
        Standard = 1 << 0,
        Gartic = 1 << 1,

        /// <summary>Practice: no round, no sides, everyone building. Its spawns are usually the same
        /// points Standard uses for its lobby, which is why the field's default of "everything" already
        /// covers most maps - a marker only needs this bit cleared to be kept out of Practice.</summary>
        Practice = 1 << 2,

        /// <summary>Deathmatch: everybody armed, no sides. Uses EVERY spawn kind a map has, so a map's
        /// markers need no new points for it - only this bit, which the default of "everything" already
        /// carries.</summary>
        Deathmatch = 1 << 3,
    }

    /// <summary>
    /// Tags a point of interest inside a map prefab so MapLoader can find it after instantiating.
    ///
    /// A component rather than a name convention on purpose. Searching for "HunterSpawnPoint0" works
    /// right up until somebody renames an object, or Blender's re-import appends a suffix, or a
    /// duplicate arrives as "… (1)" - and every one of those failures is silent until someone spawns
    /// under the floor in a live round. A missing component is visible in the Inspector while the map
    /// is being built, and MapLoader can say exactly which kind was missing.
    /// </summary>
    public class MapMarker : MonoBehaviour
    {
        [Tooltip("Hangi oyun modlarına ait. Birden fazlası seçilebilir; Everything = her mod " +
                 "kullanabilir. Önce bunu seç, sonra aşağıdaki türü - hangi türlerin anlamlı olduğu " +
                 "moda göre değişir. Hiçbiri seçili değilse bu marker hiçbir modda kullanılmaz.")]
        [SerializeField] MapMarkerModes modes = (MapMarkerModes)~0;

        [Tooltip("Bu nokta ne işe yarıyor. Birden fazlası seçilebilir - aynı yer hem lobi hem Hider " +
                 "spawn'ı olabilir, ki çoğu haritada öyledir. " +
                 "Hunter/Hider/Lobby spawn'ları istediğin kadar olabilir; RoundManager aralarında " +
                 "sırayla dağıtıyor. HunterDoor'un en fazla bir tane olması bekleniyor.")]
        [SerializeField] MapMarkerKind kinds = MapMarkerKind.HiderSpawn;

        /// <summary>The pre-Flags value, kept only so old markers can be read once and forgotten.
        ///
        /// It has to be a separate FIELD, not the same one reinterpreted: the old enum was ordinal, so a
        /// marker saved as HunterDoor (2) would come back as HunterSpawn|HiderSpawn under the new
        /// numbering - a silent change of meaning on every map in the project, discovered by somebody
        /// spawning in a wall. Reading the old name into its own field is what makes the conversion
        /// something that can happen exactly once and be seen to have happened.
        ///
        /// -1 means "already migrated", which is what every marker written after this change stores.</summary>
        [SerializeField, HideInInspector] int kind = -1;

        public MapMarkerModes Modes => modes;

        /// <summary>Everything this point is. Test with <see cref="Is"/> rather than comparing - a marker
        /// that is both a lobby and a Hider spawn equals neither.</summary>
        public MapMarkerKind Kind
        {
            get
            {
                Migrate();
                return kinds;
            }
        }

        /// <summary>Whether this point serves a given purpose. The one way to ask, so nothing is left
        /// comparing a combination against a single value and quietly matching nothing.</summary>
        public bool Is(MapMarkerKind purpose) => (Kind & purpose) != 0;

        void Awake() => Migrate();

        void OnValidate() => Migrate();

        /// <summary>Converts a pre-Flags ordinal exactly once. Ordinals and flags cannot be told apart by
        /// value, which is why the old one lives in its own field and is cleared as it is read.</summary>
        void Migrate()
        {
            if (kind < 0)
                return;

            kinds = kind switch
            {
                0 => MapMarkerKind.HunterSpawn,
                1 => MapMarkerKind.HiderSpawn,
                2 => MapMarkerKind.HunterDoor,
                3 => MapMarkerKind.LobbySpawn,
                4 => MapMarkerKind.ModelerSpawn,
                _ => kinds,
            };

            kind = -1;
        }

        /// <summary>Whether this marker applies to a given mode.
        ///
        /// Defaults to every bit set, so a marker placed before this field existed - or one whose modes
        /// nobody thought about - keeps working everywhere rather than quietly applying nowhere. Clearing
        /// the mask is therefore a deliberate act, which is the right way round for something whose
        /// failure is a player spawning inside the floor.</summary>
        public bool AppliesTo(MapMarkerModes target) => (modes & target) != 0;

        /// <summary>How solid a figure's boxes are drawn, under their outlines: enough to read a shape from across
        /// the map, little enough to see the floor through.</summary>
        const float FigureFill = 0.18f;

        // Spawn points are usually empty GameObjects with nothing to see in the Scene view, which
        // makes them easy to lose track of while laying a map out. Each draws what the game will put there
        // (MapMarkerFigures): a spawn the default character, standing where and facing the way the player
        // will, so it doubles as a scale reference - the kit has no walking test of its own, Pack and Test
        // opens the real game (plan §14.4).
        void OnDrawGizmos()
        {
            // First match wins, because a point can now be several things and a gizmo can only be one
            // colour. Ordered by how much it matters to spot the marker while laying a map out: a door is
            // unmistakable in shape, the two round spawns decide where a side starts, and the rest are
            // conveniences.
            Color color =
                Is(MapMarkerKind.HunterDoor) ? new Color(0.95f, 0.85f, 0.3f, 1f) :
                Is(MapMarkerKind.GarticComputer) ? new Color(0.35f, 0.55f, 1f, 1f) :
                Is(MapMarkerKind.HunterRoomSpawn) ? new Color(0.95f, 0.55f, 0.15f, 1f) :
                Is(MapMarkerKind.HunterSpawn) ? new Color(0.95f, 0.4f, 0.3f, 1f) :
                Is(MapMarkerKind.HiderSpawn) ? new Color(0.35f, 0.75f, 0.95f, 1f) :
                Is(MapMarkerKind.ModelerSpawn) ? new Color(0.95f, 0.6f, 0.95f, 1f) :
                Is(MapMarkerKind.LobbySpawn) ? new Color(0.5f, 0.95f, 0.5f, 1f) :
                new Color(0.6f, 0.6f, 0.6f, 1f);
            Gizmos.color = color;

            if (Is(MapMarkerKind.HunterDoor))
            {
                DrawDoor(color);
                return;
            }

            // The game's console where the game stands it - at the point, turned with it, at its own scale
            // (GarticRoundManager) - and the modeller sitting at it.
            if (Is(MapMarkerKind.GarticComputer))
            {
                Matrix4x4 console = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
                if (!MapMarkerFigures.Draw(MapMarkerFigures.Console, console, color, FigureFill))
                {
                    Gizmos.matrix = console;
                    Gizmos.DrawWireCube(Vector3.up * 0.5f, Vector3.one);
                    Gizmos.matrix = Matrix4x4.identity;
                }

                MapMarkerFigures.Draw(MapMarkerFigures.Seated, console, Color.Lerp(color, Color.white, 0.4f), FigureFill);
                Gizmos.DrawRay(transform.position + Vector3.up * 0.5f, transform.forward * 1.2f);
                return;
            }

            // A player is stood upright whatever the point's tilt (PlayerMovement.Upright), and at their own size
            // whatever its scale.
            Quaternion facing = Quaternion.Euler(0f, transform.eulerAngles.y, 0f);
            Matrix4x4 player = Matrix4x4.TRS(transform.position, facing, Vector3.one);
            if (!MapMarkerFigures.Draw(MapMarkerFigures.Standing, player, color, FigureFill))
                DrawCapsule();

            // Which way the player will be facing when they land here.
            Gizmos.DrawRay(transform.position + Vector3.up * 0.9f, facing * Vector3.forward * 1.2f);
        }

        /// <summary>Before anything is baked: the player's own capsule (the Player prefab's CharacterController,
        /// 1.8 m tall, 0.4 m radius, 0.1 m off the ground).</summary>
        void DrawCapsule()
        {
            Vector3 feet = transform.position + Vector3.up * 0.1f;
            Vector3 lower = feet + Vector3.up * 0.4f;
            Vector3 upper = feet + Vector3.up * 1.4f;
            Gizmos.DrawWireSphere(lower, 0.4f);
            Gizmos.DrawWireSphere(upper, 0.4f);
            foreach (Vector3 side in new[] { Vector3.right, Vector3.left, Vector3.forward, Vector3.back })
                Gizmos.DrawLine(lower + side * 0.4f, upper + side * 0.4f);
        }

        /// <summary>The door itself: the marker IS the object the game switches off when the hunt starts, so its own
        /// colliders say what it blocks - its renderers when it has none, a metre box when it has neither.</summary>
        void DrawDoor(Color color)
        {
            bool found = false;
            Bounds bounds = default;

            foreach (Collider blocker in GetComponentsInChildren<Collider>())
            {
                if (!blocker.enabled || blocker.isTrigger)
                    continue;

                if (found)
                    bounds.Encapsulate(blocker.bounds);
                else
                    bounds = blocker.bounds;
                found = true;
            }

            if (!found)
            {
                foreach (Renderer shown in GetComponentsInChildren<Renderer>())
                {
                    if (found)
                        bounds.Encapsulate(shown.bounds);
                    else
                        bounds = shown.bounds;
                    found = true;
                }
            }

            if (!found)
            {
                Gizmos.DrawWireCube(transform.position, Vector3.one);
                return;
            }

            Gizmos.color = new Color(color.r, color.g, color.b, color.a * FigureFill);
            Gizmos.DrawCube(bounds.center, bounds.size);
            Gizmos.color = color;
            Gizmos.DrawWireCube(bounds.center, bounds.size);

            // Crossed through along its width: a way that is shut until the hunt.
            Vector3 e = bounds.extents;
            Vector3 across = e.x >= e.z ? new Vector3(e.x, 0f, 0f) : new Vector3(0f, 0f, e.z);
            Vector3 up = new Vector3(0f, e.y, 0f);
            Gizmos.DrawLine(bounds.center - across - up, bounds.center + across + up);
            Gizmos.DrawLine(bounds.center - across + up, bounds.center + across - up);
        }
    }
}
