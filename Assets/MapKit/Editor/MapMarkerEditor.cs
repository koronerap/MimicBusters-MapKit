using System.Collections.Generic;
using Mimicraft.Gameplay;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Mimicraft.EditorTools
{
    /// <summary>
    /// The MapMarker inspector: modes first, because which kinds mean anything depends on them (the
    /// field's own tooltip always said so), then the kinds those modes read, then what the two add up to.
    ///
    /// The kinds list follows the modes. With only Deathmatch ticked it offers HiderSpawn, HunterSpawn and
    /// LobbySpawn, the kinds Deathmatch reads, and folds the rest away under one line that can be opened.
    /// A ticked kind is never folded away, read or not: a box nobody can see that still holds its bit
    /// would make the marker do something invisible (under Deathmatch alone a ticked HunterDoor gets its
    /// object switched off as a door). It stays in the list, marked as unread by the ticked modes.
    ///
    /// The modes' boxes are MarkerMaskDrawer's, so that field looks the same everywhere else. The kinds
    /// are drawn here, because only here are the marker's modes known; everywhere else (the bounds
    /// volume's seed kinds) they keep the plain drawer.
    ///
    /// A Modeler Spawn gets a third section, the Gartic modeller's area (MapMarker.HasModelArea): a box set
    /// like a BoxCollider's, by its fields or by dragging its faces in the Scene view, and a padding handle
    /// that lifts where the modeller stands off the floor of the box.
    /// </summary>
    [CustomEditor(typeof(MapMarker)), CanEditMultipleObjects]
    public sealed class MapMarkerEditor : Editor
    {
        SerializedProperty modes;
        SerializedProperty kinds;
        SerializedProperty modelArea;
        SerializedProperty modelAreaCenter;
        SerializedProperty modelAreaSize;
        SerializedProperty modelAreaPadding;
        bool showUnread;
        GUIStyle unreadNote;

        readonly BoxBoundsHandle areaHandle = new BoxBoundsHandle();

        static string T(string tr, string en) => KitEditorLanguage.T(tr, en);

        void OnEnable()
        {
            modes = serializedObject.FindProperty("modes");
            kinds = serializedObject.FindProperty("kinds");
            modelArea = serializedObject.FindProperty("modelArea");
            modelAreaCenter = serializedObject.FindProperty("modelAreaCenter");
            modelAreaSize = serializedObject.FindProperty("modelAreaSize");
            modelAreaPadding = serializedObject.FindProperty("modelAreaPadding");

            // Open by default: they are all this inspector is for.
            modes.isExpanded = true;
            kinds.isExpanded = true;
        }

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            unreadNote ??= new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.95f, 0.6f, 0.15f) }, wordWrap = true };

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                KitEditorLanguage.Toolbar();
            }

            EditorGUILayout.PropertyField(modes, new GUIContent(T("1. Modlar", "1. Modes"),
                T("Bu işaretçiyi hangi modlar kullanır. Hiçbiri seçili değilse hiçbir mod kullanmaz.",
                    "Which modes use this marker. With none ticked, no mode does.")));
            EditorGUILayout.Space(2);

            bool mixed = modes.hasMultipleDifferentValues || kinds.hasMultipleDifferentValues;
            if (mixed)
                EditorGUILayout.PropertyField(kinds, new GUIContent(T("2. Türler", "2. Kinds")));
            else
                DrawKinds((MapMarkerModes)modes.enumValueFlag);

            if (!kinds.hasMultipleDifferentValues && (kinds.enumValueFlag & (int)MapMarkerKind.ModelerSpawn) != 0)
                DrawModelArea();

            serializedObject.ApplyModifiedProperties();

            if (mixed)
                return;

            EditorGUILayout.Space(4);
            DrawVerdict((MapMarkerModes)modes.enumValueFlag, (MapMarkerKind)kinds.enumValueFlag);
        }

        /// <summary>The Gartic modeller's area: on or off, and when on the box and its padding, with a word on how to
        /// drag them.</summary>
        void DrawModelArea()
        {
            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(T("3. Modelci alanı (Taklidi Tahmin Et)", "3. Modeller's area (Guess the Mimic)"), EditorStyles.boldLabel);

            using (new EditorGUI.IndentLevelScope())
            {
                EditorGUILayout.PropertyField(modelArea, new GUIContent(T("Alanı kullan", "Use the area"),
                    T("Açıkken Taklidi Tahmin Et'te modelcinin modeli bu kutunun içinde kalmak zorunda ve modelci kutunun " +
                      "tabanının ortasında doğar. Kapalıyken modelci işaretçide doğar, alan sınırı yok.",
                      "When on, the Guess the Mimic modeller's model has to stay inside this box and the modeller stands at " +
                      "the middle of its floor. When off, the modeller stands on the marker with no area limit.")));

                if (!modelArea.boolValue && !modelArea.hasMultipleDifferentValues)
                    return;

                EditorGUILayout.PropertyField(modelAreaCenter, new GUIContent(T("Merkez", "Center"), modelAreaCenter.tooltip));
                EditorGUILayout.PropertyField(modelAreaSize, new GUIContent(T("Boyut", "Size"), modelAreaSize.tooltip));
                EditorGUILayout.PropertyField(modelAreaPadding, new GUIContent(T("Tabandan yukarı (padding)", "Up from the floor (padding)"),
                    modelAreaPadding.tooltip));

                EditorGUILayout.HelpBox(T("Sahnede kutunun yüzlerindeki noktaları sürükleyerek boyutlandır. İçteki " +
                    "yukarı ok, modelcinin doğduğu yüksekliği (padding) ayarlar. Model kutunun tabanına oturur, " +
                    "ortasına değil.",
                    "Drag the dots on the box's faces in the Scene view to size it. The arrow inside sets how high the " +
                    "modeller stands (padding). The model rests on the floor of the box, not in its middle."), MessageType.None);
            }
        }

        /// <summary>The area's handles, like a BoxCollider's: a dot on each face to drag it, and an arrow at the
        /// modeller's spot for the padding. In the marker's own space, so they turn and scale with it.</summary>
        void OnSceneGUI()
        {
            var marker = (MapMarker)target;
            if (marker == null || !marker.HasModelArea)
                return;

            Color color = new Color(0.95f, 0.6f, 0.95f, 1f);
            using (new Handles.DrawingScope(color, marker.transform.localToWorldMatrix))
            {
                areaHandle.center = marker.ModelAreaCenter;
                areaHandle.size = marker.ModelAreaSize;
                areaHandle.SetColor(color);

                EditorGUI.BeginChangeCheck();
                areaHandle.DrawHandle();
                if (EditorGUI.EndChangeCheck())
                {
                    var single = new SerializedObject(marker);
                    single.FindProperty("modelAreaCenter").vector3Value = areaHandle.center;
                    single.FindProperty("modelAreaSize").vector3Value =
                        Vector3.Max(areaHandle.size, new Vector3(0.1f, 0.1f, 0.1f));
                    single.ApplyModifiedProperties();
                }

                Vector3 floor = marker.ModelAreaCenter - new Vector3(0f, marker.ModelAreaSize.y * 0.5f, 0f);
                Vector3 stand = marker.ModelerStandPointLocal;
                float size = HandleUtility.GetHandleSize(stand) * 0.6f;

                EditorGUI.BeginChangeCheck();
                Vector3 moved = Handles.Slider(stand, Vector3.up, size, Handles.ArrowHandleCap, 0f);
                if (EditorGUI.EndChangeCheck())
                {
                    var single = new SerializedObject(marker);
                    single.FindProperty("modelAreaPadding").floatValue =
                        Mathf.Clamp(moved.y - floor.y, 0f, marker.ModelAreaSize.y);
                    single.ApplyModifiedProperties();
                }

                Handles.Label(stand + Vector3.up * size * 1.15f,
                    string.Format(T("modelci  (padding {0:0.##})", "modeller  (padding {0:0.##})"), marker.ModelAreaPadding));
            }
        }

        void DrawKinds(MapMarkerModes modeMask)
        {
            int value = kinds.enumValueFlag;
            kinds.isExpanded = EditorGUILayout.Foldout(kinds.isExpanded,
                $"{T("2. Türler", "2. Kinds")}   ({MapMarkerGuide.Summary(value, MapMarkerGuide.Kinds)})", true);
            if (!kinds.isExpanded)
                return;

            bool anyMode = modeMask != MapMarkerModes.None;
            int read = anyMode ? (int)MapMarkerGuide.Reads(modeMask) : ~0;

            using (new EditorGUI.IndentLevelScope())
            {
                if (!anyMode)
                    EditorGUILayout.LabelField(T("Önce modları seç: türler seçtiğin modların okuduklarına göre listelenir.",
                        "Pick the modes first: the kinds are listed by what the modes you pick read."), EditorStyles.wordWrappedMiniLabel);

                var unread = new List<MapMarkerGuide.Entry>();
                foreach (MapMarkerGuide.Entry entry in MapMarkerGuide.Kinds)
                {
                    bool on = (value & entry.Bit) != 0;
                    bool isRead = (read & entry.Bit) != 0;
                    if (!isRead && !on)
                    {
                        unread.Add(entry);
                        continue;
                    }
                    value = KindRow(entry, value, isRead);
                }

                if (unread.Count > 0)
                {
                    showUnread = EditorGUILayout.Foldout(showUnread,
                        string.Format(T("Bu modların okumadığı türler ({0})", "Kinds these modes do not read ({0})"), unread.Count), true);
                    if (showUnread)
                    {
                        using (new EditorGUI.IndentLevelScope())
                        {
                            foreach (MapMarkerGuide.Entry entry in unread)
                                value = KindRow(entry, value, false);
                        }
                    }
                }

                if (GUILayout.Button(T("Türleri temizle", "Clear the kinds"), EditorStyles.miniButton, GUILayout.Width(120)))
                    value = 0;
            }

            if (value != kinds.enumValueFlag)
                kinds.enumValueFlag = value;
        }

        /// <summary>One kind's box and what it is for; for a kind the ticked modes do not read, the note
        /// says that first, in a warning colour.</summary>
        int KindRow(MapMarkerGuide.Entry entry, int value, bool read)
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                bool on = EditorGUILayout.ToggleLeft(new GUIContent(entry.Name, entry.Description), (value & entry.Bit) != 0,
                    GUILayout.Width(170));
                bool ticked = (value & entry.Bit) != 0;
                if (read || !ticked)
                    EditorGUILayout.LabelField(entry.Description, EditorStyles.miniLabel);
                else
                    EditorGUILayout.LabelField(T("Seçili modlar bunu okumaz. ", "The ticked modes do not read this. ") + entry.Description, unreadNote);
                return on ? value | entry.Bit : value & ~entry.Bit;
            }
        }

        static void DrawVerdict(MapMarkerModes modeMask, MapMarkerKind kindMask)
        {
            if (modeMask == MapMarkerModes.None)
            {
                EditorGUILayout.HelpBox(T("Hiçbir mod seçili değil: bu işaretçiyi oyunda hiçbir mod kullanmaz.",
                    "No mode is ticked: no mode in the game uses this marker."), MessageType.Warning);
                return;
            }

            if (kindMask == MapMarkerKind.None)
            {
                EditorGUILayout.HelpBox(T("Hiçbir tür seçili değil: bu nokta hiçbir şey için kullanılmaz.",
                    "No kind is ticked: this point is used for nothing."), MessageType.Warning);
                return;
            }

            var lines = new List<string>();
            foreach (MapMarkerGuide.Entry mode in MapMarkerGuide.Modes)
            {
                if (((int)modeMask & mode.Bit) == 0)
                    continue;
                var used = (int)(kindMask & MapMarkerGuide.Reads((MapMarkerModes)mode.Bit));
                lines.Add(used == 0
                    ? string.Format(T("{0}: bu türlerin hiçbirini okumaz", "{0}: reads none of these kinds"), mode.Name)
                    : $"{mode.Name}: {string.Join(", ", MapMarkerGuide.NamesIn(used, MapMarkerGuide.Kinds))}");
            }
            EditorGUILayout.HelpBox(T("Oyunda kim, ne olarak kullanır:\n", "Who uses it in the game, and as what:\n")
                + string.Join("\n", lines), MessageType.Info);
        }
    }
}
