using System.Collections.Generic;
using Mimicraft.Gameplay;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Mimicraft.EditorTools
{
    /// <summary>
    /// Bakes what a MapBoundsVolume covers into the volume itself.
    ///
    /// IN PLACE, in the scene you are looking at. The old baker instantiated a map prefab, moved it to
    /// the origin, baked, and put it back - which is why the feature only ever worked for maps that were
    /// prefabs, and why the Hunter room, a scene, could not be described at all. Baking where the volume
    /// already sits removes the whole apparatus: the physics queries answer about the world as it is
    /// authored, and the result is stored in the volume's own local space so it still travels with the
    /// object when MapLoader moves it later.
    ///
    /// What comes out is REACHABILITY, not the box. Cells are flood-filled from the map's own spawn
    /// markers and stopped by its own collision, so a concave footprint, an interior courtyard, stacked
    /// floors and a room inside a room all fall out of the fill. The box only bounds the search - which
    /// is also what makes a leak survivable rather than fatal: geometry with a crack in it fills the
    /// space beyond the crack up to the box and stops, instead of escaping into the world.
    ///
    /// KNOWN COST: the data goes stale if the map is edited and nobody re-bakes. There is no way around
    /// that short of baking at build time, which is the thing this exists to avoid - so the volume stamps
    /// a date on itself, remembers the box it ran against, and its Inspector says so in orange.
    /// </summary>
    public static class MapBoundsBaker
    {
        /// <summary>Cells are marked solid by testing a box slightly SMALLER than the cell. A box the
        /// full size catches a wall in the next cell over through sheer floating-point contact, and walls
        /// off doorways that are genuinely open.</summary>
        const float SolidProbeScale = 0.9f;

        // ---------------------------------------------------------------- Menu

        [MenuItem("Mimicraft/Setup/Bake Map Bounds (Secili)", priority = 40)]
        public static void BakeSelected()
        {
            var volumes = new List<MapBoundsVolume>();

            foreach (GameObject selected in Selection.gameObjects)
            {
                if (EditorUtility.IsPersistent(selected))
                {
                    Debug.LogWarning($"[MapBoundsBaker] '{selected.name}' bir prefab ASSET'i. Bake " +
                        "sahnedeki fizik dunyasina sorar, prefab dosyasina degil - prefab'i sahneye " +
                        "surukle, bake et, sonuc otomatik olarak asset'e islenir.", selected);
                    continue;
                }

                volumes.AddRange(selected.GetComponentsInChildren<MapBoundsVolume>(true));
            }

            if (volumes.Count == 0)
            {
                Debug.LogWarning("[MapBoundsBaker] Secimde MapBoundsVolume yok. Hierarchy'den hacmi " +
                                 "(veya onu iceren bir ust objeyi) sec.");
                return;
            }

            BakeAll(volumes);
        }

        [MenuItem("Mimicraft/Setup/Bake Map Bounds (Acik Sahnelerin Tamami)", priority = 41)]
        public static void BakeOpenScenes()
        {
            var volumes = new List<MapBoundsVolume>(
                Object.FindObjectsByType<MapBoundsVolume>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None));

            if (volumes.Count == 0)
            {
                Debug.LogWarning("[MapBoundsBaker] Acik sahnelerde hic MapBoundsVolume yok.");
                return;
            }

            BakeAll(volumes);
        }

        /// <summary>Single-volume entry point, used by the Inspector's own Bake button.</summary>
        public static void BakeAll(IList<MapBoundsVolume> volumes)
        {
            try
            {
                // Colliders answer from where PhysX thinks they are, and in the editor that is not
                // necessarily where the Inspector says - a transform typed in a moment ago has not
                // reached the physics world until something asks it to. A bake against stale transforms
                // is a bake of where the map used to be.
                Physics.SyncTransforms();

                for (int i = 0; i < volumes.Count; i++)
                {
                    MapBoundsVolume volume = volumes[i];
                    if (volume == null)
                        continue;

                    if (!Bake(volume, i, volumes.Count))
                        break;
                }

                // After every volume, not after each one: a marker outside THIS volume is very often
                // inside the next one - the Hunter room is its own region two hundred metres down - and
                // reporting per volume would cry wolf about every marker in the map each time.
                ReportStrandedMarkers(volumes);
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        /// <summary>Names every spawn marker that no baked volume covers.
        ///
        /// This is the check that was missing. A spawn point can sit two metres inside a wall and
        /// nothing says so: the map bakes cleanly, the marker draws its gizmo, the mode teleports a
        /// player onto it, and two seconds later MapBoundsWarden quietly pulls them somewhere else. What
        /// the player sees is "I spawned in the wrong place", with no way to connect that to a point
        /// somebody nudged inside a pillar - and it costs an afternoon to find. The bake knows the
        /// answer, so the bake should say it, at the one moment the person who can fix it is looking.
        ///
        /// Spawn kinds only. A HunterDoor is a thing to open, not a place to stand, and it is routinely
        /// authored inside the wall it is set into.</summary>
        static void ReportStrandedMarkers(IList<MapBoundsVolume> volumes)
        {
            const MapMarkerKind Spawns = MapMarkerKind.HunterSpawn | MapMarkerKind.HiderSpawn
                | MapMarkerKind.LobbySpawn | MapMarkerKind.ModelerSpawn | MapMarkerKind.HunterRoomSpawn;

            var baked = new List<MapBoundsVolume>();
            foreach (MapBoundsVolume volume in volumes)
            {
                if (volume != null && volume.IsBaked)
                    baked.Add(volume);
            }

            if (baked.Count == 0)
                return;

            int stranded = 0;

            // Inactive included: a spawn point is an empty GameObject and switching one off is a normal
            // way to park it, but a parked point that is still in the scene is still one somebody will
            // switch back on without re-checking it.
            foreach (MapMarker marker in
                     Object.FindObjectsByType<MapMarker>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if ((marker.Kind & Spawns) == 0)
                    continue;

                bool covered = false;
                foreach (MapBoundsVolume volume in baked)
                {
                    if (volume.Contains(marker.transform.position))
                    {
                        covered = true;
                        break;
                    }
                }

                if (covered)
                    continue;

                stranded++;
                Debug.LogWarning($"[MapBoundsBaker] '{marker.name}' ({marker.Kind}) oyun alaninin "
                    + $"DISINDA: {marker.transform.position}. Buraya isinlanan oyuncuyu "
                    + "MapBoundsWarden birkac saniye icinde baska bir yere geri alir - noktayi "
                    + "oynanabilir bir yere tasi ya da hacmi yeniden bake et.", marker);
            }

            if (stranded > 0)
            {
                Debug.LogWarning($"[MapBoundsBaker] {stranded} spawn noktasi oyun alaninin disinda. "
                    + "Yukaridaki satirlara tikla - her biri kendi marker'ini secer.");
            }
        }

        // ---------------------------------------------------------------- Bake

        /// <summary>Bakes one volume. Returns false only when the person at the keyboard cancelled -
        /// a volume that fails to bake is reported and skipped, because one broken map is no reason to
        /// abandon the other four in the queue.</summary>
        static bool Bake(MapBoundsVolume volume, int ordinal, int count)
        {
            Bounds box = volume.LocalBox;
            float cellSize = Mathf.Max(0.05f, volume.CellSize);

            if (box.size.x <= 0f || box.size.y <= 0f || box.size.z <= 0f)
            {
                Debug.LogError($"[MapBoundsBaker] '{volume.name}' hacminin boyutu sifir - bake edilecek " +
                               "bir sey yok.", volume);
                return true;
            }

            // Non-uniform scale would make a cube of cells a box of cells, and the whole grid assumes one
            // number for a cell's edge. Reported rather than corrected, because the fix is on the object
            // and silently baking something other than what is drawn is worse than refusing.
            // Prefab Mode has its own preview scene, and the colliders in it are not registered with the
            // physics world Physics.CheckBox answers from. The bake would not fail - it would find no
            // geometry at all, fill the entire box, and hand back a volume that says every point in the
            // world is play area. Refused rather than attempted, because that failure is invisible.
            if (UnityEditor.SceneManagement.PrefabStageUtility.GetPrefabStage(volume.gameObject) != null)
            {
                Debug.LogError($"[MapBoundsBaker] '{volume.name}' Prefab Mode'da. Prefab Mode'un fizik " +
                    "dunyasi ayridir - orada bake hicbir geometri gormez ve butun kutuyu oyun alani " +
                    "sayar. Prefab'i bir sahneye surukle, orada bake et: sonuc otomatik olarak " +
                    "prefab'e islenir.", volume);
                return true;
            }

            Vector3 scale = volume.transform.lossyScale;
            if (Mathf.Abs(scale.x - scale.y) > 0.001f || Mathf.Abs(scale.y - scale.z) > 0.001f)
            {
                Debug.LogError($"[MapBoundsBaker] '{volume.name}' esit olmayan scale'e sahip " +
                               $"({scale}). Hacim kupsel hucrelere bolunuyor - scale'i esitle.", volume);
                return true;
            }

            var size = new Vector3Int(
                Mathf.Max(1, Mathf.CeilToInt(box.size.x / cellSize)),
                Mathf.Max(1, Mathf.CeilToInt(box.size.y / cellSize)),
                Mathf.Max(1, Mathf.CeilToInt(box.size.z / cellSize)));

            // The cap is set by what the bake ALLOCATES, not by what it stores: five bytes a cell while
            // it runs - one for the solid map, four for the fill's parent links - against one bit a cell
            // once it is done. Eight million cells is forty megabytes of scratch and, at the default
            // size, a map a hundred metres on a side.
            long total = (long)size.x * size.y * size.z;
            if (total > 8_000_000L)
            {
                Debug.LogError($"[MapBoundsBaker] '{volume.name}' {size.x}x{size.y}x{size.z} = {total} " +
                               "hucre eder, sinir 8 milyon. Hucre boyutunu buyut ya da hacmi kucult.",
                               volume);
                return true;
            }

            Vector3 origin = box.min;
            int cellCount = (int)total;

            var seeds = new List<Vector3>();
            volume.CollectSeeds(seeds);

            if (seeds.Count == 0)
            {
                Debug.LogError($"[MapBoundsBaker] '{volume.name}' icinde hic tohum noktasi yok. Dolgu " +
                    "spawn noktalarindan basliyor: hacmin icine bir MapMarker koy, Tohum Turleri " +
                    "maskesini genislet, ya da Tohumlar listesine bir Transform ver.", volume);
                return true;
            }

            bool[] solid = MarkSolidCells(volume, origin, size, cellSize, cellCount, ordinal, count);
            if (solid == null)
                return false;

            byte[] blocked = MarkBlockedFaces(volume, origin, size, cellSize, cellCount, solid,
                ordinal, count);
            if (blocked == null)
                return false;

            var cells = new byte[(cellCount + 7) / 8];
            var fill = new Fill(cellCount);
            int freeCells = FloodFill(volume.transform, seeds, origin, size, cellSize, solid, blocked,
                cells, fill);

            if (freeCells == 0)
            {
                Debug.LogError($"[MapBoundsBaker] '{volume.name}' icin hicbir hucre doldurulamadi - " +
                    "tohum noktalari geometrinin icinde ya da hacmin disinda kalmis olabilir.", volume);
                return true;
            }

            // Recorded before anything is written, so undoing a bake restores the previous one whole -
            // the escape trail is written further down but is part of the same result.
            Undo.RecordObject(volume, "Bake Map Bounds");

            // Everything below runs BEFORE the crop, while the indices the trail was recorded in still
            // mean something.
            byte[] widths = MeasureWidths(size, cells, volume.NarrowThreshold, cellSize);

            // The player-sized fill: the same map, reachable only through gaps somebody could actually
            // walk down. Everything the ordinary fill reached and this one did not is space the fill
            // squeezed into, and the seam between the two is where it squeezed.
            int playerCells = Mathf.Max(1, Mathf.CeilToInt(volume.NarrowThreshold / cellSize));
            byte[] wide = FillWide(size, cells, widths, fill.Seeds, playerCells, out _);

            byte[] narrow = MarkLeakMouths(size, cells, wide, out int narrowCount);

            RecordEscape(volume, fill, size, origin, cellSize, cells, wide);
            ReportLeakWidth(volume, size, cells, widths, fill.Seeds, cellSize);
            WarnIfFillReachedTheWall(volume, size, cells);

            Crop(ref origin, ref size, ref cells, ref narrow, cellSize);

            volume.ApplyBake(origin, cellSize, size, cells, narrow, freeCells, narrowCount);
            Persist(volume);

            string leaks = narrowCount == 0
                ? "sizinti adayi yok"
                : $"{narrowCount} sizinti adayi hucre - kirmizi cizilenlere bak";

            Debug.Log($"[MapBoundsBaker] '{volume.name}' bake edildi: {size.x}x{size.y}x{size.z} hucre, "
                + $"{freeCells} tanesi oyun alani, {(cells.Length + narrow.Length) / 1024f:F1} KB, "
                + $"{leaks}.", volume);
            return true;
        }

        /// <summary>Writes the result somewhere it survives the editor closing.
        ///
        /// A volume inside a prefab instance has its bake pushed to the ASSET rather than left as an
        /// instance override, because MapLoader instantiates the prefab: data that only exists on the
        /// copy in this scene is data the game never sees. Only this one component is applied - applying
        /// the whole instance would sweep up every other unrelated override somebody had in progress.
        /// </summary>
        static void Persist(MapBoundsVolume volume)
        {
            EditorUtility.SetDirty(volume);

            if (!PrefabUtility.IsPartOfPrefabInstance(volume))
            {
                EditorSceneManager.MarkSceneDirty(volume.gameObject.scene);
                return;
            }

            string path = PrefabUtility.GetPrefabAssetPathOfNearestInstanceRoot(volume);
            if (string.IsNullOrEmpty(path))
                return;

            // A component somebody added to the instance rather than to the prefab is not an override of
            // anything, so the ordinary apply has nothing to write onto and silently does nothing. That
            // is the likely case the first time a map is given a volume.
            if (PrefabUtility.GetCorrespondingObjectFromSource(volume) == null)
                PrefabUtility.ApplyAddedComponent(volume, path, InteractionMode.AutomatedAction);
            else
                PrefabUtility.ApplyObjectOverride(volume, path, InteractionMode.AutomatedAction);
            Debug.Log($"[MapBoundsBaker] Sonuc '{path}' prefab'ine islendi.", volume);
        }

        // ---------------------------------------------------------------- Escape route

        /// <summary>Walks the fill's parent links back from the boundary to the seed and stores the
        /// route on the volume, marking the tightest point along it.
        ///
        /// This is the diagnostic that ends the search, and the reason the red cells were not enough on
        /// their own. Red says "a player could not fit here", and a real map has dozens of those that are
        /// perfectly innocent - the gap behind a wardrobe, the slot under a stair. It never said which
        /// one the fill actually LEFT through. A route does, by construction: it is the way out, so the
        /// hole is somewhere on it, and the narrowest point on a way out is almost always the hole
        /// itself.
        ///
        /// The width at that point is the last piece. Under a player's 0.8 metres it is a crack in the
        /// geometry and wants fixing; around a doorway's width it is a doorway, and the fill is leaving
        /// the way anybody would - which means the answer is a blocker or a smaller box, not a repair.
        /// </summary>
        static void RecordEscape(MapBoundsVolume volume, Fill fill, Vector3Int size, Vector3 origin,
            float cellSize, byte[] cells, byte[] wide)
        {
            if (fill.Escape < 0)
            {
                volume.ApplyEscape(null, default, 0f);
                return;
            }

            var route = new List<int>();
            int at = fill.Escape;
            int guard = 0;

            while (at >= 0 && guard++ < 4_000_000)
            {
                route.Add(at);
                at = fill.Parent[at];
            }

            // The route comes back from the wall inward, so reversing it puts the seed first - and the
            // first step off player-sized ground is then the moment the fill squeezed out, rather than
            // the last one before it reached the wall.
            route.Reverse();

            var points = new Vector3[route.Count];
            Vector3 mouth = default;
            float mouthWidth = 0f;
            bool found = false;

            for (int i = 0; i < route.Count; i++)
            {
                int index = route[i];
                int x = index % size.x;
                int y = index / size.x % size.y;
                int z = index / (size.x * size.y);

                Vector3 centre = origin + new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) * cellSize;
                points[i] = centre;

                // The first cell on the way out that a player could not have reached. Defined by the
                // failure itself rather than by measuring the route and picking its narrowest point:
                // "where did this stop being somewhere a person could walk" is the actual question, and
                // the narrowest point is only a guess at it.
                if (found || (wide[index >> 3] & (1 << (index & 7))) != 0)
                    continue;

                found = true;
                mouth = centre;

                int limit = Mathf.Clamp(Mathf.CeilToInt(volume.NarrowThreshold / cellSize) + 1, 2, 60);
                mouthWidth = Mathf.Min(
                    Span(size, cells, limit, x, y, z, 1, 0, 0),
                    Mathf.Min(Span(size, cells, limit, x, y, z, 0, 1, 0),
                              Span(size, cells, limit, x, y, z, 0, 0, 1))) * cellSize;
            }

            // No mouth means the fill never left player-sized space on its way out: it walked out of a
            // door, or the box simply does not reach the wall of the map. The end of the route is still
            // worth pointing at.
            volume.ApplyEscape(points, found ? mouth : points[points.Length - 1], mouthWidth);
        }

        // ---------------------------------------------------------------- Spill

        /// <summary>Says so when the fill ran all the way to the volume's own walls.
        ///
        /// Which is the difference between a bake that describes a map and one that describes a box. The
        /// fill is meant to be stopped by GEOMETRY; when it is stopped by the volume instead, either the
        /// box is cutting through the map, or - far more often - the fill got out of the building and
        /// filled the open air, and every point in the box now counts as play area.
        ///
        /// Reported rather than prevented, because reaching the wall is sometimes exactly right: an
        /// outdoor map's sky is play area, and Modelciler climb into it. Only the person who drew the box
        /// knows which of the two this is, so they are told which faces and left to decide.</summary>
        static void WarnIfFillReachedTheWall(MapBoundsVolume volume, Vector3Int size, byte[] cells)
        {
            var faces = new List<string>();

            if (TouchesPlane(size, cells, 0, 0)) faces.Add("-X");
            if (TouchesPlane(size, cells, 0, size.x - 1)) faces.Add("+X");
            if (TouchesPlane(size, cells, 1, 0)) faces.Add("-Y (alt)");
            if (TouchesPlane(size, cells, 1, size.y - 1)) faces.Add("+Y (ust)");
            if (TouchesPlane(size, cells, 2, 0)) faces.Add("-Z");
            if (TouchesPlane(size, cells, 2, size.z - 1)) faces.Add("+Z");

            if (faces.Count == 0)
                return;

            var message = new System.Text.StringBuilder();
            message.Append($"[MapBoundsBaker] '{volume.name}': dolgu hacmin duvarina dayandi ")
                   .Append($"({string.Join(", ", faces)}). Dolguyu durduran geometri degil kutunun ")
                   .Append("kendisi - ya kutu haritayi kesiyor, ya da dolgu binadan cikip aciga tasti.");

            if (volume.HasEscapePath)
            {
                message.Append($"\nEn dar gectigi yer {volume.EscapeWidth:0.##} m, konum ")
                       .Append($"{volume.EscapePoint}. Inspector'da 'Sizintiya git' oraya goturur.")
                       .Append(Describe(volume, volume.EscapePoint));
            }

            message.Append("\nAcik hava haritasiysa bu normaldir.");
            Debug.LogWarning(message.ToString(), volume);
        }

        /// <summary>Names what is standing where the fill got out.
        ///
        /// The last question the diagnostics could not answer. The route says WHERE, and the width says
        /// whether a player could have followed it - but neither says why the geometry did not stop it,
        /// and there are three different answers with three different fixes.
        ///
        /// Collision is there: the fill went round it, or through an opening. Look at the route.
        ///
        /// Collision is NOT there but a renderer is: a wall you can see and walk through. This is the one
        /// that wastes the most time, because the scene view shows a perfectly solid room and the bake is
        /// simply telling the truth about it. Imported geometry arrives without colliders unless somebody
        /// adds them, and one missed piece is one missed piece.
        ///
        /// Nothing at all: the fill left through open air, and the box is bigger than the map.</summary>
        static string Describe(MapBoundsVolume volume, Vector3 at)
        {
            const float Radius = 2f;
            var report = new System.Text.StringBuilder();

            Collider[] solid = Physics.OverlapSphere(at, Radius, volume.BlockingLayers,
                QueryTriggerInteraction.Ignore);

            report.Append("\nBurada collision: ")
                  .Append(solid.Length == 0 ? "YOK" : Names(System.Array.ConvertAll(solid, c => c.name)));

            var ghosts = new List<string>();

            foreach (Renderer renderer in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                if (renderer.bounds.SqrDistance(at) > Radius * Radius)
                    continue;

                // Up as well as down: a single MeshCollider on a parent covers a whole imported model,
                // and calling every one of its child renderers collisionless would be noise, not a lead.
                if (renderer.GetComponentInParent<Collider>() != null
                    || renderer.GetComponentInChildren<Collider>() != null)
                    continue;

                ghosts.Add(renderer.name);
            }

            if (ghosts.Count > 0)
            {
                report.Append("\nCOLLIDER'I OLMAYAN gorunur geometri: ").Append(Names(ghosts.ToArray()))
                      .Append(" - gorunen ama gecilebilen duvar budur, MeshCollider ekle.");
            }
            else if (solid.Length == 0)
            {
                report.Append(" - burada hicbir sey yok, dolgu acik havadan cikiyor. Kutu haritadan buyuk.");
            }

            return report.ToString();
        }

        static string Names(string[] names)
        {
            const int Shown = 4;
            string list = string.Join(", ", names, 0, Mathf.Min(Shown, names.Length));
            return names.Length > Shown ? $"{list} (+{names.Length - Shown})" : list;
        }

        /// <summary>Whether any filled cell sits on one bounding plane of the grid.</summary>
        static bool TouchesPlane(Vector3Int size, byte[] cells, int axis, int at)
        {
            // The two axes still free to vary on this plane.
            int uAxis = axis == 0 ? 1 : 0;
            int vAxis = axis == 2 ? 1 : 2;

            int uCount = uAxis == 0 ? size.x : uAxis == 1 ? size.y : size.z;
            int vCount = vAxis == 0 ? size.x : vAxis == 1 ? size.y : size.z;

            var cell = Vector3Int.zero;
            cell[axis] = at;

            for (int u = 0; u < uCount; u++)
            {
                cell[uAxis] = u;

                for (int v = 0; v < vCount; v++)
                {
                    cell[vAxis] = v;

                    int index = (cell.z * size.y + cell.y) * size.x + cell.x;
                    if ((cells[index >> 3] & (1 << (index & 7))) != 0)
                        return true;
                }
            }

            return false;
        }

        // ---------------------------------------------------------------- Solid

        /// <summary>Which cells are blocked.
        ///
        /// By LAYER rather than by "is a child of the map", which is what the old baker had to ask
        /// because it bakes a prefab it had just dropped into an arbitrary scene. Baking in place makes
        /// the question straightforward again: everything solid in the world is solid, and the mask is
        /// there for the exceptions - a decorative collider, a trigger volume's shell, a blocker that
        /// only stops projectiles.</summary>
        static bool[] MarkSolidCells(MapBoundsVolume volume, Vector3 origin, Vector3Int size,
            float cellSize, int total, int ordinal, int count)
        {
            var solid = new bool[total];
            Vector3 half = Vector3.one * (cellSize * 0.5f * SolidProbeScale);
            Transform space = volume.transform;
            Quaternion rotation = space.rotation;
            int mask = volume.BlockingLayers;
            int index = 0;

            for (int z = 0; z < size.z; z++)
            {
                // Per slice, not per cell: this loop is millions of physics queries on a large map and
                // it is the only part of a bake anyone waits for, so it has to be interruptible - but a
                // progress bar redrawn a million times costs more than the work it is reporting on.
                if (EditorUtility.DisplayCancelableProgressBar("Map Bounds",
                        $"'{volume.name}' taraniyor ({ordinal + 1}/{count})",
                        (ordinal + (float)z / size.z) / count))
                    return null;

                for (int y = 0; y < size.y; y++)
                {
                    for (int x = 0; x < size.x; x++, index++)
                    {
                        Vector3 local = origin + new Vector3(x + 0.5f, y + 0.5f, z + 0.5f) * cellSize;
                        Vector3 world = space.TransformPoint(local);

                        solid[index] = Physics.CheckBox(world, half, rotation, mask,
                            QueryTriggerInteraction.Ignore);
                    }
                }
            }

            return solid;
        }

        /// <summary>Which cell BOUNDARIES have geometry sitting in them.
        ///
        /// This is the pass that stops the fill walking through walls, and it exists because the cell
        /// probes above cannot do it. They test a box slightly smaller than the cell, deliberately - a
        /// full-size one touches the next cell along and seals doorways that are genuinely open - and the
        /// price of that shrink is a blind band, five centimetres wide at the default cell size, sitting
        /// exactly on every boundary between two cells. A wall thin enough to fit inside that band, and
        /// unlucky enough to be aligned with it, is invisible to both cells it separates. They both bake
        /// free, they are six-connected, and the fill strolls through the wall.
        ///
        /// That is not a rare accident. Walls exported from a modelling package are often a single plane
        /// with no thickness at all, and a building modelled on a half- or one-metre grid lines those
        /// planes up with the cell grid along an entire wall at a time - so the failure is not one cell
        /// here and there, it is a whole side of a room quietly missing.
        ///
        /// Testing the boundary itself closes it without giving up what the shrink bought: the slab is
        /// thin, straddles the face, and is inset in the other two directions, so a doorway's opening is
        /// still an opening and only something actually IN the gap between two cells blocks the step.
        ///
        /// One byte a cell, three bits used: the +X, +Y and +Z faces. The other three belong to the
        /// neighbours, and testing them here would test every boundary twice.</summary>
        static byte[] MarkBlockedFaces(MapBoundsVolume volume, Vector3 origin, Vector3Int size,
            float cellSize, int total, bool[] solid, int ordinal, int count)
        {
            var blocked = new byte[total];

            // Thick enough to more than cover the band the cell probes miss, thin enough that it only
            // ever describes the boundary. Inset in the tangential directions by the same shrink the cell
            // probes use, so a wall passing the very edge of a face - where it is not between the two
            // cells in any meaningful sense - does not read as a barrier.
            float gap = cellSize * 0.5f * (1f - SolidProbeScale) * 1.6f;
            float span = cellSize * 0.5f * SolidProbeScale;

            var halves = new[]
            {
                new Vector3(gap, span, span),
                new Vector3(span, gap, span),
                new Vector3(span, span, gap),
            };

            Transform space = volume.transform;
            Quaternion rotation = space.rotation;
            int mask = volume.BlockingLayers;

            for (int z = 0; z < size.z; z++)
            {
                if (EditorUtility.DisplayCancelableProgressBar("Map Bounds",
                        $"'{volume.name}' duvarlari araniyor ({ordinal + 1}/{count})",
                        (ordinal + 0.5f + 0.5f * z / size.z) / count))
                    return null;

                for (int y = 0; y < size.y; y++)
                {
                    for (int x = 0; x < size.x; x++)
                    {
                        int index = (z * size.y + y) * size.x + x;

                        // A solid cell is already a wall; what is on its faces changes nothing, and
                        // skipping it is most of the map on an interior scene.
                        if (solid[index])
                            continue;

                        for (int axis = 0; axis < 3; axis++)
                        {
                            var neighbour = new Vector3Int(x, y, z);
                            neighbour[axis]++;

                            if (neighbour[axis] >= (axis == 0 ? size.x : axis == 1 ? size.y : size.z))
                                continue;

                            int other = (neighbour.z * size.y + neighbour.y) * size.x + neighbour.x;
                            if (solid[other])
                                continue;

                            var centre = new Vector3(x + 0.5f, y + 0.5f, z + 0.5f);
                            centre[axis] += 0.5f;

                            Vector3 world = space.TransformPoint(origin + centre * cellSize);

                            if (Physics.CheckBox(world, halves[axis], rotation, mask,
                                    QueryTriggerInteraction.Ignore))
                                blocked[index] |= (byte)(1 << axis);
                        }
                    }
                }
            }

            return blocked;
        }

        // ---------------------------------------------------------------- Fill

        /// <summary>Everything reachable from the seeds without passing through geometry.
        ///
        /// Six-connected rather than diagonal: a diagonal step squeezes between two cells that touch only
        /// along an edge, which on a map means leaking through the corner where two walls meet.</summary>
        /// <summary>What the fill learns on its way round, beyond which cells it reached.
        ///
        /// Parent links, so any cell can be walked back to the seed it came from - which is the whole
        /// answer to "where did this get out". Breadth-first order means those links form SHORTEST
        /// routes, so the trail that comes back is the most direct way out rather than a tour of the
        /// building.</summary>
        sealed class Fill
        {
            public readonly int[] Parent;

            /// <summary>The cells the fill actually started from, after the lift off the floor. Needed
            /// again by the second, narrower fill - which has to start from exactly the same places or
            /// the two are not comparable.</summary>
            public readonly List<int> Seeds = new List<int>();

            /// <summary>The first filled cell found on the grid's own boundary. First in breadth-first
            /// order, so it is the closest one to a seed - of all the ways the fill got out, the
            /// shortest, which is the one worth showing.</summary>
            public int Escape = -1;

            public Fill(int cells)
            {
                Parent = new int[cells];
                for (int i = 0; i < cells; i++)
                    Parent[i] = -1;
            }
        }

        static int FloodFill(Transform space, List<Vector3> seeds, Vector3 origin, Vector3Int size,
            float cellSize, bool[] solid, byte[] blocked, byte[] cells, Fill fill)
        {
            var queue = new Queue<Vector3Int>();
            int filled = 0;

            foreach (Vector3 seed in seeds)
            {
                // Into the volume's own space first. Seeds are world positions - a marker's transform -
                // while the grid is local, because that is how the data has to be stored for MapLoader to
                // be free to move the whole scene afterwards.
                Vector3 local = space.InverseTransformPoint(seed) - origin;
                var cell = new Vector3Int(
                    Mathf.FloorToInt(local.x / cellSize),
                    Mathf.FloorToInt(local.y / cellSize),
                    Mathf.FloorToInt(local.z / cellSize));

                // Upward until the seed is in open air. A spawn marker sits ON the floor, and the cell
                // around a surface bakes SOLID by design - starting the fill exactly at the marker would
                // refuse to start it at all, and the bake would report an empty volume for a map that is
                // perfectly fine. Four cells is head height at the default size; beyond that the marker
                // is genuinely buried and the error message is the right outcome.
                for (int lift = 0; lift <= 4; lift++)
                {
                    var lifted = new Vector3Int(cell.x, cell.y + lift, cell.z);
                    if (!TryEnqueue(lifted, size, solid, cells, queue, ref filled, fill, -1))
                        continue;

                    if (lifted.x >= 0 && lifted.y >= 0 && lifted.z >= 0 && lifted.x < size.x
                        && lifted.y < size.y && lifted.z < size.z)
                        fill.Seeds.Add((lifted.z * size.y + lifted.y) * size.x + lifted.x);

                    break;
                }
            }

            while (queue.Count > 0)
            {
                Vector3Int cell = queue.Dequeue();
                int from = (cell.z * size.y + cell.y) * size.x + cell.x;

                for (int axis = 0; axis < 3; axis++)
                {
                    TryStep(cell, axis, 1, size, solid, blocked, cells, queue, ref filled, fill, from);
                    TryStep(cell, axis, -1, size, solid, blocked, cells, queue, ref filled, fill, from);
                }
            }

            return filled;
        }

        /// <summary>One step to a neighbour, if the boundary between them is clear.
        ///
        /// Which face to consult depends on the direction: a step forward reads this cell's own face, a
        /// step back reads the neighbour's forward face. There is one bit per boundary, not two, and this
        /// is where that pays for itself - the two cells sharing it can never disagree about whether it
        /// is passable.</summary>
        static void TryStep(Vector3Int cell, int axis, int sign, Vector3Int size, bool[] solid,
            byte[] blocked, byte[] cells, Queue<Vector3Int> queue, ref int filled, Fill fill, int from)
        {
            var neighbour = cell;
            neighbour[axis] += sign;

            if (neighbour.x < 0 || neighbour.y < 0 || neighbour.z < 0
                || neighbour.x >= size.x || neighbour.y >= size.y || neighbour.z >= size.z)
                return;

            Vector3Int owner = sign > 0 ? cell : neighbour;
            int ownerIndex = (owner.z * size.y + owner.y) * size.x + owner.x;

            if ((blocked[ownerIndex] & (1 << axis)) != 0)
                return;

            TryEnqueue(neighbour, size, solid, cells, queue, ref filled, fill, from);
        }

        /// <summary>Adds a cell to the fill. Returns whether it is now part of the play area - which
        /// includes cells another seed already reached, so a second marker in the same room is not
        /// mistaken for a marker stuck inside a wall.</summary>
        static bool TryEnqueue(Vector3Int cell, Vector3Int size, bool[] solid, byte[] cells,
            Queue<Vector3Int> queue, ref int filled, Fill fill, int from)
        {
            if (cell.x < 0 || cell.y < 0 || cell.z < 0
                || cell.x >= size.x || cell.y >= size.y || cell.z >= size.z)
                return false;

            int index = (cell.z * size.y + cell.y) * size.x + cell.x;
            if (solid[index])
                return false;

            if ((cells[index >> 3] & (1 << (index & 7))) != 0)
                return true;

            cells[index >> 3] |= (byte)(1 << (index & 7));
            fill.Parent[index] = from;
            filled++;
            queue.Enqueue(cell);

            // Noted the first time it happens, and only then: in breadth-first order the first cell to
            // touch the boundary is the one the fill reached soonest, which is the shortest way out.
            if (fill.Escape < 0 && OnBoundary(cell, size))
                fill.Escape = index;

            return true;
        }

        static bool OnBoundary(Vector3Int cell, Vector3Int size) =>
            cell.x == 0 || cell.y == 0 || cell.z == 0
            || cell.x == size.x - 1 || cell.y == size.y - 1 || cell.z == size.z - 1;

        // ---------------------------------------------------------------- Leak candidates

        /// <summary>How wide the play area is at each cell, in CELLS, capped at what matters.
        ///
        /// The narrowest of the three axes, which is what "could somebody get through here" means: a slot
        /// that is generous in two directions and one cell in the third is still a slot. Measured through
        /// the play area rather than to the nearest solid cell, so a pocket the fill never entered reads
        /// as a wall - which for this question it is.
        ///
        /// A byte a cell, and capped, because the difference between wide and wider is not a question
        /// anybody here is asking.</summary>
        static byte[] MeasureWidths(Vector3Int size, byte[] cells, float threshold, float cellSize)
        {
            int total = size.x * size.y * size.z;
            var widths = new byte[total];
            int limit = Mathf.Clamp(Mathf.CeilToInt(threshold / cellSize) + 1, 2, 60);

            for (int z = 0; z < size.z; z++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    for (int x = 0; x < size.x; x++)
                    {
                        int index = (z * size.y + y) * size.x + x;
                        if ((cells[index >> 3] & (1 << (index & 7))) == 0)
                            continue;

                        int width = Span(size, cells, limit, x, y, z, 1, 0, 0);
                        width = Mathf.Min(width, Span(size, cells, limit, x, y, z, 0, 1, 0));
                        width = Mathf.Min(width, Span(size, cells, limit, x, y, z, 0, 0, 1));

                        widths[index] = (byte)Mathf.Min(width, 255);
                    }
                }
            }

            return widths;
        }

        /// <summary>How many play-area cells there are through this one along an axis, capped.</summary>
        static int Span(Vector3Int size, byte[] cells, int limit, int x, int y, int z,
            int dx, int dy, int dz)
        {
            int span = 1;

            for (int step = 1; step <= limit; step++)
            {
                if (!IsSet(size, cells, x + dx * step, y + dy * step, z + dz * step))
                    break;
                span++;
            }

            for (int step = 1; step <= limit; step++)
            {
                if (!IsSet(size, cells, x - dx * step, y - dy * step, z - dz * step))
                    break;
                span++;
            }

            return span;
        }

        /// <summary>The same fill again, but only through gaps at least this many cells wide.
        ///
        /// Started from the same seeds regardless of how tight THEY are: a spawn point is somewhere the
        /// map author said a player stands, and disqualifying it for being in a doorway would leave
        /// nothing to compare against.</summary>
        static byte[] FillWide(Vector3Int size, byte[] cells, byte[] widths, List<int> seeds,
            int minWidth, out bool reachedBoundary)
        {
            int total = size.x * size.y * size.z;
            var wide = new byte[(total + 7) / 8];
            var queue = new Queue<int>();
            reachedBoundary = false;

            foreach (int seed in seeds)
            {
                if ((wide[seed >> 3] & (1 << (seed & 7))) != 0)
                    continue;

                wide[seed >> 3] |= (byte)(1 << (seed & 7));
                queue.Enqueue(seed);
            }

            while (queue.Count > 0)
            {
                int index = queue.Dequeue();
                int x = index % size.x;
                int y = index / size.x % size.y;
                int z = index / (size.x * size.y);

                if (x == 0 || y == 0 || z == 0
                    || x == size.x - 1 || y == size.y - 1 || z == size.z - 1)
                    reachedBoundary = true;

                for (int axis = 0; axis < 3; axis++)
                {
                    for (int sign = -1; sign <= 1; sign += 2)
                    {
                        var step = new Vector3Int(x, y, z);
                        step[axis] += sign;

                        if (step.x < 0 || step.y < 0 || step.z < 0
                            || step.x >= size.x || step.y >= size.y || step.z >= size.z)
                            continue;

                        int next = (step.z * size.y + step.y) * size.x + step.x;

                        if ((cells[next >> 3] & (1 << (next & 7))) == 0)
                            continue;
                        if ((wide[next >> 3] & (1 << (next & 7))) != 0)
                            continue;
                        if (widths[next] < minWidth)
                            continue;

                        wide[next >> 3] |= (byte)(1 << (next & 7));
                        queue.Enqueue(next);
                    }
                }
            }

            return wide;
        }

        /// <summary>The cells where the fill left player-sized space, and nothing else.
        ///
        /// This is what the red cells mean now, and the change is the whole point. They used to be every
        /// cell in the map pinched below a player's width, which on a real map is dozens of perfectly
        /// innocent places - the gap behind a wardrobe, the slot under a stair - and none of them was the
        /// leak. A mouth is narrower than that in the only way that matters: it is a tight cell the fill
        /// entered FROM somewhere a player can stand. Every route out of the honest map passes through
        /// one, and there are usually a handful in the whole volume.</summary>
        static byte[] MarkLeakMouths(Vector3Int size, byte[] cells, byte[] wide, out int count)
        {
            int total = size.x * size.y * size.z;
            var mouths = new byte[(total + 7) / 8];
            count = 0;

            for (int z = 0; z < size.z; z++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    for (int x = 0; x < size.x; x++)
                    {
                        int index = (z * size.y + y) * size.x + x;

                        if ((cells[index >> 3] & (1 << (index & 7))) == 0)
                            continue;
                        if ((wide[index >> 3] & (1 << (index & 7))) != 0)
                            continue;

                        if (!TouchesWide(size, wide, x, y, z))
                            continue;

                        mouths[index >> 3] |= (byte)(1 << (index & 7));
                        count++;
                    }
                }
            }

            return mouths;
        }

        static bool TouchesWide(Vector3Int size, byte[] wide, int x, int y, int z)
        {
            for (int axis = 0; axis < 3; axis++)
            {
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    var step = new Vector3Int(x, y, z);
                    step[axis] += sign;

                    if (step.x < 0 || step.y < 0 || step.z < 0
                        || step.x >= size.x || step.y >= size.y || step.z >= size.z)
                        continue;

                    int index = (step.z * size.y + step.y) * size.x + step.x;
                    if ((wide[index >> 3] & (1 << (index & 7))) != 0)
                        return true;
                }
            }

            return false;
        }

        /// <summary>Finds how wide the gap the fill escapes through actually is, and says so.
        ///
        /// Straight out of the observation that made this possible: baking at 0.7 did not leak and 0.6
        /// did. That is not two bakes disagreeing, it is a measurement - the hole is somewhere between
        /// those two numbers, and the fill only fits through it once the cells are small enough. So this
        /// asks the question directly instead of making anybody re-bake to find out: widen the minimum
        /// passable gap one cell at a time and see where the fill stops getting out.
        ///
        /// The answer is a number you can act on. Below a player's width it is a crack and the geometry
        /// wants repairing; at a door's width it is a door, and no repair will change that.</summary>
        static void ReportLeakWidth(MapBoundsVolume volume, Vector3Int size, byte[] cells, byte[] widths,
            List<int> seeds, float cellSize)
        {
            FillWide(size, cells, widths, seeds, 1, out bool escapesAtAll);
            if (!escapesAtAll)
                return;

            // One cell at a time rather than a binary search: the range is a handful of steps, and the
            // first width that holds is the answer whether or not the escapes above it are the same one.
            int limit = Mathf.Clamp(Mathf.CeilToInt(volume.NarrowThreshold / cellSize) + 1, 2, 60);

            for (int minWidth = 2; minWidth <= limit; minWidth++)
            {
                FillWide(size, cells, widths, seeds, minWidth, out bool escapes);
                if (escapes)
                    continue;

                float gap = (minWidth - 1) * cellSize;
                Debug.Log($"[MapBoundsBaker] '{volume.name}': dolgu yaklasik {gap:0.##} m genisliginde " +
                    $"bir yerden kaciyor ({minWidth - 1} hucre). Oyuncu genisligi " +
                    $"{volume.NarrowThreshold:0.##} m - " +
                    (gap < volume.NarrowThreshold
                        ? "bu bir CATLAK, geometri birlesmiyor. Kirmizi hucreler agzini gosteriyor."
                        : "bu gercek bir aciklik (kapi/pencere), catlak degil."), volume);
                return;
            }

            Debug.Log($"[MapBoundsBaker] '{volume.name}': dolgu dar bir yerden degil, genis bir " +
                "aciklikan cikiyor - kutu haritadan buyuk ya da harita bu yonde acik.", volume);
        }

        static bool IsSet(Vector3Int size, byte[] cells, int x, int y, int z)
        {
            if (x < 0 || y < 0 || z < 0 || x >= size.x || y >= size.y || z >= size.z)
                return false;

            int index = (z * size.y + y) * size.x + x;
            return (cells[index >> 3] & (1 << (index & 7))) != 0;
        }

        // ---------------------------------------------------------------- Crop

        /// <summary>Shrinks the grid to the part that actually got filled.
        ///
        /// A volume is drawn generously - around the building, over the roof, with room to spare - and
        /// most of that generosity is air the fill never reaches, because the walls are what stop it.
        /// Keeping it would mean storing a shell of permanently-empty cells around every map, which on a
        /// mid-sized one is most of the array.
        ///
        /// Only the empty rind comes off: the fill's own extent is kept exactly, so the sky above the map
        /// that a Modelci can climb into is still in there.</summary>
        static void Crop(ref Vector3 origin, ref Vector3Int size, ref byte[] cells, ref byte[] narrow,
            float cellSize)
        {
            var min = new Vector3Int(int.MaxValue, int.MaxValue, int.MaxValue);
            var max = new Vector3Int(int.MinValue, int.MinValue, int.MinValue);

            for (int z = 0; z < size.z; z++)
            {
                for (int y = 0; y < size.y; y++)
                {
                    for (int x = 0; x < size.x; x++)
                    {
                        int index = (z * size.y + y) * size.x + x;
                        if ((cells[index >> 3] & (1 << (index & 7))) == 0)
                            continue;

                        min = Vector3Int.Min(min, new Vector3Int(x, y, z));
                        max = Vector3Int.Max(max, new Vector3Int(x, y, z));
                    }
                }
            }

            Vector3Int cropped = max - min + Vector3Int.one;
            if (cropped == size)
                return;

            int total = cropped.x * cropped.y * cropped.z;
            var packedCells = new byte[(total + 7) / 8];
            var packedNarrow = new byte[(total + 7) / 8];

            for (int z = 0; z < cropped.z; z++)
            {
                for (int y = 0; y < cropped.y; y++)
                {
                    for (int x = 0; x < cropped.x; x++)
                    {
                        int source = ((z + min.z) * size.y + (y + min.y)) * size.x + (x + min.x);
                        int target = (z * cropped.y + y) * cropped.x + x;

                        if ((cells[source >> 3] & (1 << (source & 7))) != 0)
                            packedCells[target >> 3] |= (byte)(1 << (target & 7));

                        // Carried through the same remap rather than recomputed afterwards: the red set
                        // is defined against the UNCROPPED grid, where the fill's own indices still mean
                        // something, and recomputing it on the cropped one would quietly answer a
                        // slightly different question.
                        if ((narrow[source >> 3] & (1 << (source & 7))) != 0)
                            packedNarrow[target >> 3] |= (byte)(1 << (target & 7));
                    }
                }
            }

            origin += (Vector3)min * cellSize;
            size = cropped;
            cells = packedCells;
            narrow = packedNarrow;
        }
    }
}
