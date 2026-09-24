using System.Collections.Generic;
using UnityEngine;

namespace Mimicraft.Gameplay
{
    /// <summary>
    /// A hand-placed box that says "bake the playable area in here", plus the grid that bake produced.
    ///
    /// The shape of the authoring is deliberately APV's: you drag a volume around the part of the world
    /// you care about, and you press Bake when you are ready. Nothing is derived from the map's bounding
    /// box and nothing happens on its own, because both of those turned out to be the same mistake - the
    /// system guessing at a region, and being wrong somewhere nobody looked until a player was thrown out
    /// of a round.
    ///
    /// WHAT IS BAKED IS REACHABILITY, not the box. Cells inside the volume are flood-filled outward from
    /// the map's own spawn markers and stopped by its own collision, so a concave footprint, an interior
    /// courtyard, three stacked floors and a room inside a room all fall out of the fill for free. The
    /// volume's only job is to bound the search - which is also what makes a leak survivable: geometry
    /// with a crack in it fills the space beyond the crack up to the volume's wall and no further,
    /// instead of escaping into the entire world.
    ///
    /// MANY VOLUMES, NOT ONE. A point is inside the play area if ANY volume says so. That is what lets
    /// the Hunter room - a separate scene, sitting two hundred metres below the map, that the map's own
    /// bake has never heard of - simply carry a volume of its own and stop being a special case. The old
    /// single-instance design could not express "these two disconnected places are both legal", so the
    /// room had to be excused by name through GameModeController.AllowsOutsidePlayArea.
    ///
    /// Data is stored in this component's LOCAL space, so moving or rotating the object it sits on takes
    /// the bounds with it. That is not a nicety here: MapLoader shifts the whole Hunter room scene by
    /// GameModeDefinition.HunterRoomOffset at runtime, and a volume in world coordinates would be left
    /// behind at the authored depth.
    ///
    /// UNBAKED MEANS UNLIMITED, deliberately - see MapBounds.IsInsidePlayArea. The failure mode of
    /// missing data has to be "the feature is off", never "the game is unplayable".
    /// </summary>
    [DisallowMultipleComponent]
    public class MapBoundsVolume : MonoBehaviour
    {
        // ---------------------------------------------------------------- Authored

        [Header("Hacim (yerel uzay)")]
        [Tooltip("Kutunun merkezi - bu objenin yerel uzayında.")]
        [SerializeField] Vector3 center;

        [Tooltip("Kutunun boyutu, metre. Sahnede sarı kutunun köşelerinden de sürükleyebilirsin.")]
        [SerializeField] Vector3 size = new Vector3(64f, 32f, 64f);

        /// <summary>How much room the Sığdır buttons leave around the geometry they measure.
        ///
        /// Generous, and generous upward is the point: a Modelci standing on a roof or a Hunter at the
        /// top of a jump is still on the map, and a box that stops at the roofline would have the bake
        /// declare the air above it out of bounds. Six metres is roughly two storeys of headroom, which
        /// is also what the old automatic volume used before any of this was placed by hand.</summary>
        [Tooltip("Sığdır düğmelerinin geometrinin çevresine bıraktığı pay, metre. Yukarısı önemli: " +
                 "çatıdaki bir Modelci ya da zıplamasının tepesindeki bir Avcı hâlâ haritadadır.")]
        [SerializeField] float fitMargin = 6f;

        [Header("Bake ayarları")]
        [Tooltip("Hücre boyutu, metre. Küçültmek ince geçitleri ve çatlakları daha iyi yakalar ama " +
                 "bake süresi de veri boyutu da bunun KÜPÜ ile büyür.")]
        [SerializeField] float cellSize = 0.5f;

        [Tooltip("Hangi katmanlar dolu sayılsın. Oyuncular, prop'lar, FPS eli ve Studio sahnesi " +
                 "varsayılan olarak dışarıda - onlar harita değil.")]
        [SerializeField] LayerMask blockingLayers = MapLayers;

        /// <summary>Everything except the layers that are, by definition, not map geometry.
        ///
        /// It used to be "everything", which is wrong in both directions at once. For the BAKE, a player
        /// or a Modelci's voxel body standing in the scene when somebody presses the button carves a
        /// permanent hole in the map's bounds. For the FIT, the Studio stage - the character and weapon
        /// preview set, which lives far from the origin so it cannot be seen from the map - drags the box
        /// out to meet it.
        ///
        /// Ignore Raycast (2), UI (5), Studio (7), PlayerController (8), VoxelBody (9), FPSVisual (10).
        /// What is left is the geometry a map is actually built from, MapBlocker included, because an
        /// invisible wall is exactly as much of a boundary as a visible one.</summary>
        public const int MapLayers = ~((1 << 2) | (1 << 5) | (1 << 7) | (1 << 8) | (1 << 9) | (1 << 10));

        [Tooltip("Dolgunun başlayacağı noktalar. Boş bırakılırsa hacmin içindeki MapMarker'lar " +
                 "kullanılır - normal olan budur. Doldurursan SADECE bunlar kullanılır.")]
        [SerializeField] Transform[] seeds;

        [Tooltip("Hangi tür MapMarker dolgunun başlangıcı sayılsın. Dolgu binadan taşıp bütün kutuyu " +
                 "dolduruyorsa sebep genelde budur: binanın dışında duran bir marker dışarıyı " +
                 "tohumluyordur.")]
        [SerializeField] MapMarkerKind seedKinds = (MapMarkerKind)~0;

        /// <summary>How wide a gap has to be to count as somewhere a player could have walked.
        ///
        /// The player's own width: the CharacterController on Player.prefab has radius 0.4, so 0.8 metres
        /// is the narrowest gap a person can pass through. The bake runs the fill twice - once normally,
        /// once allowed only through gaps at least this wide - and the difference between the two is
        /// space the fill squeezed into and nobody can reach. The seam between them is the leak.</summary>
        [Tooltip("Bir oyuncunun geçebileceği en dar aralık. Bake dolguyu iki kez çalıştırır - biri " +
                 "normal, biri sadece bu genişlikteki yerlerden - ve aradaki fark sızıntıdır. " +
                 "Varsayılan 0.8 = oyuncu genişliği (CharacterController yarıçapı 0.4).")]
        [SerializeField] float narrowThreshold = 0.8f;

        // ---------------------------------------------------------------- Baked

        // Hidden because Unity draws one array element per entry and there are eight cells to a byte: a
        // house-sized map is a six-figure list that takes seconds to draw and says nothing. The readout
        // the custom Inspector prints instead is the part worth reading.
        [SerializeField, HideInInspector] Vector3 bakedOrigin;
        [SerializeField, HideInInspector] float bakedCellSize;
        [SerializeField, HideInInspector] Vector3Int bakedSize;
        [SerializeField, HideInInspector] byte[] cells;

        // Leak mouths: cells the fill entered FROM player-sized space and which a player cannot fit
        // in. A separate bitset over the same grid rather than a second state in the first, because they
        // are not a third kind of cell - they are play area and they stay play area. A passage the fill
        // went through is somewhere a player might be standing, and refusing to bake it would turn a
        // diagnostic into a trapdoor.
        [SerializeField, HideInInspector] byte[] narrow;

        // The box the bake actually ran against, so "you have moved this since baking" can be answered
        // by comparison rather than by inferring it from the grid - which cannot be done, because the
        // bake crops its empty rind away and the grid is deliberately smaller than the box.
        [SerializeField, HideInInspector] Bounds bakedBox;

        // How the fill travelled from a seed to the volume's own wall, in local metres, and the
        // tightest point along the way. Kept because "there is a leak" and "the leak is HERE" are
        // different amounts of help, and only the second one ends the search.
        [SerializeField, HideInInspector] Vector3[] escapePath;
        [SerializeField, HideInInspector] Vector3 escapePoint;
        [SerializeField, HideInInspector] float escapeWidth;

        [SerializeField, HideInInspector] string bakedAt;
        [SerializeField, HideInInspector] int playCellCount;
        [SerializeField, HideInInspector] int narrowCellCount;

        // ---------------------------------------------------------------- Registry

        static readonly List<MapBoundsVolume> active = new List<MapBoundsVolume>();

        /// <summary>Every volume currently in play, in registration order.</summary>
        public static IReadOnlyList<MapBoundsVolume> Active => active;

        /// <summary>Domain reload is off in this project, so a static list survives leaving Play mode and
        /// the next session starts holding destroyed volumes from the last one.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => active.Clear();

        /// <summary>Raised by every volume as it enables; the game's MapBoundsWarden listens and makes
        /// sure it exists. This component lives in Mimicraft.MapKit, which the map kit shares with the
        /// game and which therefore cannot name game code - so it announces itself rather than calling
        /// the warden.
        ///
        /// Deliberately not cleared in ResetStatics: the listener subscribes from its own
        /// SubsystemRegistration hook, and the order two such hooks run in is not something to lean on.
        /// </summary>
        public static event System.Action Registered;

        // OnEnable rather than Awake, so switching a volume off switches its region off with it. That is
        // the only handle anything has on a volume at runtime, and it should mean what it looks like.
        void OnEnable()
        {
            active.Add(this);

            // From here rather than from a scene object, because a volume can arrive from a map prefab, a
            // map scene or the Hunter room scene and none of those is reliably first. Whoever gets here
            // first makes the warden; everybody else finds it already there.
            Registered?.Invoke();
        }

        void OnDisable() => active.Remove(this);

        public bool IsBaked =>
            cells != null && cells.Length > 0 && bakedSize.x > 0 && bakedSize.y > 0 && bakedSize.z > 0;

        public string BakedAt => bakedAt;

        /// <summary>Whether the fill reached the volume's wall, and the trail it took to get there.
        ///
        /// An escape is not automatically a fault - an outdoor map's sky is play area and Modelciler
        /// climb into it - but when the box came back full and you did not expect it to, this is the
        /// route it took and the narrow point it squeezed through.</summary>
        public bool HasEscapePath => escapePath != null && escapePath.Length > 1;

        /// <summary>The tightest point on the escape route, in WORLD space. Where the hole is.</summary>
        public Vector3 EscapePoint => transform.TransformPoint(escapePoint);

        /// <summary>How wide the fill was at that point, in metres. Under a player's 0.8 it is a crack;
        /// around a doorway's width it is a door, and the fill is leaving the way anyone would.</summary>
        public float EscapeWidth => escapeWidth;
        public int PlayCellCount => playCellCount;
        public int NarrowCellCount => narrowCellCount;
        public Vector3Int BakedSize => bakedSize;
        public float BakedCellSize => bakedCellSize;
        public int DataBytes => cells != null ? cells.Length + (narrow != null ? narrow.Length : 0) : 0;

        /// <summary>The authored box, in local space. What the next bake will cover.</summary>
        public Bounds LocalBox
        {
            get => new Bounds(center, size);
            set
            {
                center = value.center;
                size = value.size;
            }
        }

        /// <summary>Puts the layer mask back to MapLayers. For the Inspector's own repair button - a
        /// volume placed before that default existed still carries "everything", and the two things it
        /// then gets wrong are both invisible.</summary>
        public void UseMapLayers() => blockingLayers = MapLayers;

        public float FitMargin => fitMargin;
        public float CellSize => cellSize;
        public LayerMask BlockingLayers => blockingLayers;
        public Transform[] Seeds => seeds;
        public MapMarkerKind SeedKinds => seedKinds;

        /// <summary>Where the fill starts. ONE definition, used by the bake and by the gizmo that draws
        /// them - a preview of the seeds that was computed separately would eventually disagree with the
        /// bake, and a diagnostic that lies is worse than none.
        ///
        /// The explicit Tohumlar list wins outright when it has anything in it. Otherwise it is the map's
        /// own markers: the spawn points are already the answer to "where does a player legitimately
        /// stand", and reachability from anywhere else would be reachability from somewhere nobody
        /// starts. Filtered by kind, because a marker OUTSIDE the building - a lobby spawn on the
        /// terrace, a Hunter room spawn off to one side - seeds the open air and fills the entire volume
        /// with it, which looks exactly like the bake being broken.
        ///
        /// Inside the box only, and that is not a convenience: a seed outside the grid cannot start
        /// anything, and one belonging to a different region is that region's business.</summary>
        public void CollectSeeds(List<Vector3> into)
        {
            into.Clear();

            if (seeds != null && seeds.Length > 0)
            {
                foreach (Transform seed in seeds)
                {
                    if (seed != null)
                        into.Add(seed.position);
                }

                if (into.Count > 0)
                    return;
            }

            Bounds box = LocalBox;

            // Inactive ones included: a marker parented under something switched off in the editor is
            // still where a player will stand, and MapLoader collects them the same way.
            foreach (MapMarker marker in FindObjectsByType<MapMarker>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if ((marker.Kind & seedKinds) == 0)
                    continue;

                if (box.Contains(transform.InverseTransformPoint(marker.transform.position)))
                    into.Add(marker.transform.position);
            }
        }
        public float NarrowThreshold => narrowThreshold;

        /// <summary>Whether the box has been moved or resized since the bake ran.
        ///
        /// Worth surfacing on its own rather than folding into "is it baked", because it is the failure
        /// that looks like success: the data is real, the gizmo draws, and it describes a region that is
        /// no longer the one on screen.</summary>
        public bool IsStale =>
            IsBaked && (bakedBox.center != center || bakedBox.size != size
                || !Mathf.Approximately(bakedCellSize, cellSize));

        // ---------------------------------------------------------------- Query

        /// <summary>Whether this point is inside this volume's play area.
        ///
        /// A point counts as inside if its own cell OR any cell touching it is play area, and that
        /// tolerance is not a fudge - it is the difference between this working and rejecting everybody.
        /// Cells straddling a surface bake SOLID, and a player's position is their feet, which are by
        /// definition on a surface: without the neighbours, standing on the floor reads as being off the
        /// map.
        ///
        /// Being generous by one cell is also the right way to be wrong. The question is "has this player
        /// left the world", not "is this player clear of the geometry" - half a metre of slack costs
        /// nothing, while a false positive teleports somebody out of a round they were playing
        /// correctly.</summary>
        public bool Contains(Vector3 worldPoint)
        {
            if (!IsBaked)
                return false;

            Vector3 local = transform.InverseTransformPoint(worldPoint) - bakedOrigin;

            // The cheap rejection first. Most volumes are nowhere near most points - with several of them
            // registered, the 27-cell loop below should only ever run for the one the player is actually
            // in.
            float slack = bakedCellSize;
            if (local.x < -slack || local.y < -slack || local.z < -slack
                || local.x > bakedSize.x * bakedCellSize + slack
                || local.y > bakedSize.y * bakedCellSize + slack
                || local.z > bakedSize.z * bakedCellSize + slack)
                return false;

            int x = Mathf.FloorToInt(local.x / bakedCellSize);
            int y = Mathf.FloorToInt(local.y / bakedCellSize);
            int z = Mathf.FloorToInt(local.z / bakedCellSize);

            for (int dz = -1; dz <= 1; dz++)
            {
                for (int dy = -1; dy <= 1; dy++)
                {
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        if (IsPlayArea(x + dx, y + dy, z + dz))
                            return true;
                    }
                }
            }

            return false;
        }

        bool IsPlayArea(int x, int y, int z)
        {
            if (x < 0 || y < 0 || z < 0 || x >= bakedSize.x || y >= bakedSize.y || z >= bakedSize.z)
                return false;

            return GetBit(cells, CellIndex(x, y, z));
        }

        int CellIndex(int x, int y, int z) => (z * bakedSize.y + y) * bakedSize.x + x;

        static bool GetBit(byte[] set, int index) =>
            set != null && (set[index >> 3] & (1 << (index & 7))) != 0;

        // ---------------------------------------------------------------- Bake entry point

        /// <summary>Writes a freshly baked grid in. MapBoundsBaker is the only caller; it is public
        /// because the baker lives in the editor assembly and this component does not.</summary>
        /// <summary>Records how the fill got out. Separate from ApplyBake because it is diagnosis
        /// rather than data - nothing at runtime reads it, and a bake that found no escape still has to
        /// clear whatever the last one left behind.</summary>
        public void ApplyEscape(Vector3[] path, Vector3 tightest, float width)
        {
            escapePath = path;
            escapePoint = tightest;
            escapeWidth = width;
        }

        public void ApplyBake(Vector3 origin, float bakedCells, Vector3Int gridSize,
            byte[] playArea, byte[] narrowCells, int playCells, int narrowCount)
        {
            bakedOrigin = origin;
            bakedBox = LocalBox;
            bakedCellSize = bakedCells;
            bakedSize = gridSize;
            cells = playArea;
            narrow = narrowCells;
            playCellCount = playCells;
            narrowCellCount = narrowCount;
            bakedAt = System.DateTime.Now.ToString("yyyy-MM-dd HH:mm");
            InvalidateGizmoMeshes();
        }

        /// <summary>Throws the bake away. Not the same as deleting the component: an unbaked volume is
        /// still a region somebody meant to define, and leaving it there is how they find it again.
        /// </summary>
        public void ClearBake()
        {
            bakedOrigin = default;
            bakedBox = default;
            bakedCellSize = 0f;
            bakedSize = default;
            cells = null;
            narrow = null;
            playCellCount = 0;
            narrowCellCount = 0;
            bakedAt = null;
            escapePath = null;
            escapePoint = default;
            escapeWidth = 0f;
            InvalidateGizmoMeshes();
        }

        // ---------------------------------------------------------------- Visualisation

#if UNITY_EDITOR

        [Header("Görselleştirme")]
        [Tooltip("Sızıntı ağızlarını kırmızı çizer: dolgunun, bir oyuncunun durabileceği yerden " +
                 "çıkıp sığamayacağı bir yere girdiği hücreler. Haritadan çıkan her yol birinden " +
                 "geçer ve genelde bütün hacimde bir avuç tanedir.")]
        [SerializeField] bool showLeaks = true;

        [Tooltip("Tüm oyun alanını çizer. PAHALI - büyük bir haritada yüz binlerce hücre. Bir yerin " +
                 "neden 'harita dışı' sayıldığını ararken aç, sonra kapat.")]
        [SerializeField] bool showPlayArea;

        [Tooltip("Dolgunun tohumdan hacmin duvarına kadar izlediği yolu macenta çizer, en dar " +
                 "noktasını da işaretler. Sızıntı oradadır.")]
        [SerializeField] bool showEscapePath = true;

        [Tooltip("Açıkken oyun alanı ve sızıntı çizimleri tek bir yatay dilimle sınırlanır. İç mekânı " +
                 "görmenin yolu bu - kabuk mesh'i içeriyi zaten gizler.")]
        [SerializeField] bool sliceOnly;

        [Tooltip("Dilimin yüksekliği - bu objenin KENDİ yerel uzayında, metre.")]
        [SerializeField] float sliceHeight;

        Mesh playAreaMesh;
        Mesh leakMesh;
        int gizmoStateHash;
        bool gizmoBuilt;

        void InvalidateGizmoMeshes() => gizmoBuilt = false;

        // Meshes made with `new Mesh()` are not garbage collected - in the editor they accumulate until
        // the next domain reload, and this rebuilds every time a toggle is flipped.
        void OnDestroy() => ReleaseGizmoMeshes();

        void ReleaseGizmoMeshes()
        {
            if (playAreaMesh != null)
                DestroyImmediate(playAreaMesh);
            if (leakMesh != null)
                DestroyImmediate(leakMesh);

            playAreaMesh = null;
            leakMesh = null;
            gizmoBuilt = false;
        }

        /// <summary>The authored box, always, so a volume can be found without selecting it. Thin and
        /// unfilled - several of these overlapping should still be readable.</summary>
        void OnDrawGizmos()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = IsBaked
                ? new Color(0.3f, 0.9f, 0.5f, 0.35f)
                : new Color(0.9f, 0.7f, 0.2f, 0.35f);
            Gizmos.DrawWireCube(center, size);
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = IsBaked ? new Color(0.3f, 0.9f, 0.5f, 0.9f) : new Color(0.9f, 0.7f, 0.2f, 0.9f);
            Gizmos.DrawWireCube(center, size);

            DrawSeeds();
            DrawEscapePath();

            if (!IsBaked || (!showLeaks && !showPlayArea))
                return;

            RebuildGizmoMeshes();

            // Play area first, so the leak cells inside it are the ones drawn last and stay visible.
            if (showPlayArea && playAreaMesh != null)
            {
                Gizmos.color = new Color(0.25f, 0.85f, 1f, 0.18f);
                Gizmos.DrawMesh(playAreaMesh);
                Gizmos.color = new Color(0.25f, 0.85f, 1f, 0.5f);
                Gizmos.DrawWireMesh(playAreaMesh);
            }

            if (showLeaks && leakMesh != null)
            {
                Gizmos.color = new Color(1f, 0.25f, 0.2f, 0.55f);
                Gizmos.DrawMesh(leakMesh);
                Gizmos.color = new Color(1f, 0.35f, 0.3f, 1f);
                Gizmos.DrawWireMesh(leakMesh);
            }
        }

        // Per volume, not shared: with two volumes selected a static list would have each of them
        // drawing the other's seeds, which is the one thing this is supposed to make unambiguous.
        readonly List<Vector3> seedCache = new List<Vector3>();
        float lastSeedScan = float.NegativeInfinity;

        /// <summary>The seeds, refreshed a few times a second. Read by the gizmo and by the Inspector's
        /// count - CollectSeeds walks every object in every loaded scene, and both of those redraw far
        /// more often than a marker moves.</summary>
        public IReadOnlyList<Vector3> CachedSeeds
        {
            get
            {
                if (Time.realtimeSinceStartup - lastSeedScan > 0.5f)
                {
                    lastSeedScan = Time.realtimeSinceStartup;
                    CollectSeeds(seedCache);
                }

                return seedCache;
            }
        }

        /// <summary>Draws the points the fill will start from.
        ///
        /// The single most useful thing on screen when a bake comes out wrong, because the two failures
        /// look nothing alike once you can see it: no seeds at all means the fill never ran, while a seed
        /// standing outside the building means it ran from the wrong place and filled the sky. Neither is
        /// visible in the numbers.</summary>
        void DrawSeeds()
        {
            // Back into local space - the gizmo matrix is this object's, and the seeds are world points.
            Gizmos.color = new Color(1f, 0.85f, 0.2f, 1f);
            float radius = Mathf.Max(0.25f, cellSize);

            foreach (Vector3 seed in CachedSeeds)
            {
                Vector3 local = transform.InverseTransformPoint(seed);
                Gizmos.DrawWireSphere(local, radius);
                Gizmos.DrawLine(local, local + Vector3.up * radius * 4f);
            }
        }

        /// <summary>Draws the route the fill took out of the map, and rings the tightest point on it.
        ///
        /// A line rather than more coloured cells, because the question that was still hard to answer is
        /// not "are there narrow places" - the red cells already say that, and on a real map there are
        /// dozens of innocent ones. It is "which of them did the fill actually LEAVE through", and a
        /// path answers that by construction: it is the route, so the hole is on it.</summary>
        void DrawEscapePath()
        {
            if (!showEscapePath || !HasEscapePath)
                return;

            Gizmos.color = new Color(1f, 0.2f, 0.9f, 0.9f);
            for (int i = 1; i < escapePath.Length; i++)
                Gizmos.DrawLine(escapePath[i - 1], escapePath[i]);

            // The ring is drawn generously large on purpose: the point of it is being findable from
            // across the map, not being accurate to the centimetre.
            Gizmos.color = new Color(1f, 0.2f, 0.9f, 1f);
            float ring = Mathf.Max(1f, bakedCellSize * 4f);
            Gizmos.DrawWireSphere(escapePoint, ring);
            Gizmos.DrawLine(escapePoint - Vector3.up * ring * 3f, escapePoint + Vector3.up * ring * 3f);
        }

        /// <summary>Rebuilds the two gizmo meshes when what they are showing has changed.
        ///
        /// A mesh rather than a loop of Gizmos.DrawCube, because the loop is what made the old slice view
        /// the only affordable option: a hundred thousand immediate-mode cubes is a frozen editor, while
        /// the same hundred thousand cells as one cached mesh is a single draw. That is the whole reason
        /// showPlayArea can exist at all.
        ///
        /// Only the faces on the OUTSIDE of the set are emitted, exactly as the voxel models are meshed
        /// and for the same reason - the interior faces are invisible and there are far more of them than
        /// there are visible ones.</summary>
        void RebuildGizmoMeshes()
        {
            int slice = sliceOnly
                ? Mathf.FloorToInt((sliceHeight - bakedOrigin.y) / bakedCellSize)
                : -1;

            int hash = System.HashCode.Combine(showLeaks, showPlayArea, slice, bakedAt, playCellCount);

            // A separate "have we built" flag rather than testing the meshes for null, because an empty
            // result is a legitimate answer - a bake with no leaks produces no leak mesh - and treating
            // null as "not built yet" would rescan the whole grid on every repaint precisely when there
            // is nothing to find.
            if (gizmoBuilt && hash == gizmoStateHash)
                return;

            gizmoStateHash = hash;
            gizmoBuilt = true;

            playAreaMesh = Rebuild(showPlayArea ? cells : null, slice, playAreaMesh);
            leakMesh = Rebuild(showLeaks ? narrow : null, slice, leakMesh);
        }

        /// <summary>Builds one mesh, and disposes of the old one when there is nothing to replace it
        /// with - a toggle switched off has to free its mesh, not just stop pointing at it.</summary>
        Mesh Rebuild(byte[] set, int slice, Mesh existing)
        {
            Mesh built = set != null ? BuildCellMesh(set, slice, existing) : null;

            if (built == null && existing != null)
                DestroyImmediate(existing);

            return built;
        }

        static readonly Vector3Int[] FaceNormals =
        {
            new Vector3Int(1, 0, 0), new Vector3Int(-1, 0, 0),
            new Vector3Int(0, 1, 0), new Vector3Int(0, -1, 0),
            new Vector3Int(0, 0, 1), new Vector3Int(0, 0, -1),
        };

        Mesh BuildCellMesh(byte[] set, int slice, Mesh reuse)
        {
            if (set == null)
                return null;

            var vertices = new List<Vector3>();
            var triangles = new List<int>();

            int yMin = slice >= 0 ? slice : 0;
            int yMax = slice >= 0 ? slice : bakedSize.y - 1;
            if (yMin < 0 || yMin >= bakedSize.y)
                return null;
            yMax = Mathf.Min(yMax, bakedSize.y - 1);

            for (int z = 0; z < bakedSize.z; z++)
            {
                for (int y = yMin; y <= yMax; y++)
                {
                    for (int x = 0; x < bakedSize.x; x++)
                    {
                        if (!GetBit(set, CellIndex(x, y, z)))
                            continue;

                        for (int face = 0; face < 6; face++)
                        {
                            Vector3Int n = FaceNormals[face];

                            // A slice shows its own top and bottom: the neighbour above and below is out
                            // of view, not absent, and culling against it would leave a single layer
                            // drawn as nothing at all.
                            bool neighbourInSet = n.y != 0 && slice >= 0
                                ? false
                                : InSet(set, x + n.x, y + n.y, z + n.z);

                            if (!neighbourInSet)
                                AddFace(vertices, triangles, x, y, z, face);
                        }
                    }
                }
            }

            if (vertices.Count == 0)
                return null;

            Mesh mesh = reuse != null ? reuse : new Mesh { name = "MapBoundsVolume Gizmo" };
            mesh.Clear();

            // A 90x30x90 volume can exceed 65k vertices several times over on its shell alone, and the
            // 16-bit default fails by drawing garbage rather than by complaining.
            mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            return mesh;
        }

        bool InSet(byte[] set, int x, int y, int z)
        {
            if (x < 0 || y < 0 || z < 0 || x >= bakedSize.x || y >= bakedSize.y || z >= bakedSize.z)
                return false;

            return GetBit(set, CellIndex(x, y, z));
        }

        void AddFace(List<Vector3> vertices, List<int> triangles, int x, int y, int z, int face)
        {
            Vector3 min = bakedOrigin + new Vector3(x, y, z) * bakedCellSize;
            float s = bakedCellSize;
            int b = vertices.Count;

            switch (face)
            {
                case 0: // +X
                    vertices.Add(min + new Vector3(s, 0, 0));
                    vertices.Add(min + new Vector3(s, s, 0));
                    vertices.Add(min + new Vector3(s, s, s));
                    vertices.Add(min + new Vector3(s, 0, s));
                    break;
                case 1: // -X
                    vertices.Add(min + new Vector3(0, 0, s));
                    vertices.Add(min + new Vector3(0, s, s));
                    vertices.Add(min + new Vector3(0, s, 0));
                    vertices.Add(min);
                    break;
                case 2: // +Y
                    vertices.Add(min + new Vector3(0, s, 0));
                    vertices.Add(min + new Vector3(0, s, s));
                    vertices.Add(min + new Vector3(s, s, s));
                    vertices.Add(min + new Vector3(s, s, 0));
                    break;
                case 3: // -Y
                    vertices.Add(min);
                    vertices.Add(min + new Vector3(s, 0, 0));
                    vertices.Add(min + new Vector3(s, 0, s));
                    vertices.Add(min + new Vector3(0, 0, s));
                    break;
                case 4: // +Z
                    vertices.Add(min + new Vector3(s, 0, s));
                    vertices.Add(min + new Vector3(s, s, s));
                    vertices.Add(min + new Vector3(0, s, s));
                    vertices.Add(min + new Vector3(0, 0, s));
                    break;
                default: // -Z
                    vertices.Add(min);
                    vertices.Add(min + new Vector3(0, s, 0));
                    vertices.Add(min + new Vector3(s, s, 0));
                    vertices.Add(min + new Vector3(s, 0, 0));
                    break;
            }

            triangles.Add(b);
            triangles.Add(b + 1);
            triangles.Add(b + 2);
            triangles.Add(b);
            triangles.Add(b + 2);
            triangles.Add(b + 3);
        }

#else
        void InvalidateGizmoMeshes() { }
#endif
    }
}
