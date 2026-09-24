using Mimicraft.Gameplay;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Mimicraft.EditorTools
{
    /// <summary>
    /// The volume's Inspector: a draggable box in the scene, a Bake button, and an honest readout.
    ///
    /// The readout is the part that earns its place. Baked data is invisible by nature - it looks
    /// identical whether it is right, empty, or describing a box you moved yesterday - so the three
    /// things worth knowing say themselves here rather than waiting to be discovered in a live round:
    /// whether it is baked at all, whether it still matches the box on screen, and how many cells the
    /// bake thinks a player could not fit through.
    /// </summary>
    [CustomEditor(typeof(MapBoundsVolume))]
    [CanEditMultipleObjects]
    public class MapBoundsVolumeEditor : Editor
    {
        readonly BoxBoundsHandle handle = new BoxBoundsHandle();

        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            EditorGUILayout.Space();

            var volume = (MapBoundsVolume)target;

            DrawLayerWarning();
            DrawFitButtons();

            EditorGUILayout.Space();
            DrawSeedStatus(volume);
            DrawStatus(volume);
            DrawEscape(volume);

            EditorGUILayout.Space();

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Bake", GUILayout.Height(28f)))
                {
                    var volumes = new MapBoundsVolume[targets.Length];
                    for (int i = 0; i < targets.Length; i++)
                        volumes[i] = (MapBoundsVolume)targets[i];

                    MapBoundsBaker.BakeAll(volumes);
                }

                using (new EditorGUI.DisabledScope(!volume.IsBaked))
                {
                    if (GUILayout.Button("Bake'i Sil", GUILayout.Height(28f), GUILayout.Width(90f)))
                    {
                        foreach (Object each in targets)
                        {
                            var target = (MapBoundsVolume)each;
                            Undo.RecordObject(target, "Clear Map Bounds");
                            target.ClearBake();
                            EditorUtility.SetDirty(target);
                        }
                    }
                }
            }
        }

        /// <summary>Offers to repair a mask that still says "everything".
        ///
        /// Shown rather than fixed silently, because the mask is authoring data somebody may have set on
        /// purpose - but shown at all because both ways it goes wrong are invisible. A player standing in
        /// the scene during a bake carves a permanent hole in the map's bounds, and the Studio preview
        /// stage, parked far from the origin so the map cannot see it, drags any fit out to meet it.
        /// </summary>
        void DrawLayerWarning()
        {
            bool anyExtra = false;
            foreach (Object each in targets)
            {
                if ((((MapBoundsVolume)each).BlockingLayers.value & ~MapBoundsVolume.MapLayers) != 0)
                    anyExtra = true;
            }

            if (!anyExtra)
                return;

            EditorGUILayout.HelpBox(
                "Engelleyen Katmanlar maskesi haritaya ait olmayan katmanlari da iceriyor - oyuncular, " +
                "voxel prop'lar, FPS eli, Studio sahnesi. Bake sirasinda sahnede duran bir oyuncu " +
                "haritada kalici bir delik acar; sigdirma da kutuyu Studio sahnesine kadar uzatir.",
                MessageType.Warning);

            if (!GUILayout.Button("Maskeyi harita katmanlarina ayarla"))
                return;

            foreach (Object each in targets)
            {
                var volume = (MapBoundsVolume)each;
                Undo.RecordObject(volume, "Fix Map Bounds Layers");
                volume.UseMapLayers();
                EditorUtility.SetDirty(volume);
            }
        }

        // ---------------------------------------------------------------- Fit

        /// <summary>Which geometry a Sigdir button measures.</summary>
        enum FitScope
        {
            /// <summary>Everything loaded. The answer for a map whose geometry lives across more than
            /// one scene and you want a single box around the lot.</summary>
            AllScenes,

            /// <summary>Only this volume's own scene. The answer for the Hunter room, which must NOT be
            /// fitted to the map two hundred metres above it.</summary>
            Scene,

            /// <summary>Only what is selected. The answer for one wing of a building, or for a scene
            /// that holds things which are not the map.</summary>
            Selection,
        }

        void DrawFitButtons()
        {
            EditorGUILayout.LabelField("Kutuyu sigdir", EditorStyles.boldLabel);

            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Tum Sahneler"))
                    FitTargets(FitScope.AllScenes);
                if (GUILayout.Button("Bu Sahne"))
                    FitTargets(FitScope.Scene);
                if (GUILayout.Button("Secim"))
                    FitTargets(FitScope.Selection);
            }
        }

        void FitTargets(FitScope scope)
        {
            foreach (Object each in targets)
                Fit((MapBoundsVolume)each, scope);
        }

        /// <summary>Wraps the box around the geometry in scope, plus the volume's own margin.
        ///
        /// COLLIDERS, not renderers, which is the one real difference from APV's version of this button.
        /// APV fits light probes around what you can SEE; this fits a play area around what you can WALK
        /// INTO, and on a map those are not the same set - a skybox dome, a distant backdrop or a
        /// decorative billboard has no collision and would inflate the box into something useless, while
        /// a bare invisible blocker has collision and belongs inside it.
        ///
        /// Spawn markers are wrapped too. A volume that does not contain its own seeds cannot bake at
        /// all - the fill starts at the markers - so a fit that left one outside would produce a box
        /// that looks right and fails the moment you press Bake.</summary>
        static void Fit(MapBoundsVolume volume, FitScope scope)
        {
            if (!TryMeasure(volume, scope, out Bounds world, out Component[] extremes))
            {
                Debug.LogWarning($"[MapBounds] '{volume.name}' icin sigdirilacak bir sey bulunamadi. " +
                    "Collision'i olan geometri gerekiyor - Secim'de haritanin kendisini de sec.", volume);
                return;
            }

            Report(volume, world, extremes);

            Bounds local = ToLocal(volume.transform, world);

            // The margin is metres in the world and the box is in the volume's local space - on a scaled
            // object those are different units, and the padding would silently grow or shrink with it.
            float scale = Mathf.Abs(volume.transform.lossyScale.x);
            float margin = volume.FitMargin / (scale > 0.0001f ? scale : 1f);
            local.Expand(margin * 2f);

            Undo.RecordObject(volume, "Fit Map Bounds Volume");
            volume.LocalBox = local;
            EditorUtility.SetDirty(volume);
        }

        static bool TryMeasure(MapBoundsVolume volume, FitScope scope, out Bounds world,
            out Component[] extremes)
        {
            world = default;
            extremes = new Component[6];
            bool any = false;

            foreach (Collider collider in Object.FindObjectsByType<Collider>(FindObjectsInactive.Exclude,
                         FindObjectsSortMode.None))
            {
                // Enabled, not a trigger, and on a layer the bake would treat as solid. Fitting to
                // something the bake will never see is how a box ends up describing a region with
                // nothing in it - both have to be asking about the same geometry.
                if (!collider.enabled || collider.isTrigger)
                    continue;

                if ((volume.BlockingLayers.value & (1 << collider.gameObject.layer)) == 0)
                    continue;

                if (!InScope(volume, scope, collider.gameObject))
                    continue;

                // A collider with no extent is not geometry, and it does not sit quietly either: an empty
                // MeshCollider - one whose mesh reference was lost, or a marker object somebody added the
                // component to by accident - reports a zero-sized box, often at the world origin. Nothing
                // about that is visible in the scene, and encapsulating it drags the whole volume out to
                // meet a point where there is nothing at all.
                Bounds bounds = collider.bounds;
                if (bounds.size == Vector3.zero)
                    continue;

                if (collider is MeshCollider mesh && mesh.sharedMesh == null)
                    continue;

                Encapsulate(ref world, ref any, bounds, collider, extremes);
            }

            if (!any)
                return false;

            // Markers second, and only the ones already standing on the geometry. The fill starts at the
            // markers, so a volume that excludes one cannot bake - but a marker far outside the map is
            // not a reason to stretch this box to reach it. It is a marker that belongs to a DIFFERENT
            // volume, which is the whole point of there being several: the Hunter room's spawns are two
            // hundred metres down and must not drag the map's box down with them.
            Bounds geometry = world;

            foreach (MapMarker marker in Object.FindObjectsByType<MapMarker>(FindObjectsInactive.Include,
                         FindObjectsSortMode.None))
            {
                if (!InScope(volume, scope, marker.gameObject))
                    continue;

                Vector3 at = marker.transform.position;
                if (!geometry.Contains(at))
                    continue;

                Encapsulate(ref world, ref any, new Bounds(at, Vector3.one), marker, extremes);
            }

            return true;
        }

        /// <summary>Grows the box, remembering which object owns each face it pushed out.
        ///
        /// The bookkeeping is the point. A fit that comes out too big is otherwise a mystery you can only
        /// solve by deleting things one at a time - with this, the console names the six objects that
        /// decided the answer, and the one that does not belong is usually obvious on sight.</summary>
        static void Encapsulate(ref Bounds total, ref bool any, Bounds addition, Component owner,
            Component[] extremes)
        {
            if (!any)
            {
                total = addition;
                any = true;
                for (int face = 0; face < 6; face++)
                    extremes[face] = owner;
                return;
            }

            Vector3 min = total.min;
            Vector3 max = total.max;

            for (int axis = 0; axis < 3; axis++)
            {
                if (addition.min[axis] < min[axis])
                    extremes[axis * 2] = owner;
                if (addition.max[axis] > max[axis])
                    extremes[axis * 2 + 1] = owner;
            }

            total.Encapsulate(addition);
        }

        static readonly string[] FaceNames = { "-X", "+X", "-Y (alt)", "+Y (ust)", "-Z", "+Z" };

        static void Report(MapBoundsVolume volume, Bounds world, Component[] extremes)
        {
            var report = new System.Text.StringBuilder();
            report.Append($"[MapBounds] '{volume.name}' sigdirildi: ")
                  .Append($"{world.size.x:0.#} x {world.size.y:0.#} x {world.size.z:0.#} m ")
                  .Append($"(+{volume.FitMargin:0.#} m pay). Kenarlari belirleyenler:");

            for (int face = 0; face < 6; face++)
            {
                Component owner = extremes[face];
                report.Append($"\n   {FaceNames[face],-9} {(owner != null ? owner.name : "?")}");
            }

            report.Append("\nBeklenenden buyukse listedeki bir obje haritaya ait degildir - sil, ")
                  .Append("katmanini degistir, ya da Engelleyen Katmanlar maskesinden cikar.");

            Debug.Log(report.ToString(), volume);
        }

        static bool InScope(MapBoundsVolume volume, FitScope scope, GameObject candidate)
        {
            switch (scope)
            {
                case FitScope.Scene:
                    return candidate.scene == volume.gameObject.scene;

                case FitScope.Selection:
                    // Under anything selected, not merely selected: nobody clicks every collider in a
                    // building, they click the building.
                    foreach (GameObject selected in Selection.gameObjects)
                    {
                        if (candidate.transform.IsChildOf(selected.transform))
                            return true;
                    }

                    return false;

                default:
                    return true;
            }
        }

        /// <summary>A world-space box, in the volume's own space.
        ///
        /// By its eight corners rather than by transforming the centre and the size, because a rotated
        /// volume turns an axis-aligned world box into a tilted one, and the smallest local box holding
        /// that is the one around its corners. Transforming the size vector instead would shear the
        /// answer and cut the corners of the map off.</summary>
        static Bounds ToLocal(Transform space, Bounds world)
        {
            Vector3 min = world.min;
            Vector3 max = world.max;
            var local = new Bounds(space.InverseTransformPoint(min), Vector3.zero);

            for (int corner = 1; corner < 8; corner++)
            {
                var point = new Vector3(
                    (corner & 1) == 0 ? min.x : max.x,
                    (corner & 2) == 0 ? min.y : max.y,
                    (corner & 4) == 0 ? min.z : max.z);

                local.Encapsulate(space.InverseTransformPoint(point));
            }

            return local;
        }

        /// <summary>How many points the fill will start from, and a way out when the answer is none.
        ///
        /// Worth its own line because it is the first thing to check when a bake looks wrong in either
        /// direction. Zero means the fill never ran. A count far higher than the spawn points you meant
        /// usually means markers OUTSIDE the building are seeding the open air, and the whole box comes
        /// back filled - which reads as the bake ignoring the geometry entirely.</summary>
        static void DrawSeedStatus(MapBoundsVolume volume)
        {
            int count = volume.CachedSeeds.Count;

            if (count == 0)
            {
                EditorGUILayout.HelpBox(
                    "Kutunun icinde tohum noktasi yok - bake calismaz. Hacmin icine bir MapMarker koy, " +
                    "Tohum Turleri maskesini genislet, ya da Tohumlar listesine bir Transform ver.",
                    MessageType.Error);
                return;
            }

            EditorGUILayout.LabelField("Tohum noktasi",
                volume.Seeds != null && volume.Seeds.Length > 0
                    ? $"{count} (elle verilmis)"
                    : $"{count} (kutu icindeki MapMarker'lar)");
        }

        // ---------------------------------------------------------------- Status

        static void DrawStatus(MapBoundsVolume volume)
        {
            if (!volume.IsBaked)
            {
                EditorGUILayout.HelpBox(
                    "Bake edilmedi. Bake edilmemis hacim hicbir seyi sinirlamaz - hicbir hacim bake " +
                    "edilmemisse harita sinirsizdir. Kutuyu ayarla, sonra Bake'e bas.",
                    MessageType.Info);
                return;
            }

            if (volume.IsStale)
            {
                EditorGUILayout.HelpBox(
                    "Kutu bake'den sonra tasindi ya da yeniden boyutlandirildi. Asagidaki veri artik " +
                    "ekrandaki kutuyu anlatmiyor - tekrar bake et.",
                    MessageType.Warning);
            }

            Vector3Int size = volume.BakedSize;
            EditorGUILayout.LabelField("Bake tarihi", volume.BakedAt);
            EditorGUILayout.LabelField("Izgara",
                $"{size.x} x {size.y} x {size.z}  ({volume.BakedCellSize:0.##} m hucre)");
            EditorGUILayout.LabelField("Oyun alani",
                $"{volume.PlayCellCount:N0} hucre  ({volume.DataBytes / 1024f:0.#} KB)");

            if (volume.NarrowCellCount == 0)
            {
                EditorGUILayout.HelpBox("Sizinti agzi yok: dolgunun ulastigi her yere bir oyuncu da " +
                    "yuruyerek gidebiliyor.", MessageType.Info);
                return;
            }

            EditorGUILayout.HelpBox(
                $"{volume.NarrowCellCount:N0} sizinti agzi hucresi. Bunlar dolgunun bir oyuncunun " +
                "durabilecegi yerden cikip sigamayacagi bir yere girdigi noktalar - haritadan cikan " +
                "her yol birinden gecer. Sahnede kirmizi cizilirler ve genelde bir avuc tanedir; " +
                "birden fazlaysa her biri ayri bir catlaktir.",
                MessageType.Warning);
        }

        /// <summary>Where the fill got out, and a button that takes you there.
        ///
        /// The part that was still missing. Everything else said a leak EXISTS - the red cells, the count,
        /// the warning naming a face - and none of it said where, which on a house-sized map is the only
        /// half that costs any time. The route the fill took is drawn in magenta and its narrowest point
        /// is ringed; this frames the scene camera on that ring.
        ///
        /// The width is what tells you which problem you have. Below a player's 0.8 metres the fill went
        /// through something nobody can walk through, which is a crack and wants fixing. Around a
        /// doorway's width it went out of a door, and no amount of repairing geometry will change that -
        /// the answer there is a MapBlocker across the opening, or a box that stops at the wall.</summary>
        static void DrawEscape(MapBoundsVolume volume)
        {
            if (!volume.IsBaked || !volume.HasEscapePath)
                return;

            EditorGUILayout.Space();

            float width = volume.EscapeWidth;
            bool crack = width > 0f && width < 0.8f;

            EditorGUILayout.HelpBox(
                crack
                    ? $"Dolgu hacmin duvarina ulasti. En dar gectigi yer {width:0.##} m - bir oyuncu " +
                      "(0.8 m) buradan gecemez, yani geometride birlesmeyen bir yer var. Sahnede " +
                      "macenta yol ve halka orayi gosteriyor."
                    : $"Dolgu hacmin duvarina ulasti. En dar gectigi yer {width:0.##} m - bu bir " +
                      "catlak degil, gercek bir acikliktan (kapi, pencere, acik kenar) cikiyor. " +
                      "Kapatmak istiyorsan acikliga MapBlocker collider'i koy ya da kutuyu daralt.",
                crack ? MessageType.Error : MessageType.Warning);

            if (!GUILayout.Button("Sizintiya git", GUILayout.Height(24f)))
                return;

            SceneView view = SceneView.lastActiveSceneView;
            if (view == null)
                return;

            // Framed rather than teleported: dropping the camera onto the point would put it inside the
            // wall the fill went through, which is a view of nothing.
            view.Frame(new Bounds(volume.EscapePoint, Vector3.one * 6f), false);
            view.Repaint();
        }

        /// <summary>The box, dragged in the scene rather than typed.
        ///
        /// Handles.matrix set to the volume's own transform, so the handle edits the LOCAL box the data
        /// is stored in. Without that, dragging a face of a rotated volume would move it in world
        /// space and quietly shear what the numbers mean.</summary>
        void OnSceneGUI()
        {
            var volume = (MapBoundsVolume)target;
            Bounds box = volume.LocalBox;

            handle.center = box.center;
            handle.size = box.size;
            handle.SetColor(volume.IsBaked ? new Color(0.3f, 0.9f, 0.5f) : new Color(0.9f, 0.7f, 0.2f));

            using (new Handles.DrawingScope(volume.transform.localToWorldMatrix))
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                handle.DrawHandle();

                if (!check.changed)
                    return;

                Undo.RecordObject(volume, "Resize Map Bounds Volume");
                volume.LocalBox = new Bounds(handle.center, handle.size);
                EditorUtility.SetDirty(volume);
            }
        }
    }
}
