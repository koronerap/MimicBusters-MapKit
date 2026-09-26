using System.Collections.Generic;
using Mimicraft.MapKit;
using UnityEditor;
using UnityEngine;

namespace Mimicraft.EditorTools
{
    /// <summary>
    /// A mover's inspector is every entity's (MapEntityEditor), plus two things for its path. In the Scene
    /// view each stop gets a handle to drag it by and its number; the component's gizmo draws the path and
    /// the mover's outline at every stop. And a preview: a see-through copy of the mover along its path,
    /// scrubbed with a slider or played the way the game plays it (Once: out, the Return After wait, back;
    /// PingPong and Loop: their cycle, waits included), so an author sees the timing without packing the map.
    ///
    /// The preview's timing is worked out here the way the game's MapLogic.BuildPath and PathTimeAt work it
    /// out (legs at the mover's speed and turning speed, eased when Smooth), and only drawn: the mover itself
    /// never moves in the editor, so nothing is saved that the author did not change.
    /// </summary>
    [CustomEditor(typeof(MapMover)), CanEditMultipleObjects]
    public sealed class MapMoverEditor : MapEntityEditor
    {
        // The game's limits (MapLogic.MaxStops, MaxTravel), which this assembly cannot see.
        const int MaxStops = 32;
        const float MaxTravel = 1000f;

        static bool showGhost;
        static bool playing;
        static float scrub;
        static double playStarted;
        static Material ghostMaterial;

        static string T(string tr, string en) => KitEditorLanguage.T(tr, en);

        void OnDisable()
        {
            playing = false;
            EditorApplication.update -= RepaintWhilePlaying;
        }

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            if (targets.Length > 1)
                return;

            var mover = (MapMover)target;
            Path path = Build(mover);
            float end = path.Cum[path.Cum.Length - 1];

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(T("Önizleme", "Preview"), EditorStyles.boldLabel);
            string timing = path.Cyclic
                ? string.Format(T("Bir tur {0:0.0} sn, beklemeler dahil.", "One round takes {0:0.0} s, waits included."), path.CycleLength)
                : string.Format(T("Son durağa {0:0.0} sn; geri dönüşü de öyle.", "{0:0.0} s to the last stop, and as long back."), end);
            EditorGUILayout.LabelField(timing, EditorStyles.miniLabel);

            EditorGUI.BeginChangeCheck();
            showGhost = EditorGUILayout.ToggleLeft(T("Sahnede hayaletini göster", "Show its ghost in the Scene view"), showGhost);
            using (new EditorGUI.DisabledScope(!showGhost))
            {
                using (new EditorGUI.DisabledScope(playing))
                    scrub = EditorGUILayout.Slider(T("Yolda", "Along the path"), Mathf.Min(scrub, end), 0f, end);

                using (new EditorGUILayout.HorizontalScope())
                {
                    if (GUILayout.Button(playing ? T("Durdur", "Stop") : T("Oynat", "Play"), GUILayout.Width(90)))
                    {
                        playing = !playing;
                        playStarted = EditorApplication.timeSinceStartup;
                        EditorApplication.update -= RepaintWhilePlaying;
                        if (playing)
                            EditorApplication.update += RepaintWhilePlaying;
                    }

                    string how = path.Cyclic ? T("oyundaki döngüsü", "its cycle, as in the game") : T("git, bekle, dön", "out, wait, back");
                    GUILayout.Label(playing ? how : "", EditorStyles.miniLabel);
                }
            }
            if (EditorGUI.EndChangeCheck())
                SceneView.RepaintAll();
        }

        static void RepaintWhilePlaying() => SceneView.RepaintAll();

        protected override void OnSceneGUI()
        {
            base.OnSceneGUI();

            var mover = (MapMover)target;
            Transform t = mover.transform;
            Transform parent = t.parent;

            serializedObject.Update();
            SerializedProperty stops = serializedObject.FindProperty("stops");
            if (stops == null)
                return;

            Quaternion axes = Tools.pivotRotation == PivotRotation.Local && parent != null ? parent.rotation : Quaternion.identity;
            for (int i = 0; i < stops.arraySize; i++)
            {
                SerializedProperty position = stops.GetArrayElementAtIndex(i).FindPropertyRelative("position");
                Vector3 local = t.localPosition + position.vector3Value;
                Vector3 world = parent != null ? parent.TransformPoint(local) : local;

                Handles.Label(world + Vector3.up * 0.3f, (i + 1).ToString(), EditorStyles.boldLabel);
                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.PositionHandle(world, axes);
                if (EditorGUI.EndChangeCheck())
                {
                    Vector3 movedLocal = parent != null ? parent.InverseTransformPoint(moved) : moved;
                    position.vector3Value = movedLocal - t.localPosition;
                }
            }

            serializedObject.ApplyModifiedProperties();

            if (showGhost && Event.current.type == EventType.Repaint)
                DrawGhost(mover);
        }

        // ------------------------------------------------------------------ the path, as the game builds it

        struct Segment
        {
            public float Duration;
            public float FromT;
            public float ToT;
        }

        sealed class Path
        {
            public Vector3[] Pos;
            public Quaternion[] Rot;
            public float[] Cum;
            public int LastStop;
            public bool Cyclic;
            public Segment[] Cycle;
            public float CycleLength;
        }

        static Path Build(MapMover mover)
        {
            MapMoverStop[] stops = mover.Stops;
            int count = Mathf.Min(stops.Length, MaxStops);
            bool loop = mover.LoopMode == MapMoverLoop.Loop;
            int points = count + 1 + (loop && count > 0 ? 1 : 0);

            var path = new Path
            {
                Pos = new Vector3[points],
                Rot = new Quaternion[points],
                Cum = new float[points],
                LastStop = count,
            };

            Transform t = mover.transform;
            path.Pos[0] = t.localPosition;
            path.Rot[0] = t.localRotation;
            for (int i = 1; i <= count; i++)
            {
                MapMoverStop stop = stops[i - 1] ?? new MapMoverStop();
                path.Pos[i] = t.localPosition + Clamp(stop.position, MaxTravel);
                path.Rot[i] = t.localRotation * Quaternion.Euler(Clamp(stop.rotation, 3600f));
            }
            if (points > count + 1)
            {
                path.Pos[points - 1] = path.Pos[0];
                path.Rot[points - 1] = path.Rot[0];
            }

            float speed = Mathf.Clamp(mover.Speed, 0.05f, 30f);
            float turn = Mathf.Clamp(mover.AngularSpeed, 1f, 720f);
            for (int i = 1; i < points; i++)
            {
                float distance = Vector3.Distance(path.Pos[i - 1], path.Pos[i]) / speed;
                float angle = Quaternion.Angle(path.Rot[i - 1], path.Rot[i]) / turn;
                path.Cum[i] = path.Cum[i - 1] + Mathf.Max(0.05f, Mathf.Max(distance, angle));
            }

            // PingPong and Loop: the legs and waits they repeat.
            var waits = new float[count + 1];
            waits[0] = Mathf.Clamp(mover.WaitAtStart, 0f, 3600f);
            for (int i = 1; i <= count; i++)
                waits[i] = Mathf.Clamp(stops[i - 1] != null ? stops[i - 1].wait : 0f, 0f, 3600f);

            var cycle = new List<Segment>();
            void Leg(int from, int to) => cycle.Add(new Segment
            {
                Duration = Mathf.Abs(path.Cum[to] - path.Cum[from]), FromT = path.Cum[from], ToT = path.Cum[to],
            });
            void Wait(int stop)
            {
                if (waits[stop] > 0f)
                    cycle.Add(new Segment { Duration = waits[stop], FromT = path.Cum[stop], ToT = path.Cum[stop] });
            }

            if (count > 0 && mover.LoopMode == MapMoverLoop.PingPong)
            {
                for (int i = 0; i < count; i++)
                {
                    Leg(i, i + 1);
                    Wait(i + 1);
                }
                for (int i = count; i > 0; i--)
                {
                    Leg(i, i - 1);
                    Wait(i - 1);
                }
            }
            else if (count > 0 && loop)
            {
                for (int i = 0; i < count; i++)
                {
                    Leg(i, i + 1);
                    Wait(i + 1);
                }
                Leg(count, count + 1);
                Wait(0);
            }

            path.Cycle = cycle.ToArray();
            foreach (Segment segment in path.Cycle)
                path.CycleLength += segment.Duration;
            path.Cyclic = path.CycleLength > 0f;
            return path;
        }

        static Vector3 Clamp(Vector3 value, float limit) => new Vector3(
            Mathf.Clamp(value.x, -limit, limit), Mathf.Clamp(value.y, -limit, limit), Mathf.Clamp(value.z, -limit, limit));

        static float Ease(float u) => u * u * (3f - 2f * u);

        static void PoseAt(Path path, float t, out Vector3 position, out Quaternion rotation)
        {
            float[] cum = path.Cum;
            int last = cum.Length - 1;
            if (last <= 0 || t <= 0f)
            {
                position = path.Pos[0];
                rotation = path.Rot[0];
                return;
            }
            if (t >= cum[last])
            {
                position = path.Pos[last];
                rotation = path.Rot[last];
                return;
            }

            int i = 0;
            while (i < last - 1 && cum[i + 1] < t)
                i++;
            float u = (t - cum[i]) / Mathf.Max(1e-5f, cum[i + 1] - cum[i]);
            position = Vector3.Lerp(path.Pos[i], path.Pos[i + 1], u);
            rotation = Quaternion.Slerp(path.Rot[i], path.Rot[i + 1], u);
        }

        /// <summary>The path time the played preview is at. A cycle runs as the game runs it, each leg eased
        /// on its own. Once goes out to the last stop, waits (Return After, or a second when it never comes
        /// back by itself), comes back and rests a second; each journey eased as a whole, like a button's.
        /// </summary>
        static float PlayedTime(MapMover mover, Path path)
        {
            double elapsed = EditorApplication.timeSinceStartup - playStarted;

            if (path.Cyclic)
            {
                double tau = elapsed % path.CycleLength;
                for (int i = 0; i < path.Cycle.Length; i++)
                {
                    Segment segment = path.Cycle[i];
                    if (tau <= segment.Duration || i == path.Cycle.Length - 1)
                    {
                        float u = segment.Duration > 0f ? Mathf.Clamp01((float)(tau / segment.Duration)) : 1f;
                        if (mover.Smooth && segment.FromT != segment.ToT)
                            u = Ease(u);
                        return Mathf.Lerp(segment.FromT, segment.ToT, u);
                    }
                    tau -= segment.Duration;
                }
                return 0f;
            }

            float end = path.Cum[path.LastStop];
            if (end <= 0f)
                return 0f;

            float wait = mover.ReturnAfter >= 0f ? Mathf.Min(mover.ReturnAfter, 10f) : 1f;
            float t = (float)(elapsed % (end * 2f + wait + 1f));
            float Journey(float v) => mover.Smooth ? Ease(v) : v;

            if (t < end)
                return end * Journey(t / end);
            if (t < end + wait)
                return end;
            if (t < end * 2f + wait)
                return end * (1f - Journey((t - end - wait) / end));
            return 0f;
        }

        static void DrawGhost(MapMover mover)
        {
            Path path = Build(mover);
            float t = playing ? PlayedTime(mover, path) : scrub;
            PoseAt(path, t, out Vector3 position, out Quaternion rotation);

            Transform moverTransform = mover.transform;
            Matrix4x4 parent = moverTransform.parent != null ? moverTransform.parent.localToWorldMatrix : Matrix4x4.identity;
            Matrix4x4 ghostWorld = parent * Matrix4x4.TRS(position, rotation, moverTransform.localScale);
            Matrix4x4 fromMover = ghostWorld * moverTransform.worldToLocalMatrix;

            Material material = GhostMaterial();
            material.SetPass(0);
            foreach (MeshFilter filter in mover.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null)
                    continue;
                Matrix4x4 matrix = fromMover * filter.transform.localToWorldMatrix;
                for (int sub = 0; sub < mesh.subMeshCount; sub++)
                    Graphics.DrawMeshNow(mesh, matrix, sub);
            }
        }

        /// <summary>See-through, unlit, both faces: the editor's own coloured-line shader.</summary>
        static Material GhostMaterial()
        {
            if (ghostMaterial != null)
                return ghostMaterial;

            ghostMaterial = new Material(Shader.Find("Hidden/Internal-Colored")) { hideFlags = HideFlags.HideAndDontSave };
            ghostMaterial.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            ghostMaterial.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            ghostMaterial.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
            ghostMaterial.SetInt("_ZWrite", 0);
            ghostMaterial.SetColor("_Color", new Color(0.3f, 0.85f, 1f, 0.35f));
            return ghostMaterial;
        }
    }
}
