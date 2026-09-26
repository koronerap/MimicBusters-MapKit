using System;
using System.Collections.Generic;
using System.Linq;
using Mimicraft.MapKit;
using UnityEditor;
using UnityEngine;

namespace Mimicraft.EditorTools
{
    /// <summary>
    /// The inspector every map entity shares (plan §16.7): what it is, its own settings, then its outputs
    /// as Hammer lays them out (when this happens, to that entity, do this, after so long) and, underneath,
    /// who sends it inputs. Only the events this kind of entity raises and the inputs the chosen target
    /// takes are offered, and a connection that cannot work is marked where it is written.
    /// </summary>
    [CustomEditor(typeof(MapEntity), true), CanEditMultipleObjects]
    public class MapEntityEditor : Editor
    {
        SerializedProperty outputs;
        GUIStyle warning;

        static string T(string tr, string en) => KitEditorLanguage.T(tr, en);

        protected void OnEnable() => outputs = serializedObject.FindProperty("outputs");

        public override void OnInspectorGUI()
        {
            serializedObject.Update();
            warning ??= new GUIStyle(EditorStyles.miniLabel) { normal = { textColor = new Color(0.95f, 0.55f, 0.15f) }, wordWrap = true };
            var entity = (MapEntity)target;

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Label(entity.EntityId > 0 ? "#" + entity.EntityId : T("numara kaydedince verilir", "numbered on save"), EditorStyles.miniLabel);
                GUILayout.FlexibleSpace();
                KitEditorLanguage.Toolbar();
            }
            EditorGUILayout.LabelField(MapLogicGuide.DescribeEntity(entity), EditorStyles.wordWrappedMiniLabel);
            EditorGUILayout.Space(2);

            DrawPropertiesExcluding(serializedObject, "m_Script", "entityId", "outputs");

            if (targets.Length > 1)
            {
                EditorGUILayout.HelpBox(T("Çıkışlar tek bir varlık seçiliyken düzenlenir.", "Outputs are edited with one entity selected."), MessageType.None);
                serializedObject.ApplyModifiedProperties();
                return;
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(T("Çıkışlar", "Outputs"), EditorStyles.boldLabel);
            if (entity.Events.Length == 0)
            {
                EditorGUILayout.LabelField(T("Bu varlık olay vermez; yalnızca giriş alır.", "This entity raises no events; it only takes inputs."),
                    EditorStyles.wordWrappedMiniLabel);
            }
            else
            {
                for (int i = 0; i < outputs.arraySize; i++)
                {
                    if (DrawOutput(entity, outputs.GetArrayElementAtIndex(i)))
                    {
                        outputs.DeleteArrayElementAtIndex(i);
                        break;
                    }
                }

                if (outputs.arraySize > MapLogic_MaxOutputs)
                    EditorGUILayout.LabelField(string.Format(T("Oyun ilk {0} çıkışı çalıştırır, fazlası yok sayılır.",
                        "The game runs the first {0} outputs and ignores the rest."), MapLogic_MaxOutputs), warning);

                if (GUILayout.Button(T("+ Çıkış ekle", "+ Add output")))
                {
                    outputs.arraySize++;
                    SerializedProperty added = outputs.GetArrayElementAtIndex(outputs.arraySize - 1);
                    added.FindPropertyRelative("onEvent").intValue = (int)entity.Events[0];
                    added.FindPropertyRelative("target").objectReferenceValue = null;
                    added.FindPropertyRelative("input").intValue = (int)MapInput.Enable;
                    added.FindPropertyRelative("delay").floatValue = 0f;
                    added.FindPropertyRelative("parameter").floatValue = 0f;
                    added.FindPropertyRelative("once").boolValue = false;
                }
            }

            DrawIncoming(entity);
            serializedObject.ApplyModifiedProperties();
        }

        /// <summary>Each output's event and input written on its line in the Scene view; the component's own
        /// gizmo draws the lines. Several outputs to one target stack their labels rather than overprint.</summary>
        protected virtual void OnSceneGUI()
        {
            var entity = (MapEntity)target;
            if (entity == null)
                return;

            var stacked = new Dictionary<MapEntity, int>();
            foreach (MapOutput output in entity.Outputs)
            {
                if (output == null || output.target == null || output.target == entity)
                    continue;

                stacked.TryGetValue(output.target, out int row);
                stacked[output.target] = row + 1;

                Vector3 middle = Vector3.Lerp(entity.transform.position, output.target.transform.position, 0.5f);
                string text = $"{output.onEvent} → {output.input}" + (output.delay > 0f ? $" +{output.delay:0.##} s" : "");
                Handles.Label(middle + Vector3.up * (0.3f * row), text, EditorStyles.whiteMiniLabel);
            }
        }

        // The game's limit (MapLogic.MaxOutputsPerEntity), which this assembly cannot see.
        const int MapLogic_MaxOutputs = 16;

        /// <summary>One output. True when its remove button was pressed.</summary>
        bool DrawOutput(MapEntity entity, SerializedProperty output)
        {
            SerializedProperty onEvent = output.FindPropertyRelative("onEvent");
            SerializedProperty targetProperty = output.FindPropertyRelative("target");
            SerializedProperty input = output.FindPropertyRelative("input");
            SerializedProperty delay = output.FindPropertyRelative("delay");
            SerializedProperty parameter = output.FindPropertyRelative("parameter");
            SerializedProperty once = output.FindPropertyRelative("once");

            bool remove = false;
            using (new EditorGUILayout.VerticalScope(EditorStyles.helpBox))
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    onEvent.intValue = (int)Popup(entity.Events, (MapEvent)onEvent.intValue, GUILayout.Width(150));
                    GUILayout.Label("→", GUILayout.Width(14));
                    targetProperty.objectReferenceValue = EditorGUILayout.ObjectField(targetProperty.objectReferenceValue, typeof(MapEntity), true);
                    if (GUILayout.Button("✕", EditorStyles.miniButton, GUILayout.Width(22)))
                        remove = true;
                }

                var targetEntity = targetProperty.objectReferenceValue as MapEntity;
                IReadOnlyList<MapInput> inputs = targetEntity != null ? targetEntity.Inputs : MapLogicGuide.AllInputs;
                using (new EditorGUILayout.HorizontalScope())
                {
                    input.intValue = (int)Popup(inputs, (MapInput)input.intValue, GUILayout.Width(150));
                    GUILayout.Label(T("gecikme", "delay"), EditorStyles.miniLabel, GUILayout.Width(46));
                    delay.floatValue = Mathf.Max(0f, EditorGUILayout.FloatField(delay.floatValue, GUILayout.Width(50)));
                    GUILayout.Label(T("sn", "s"), EditorStyles.miniLabel, GUILayout.Width(16));
                    once.boolValue = EditorGUILayout.ToggleLeft(T("bir kez", "once"), once.boolValue, GUILayout.Width(70));
                }

                string parameterLabel = MapLogicGuide.ParameterLabel((MapInput)input.intValue);
                if (parameterLabel != null)
                    parameter.floatValue = EditorGUILayout.FloatField(parameterLabel, parameter.floatValue);

                string targetName = targetEntity != null ? targetEntity.name : "?";
                EditorGUILayout.LabelField($"{MapLogicGuide.DescribeEvent((MapEvent)onEvent.intValue)} → {targetName}: "
                    + MapLogicGuide.DescribeInput((MapInput)input.intValue), EditorStyles.wordWrappedMiniLabel);

                string problem = Problem(entity, (MapEvent)onEvent.intValue, targetEntity, (MapInput)input.intValue);
                if (problem != null)
                    EditorGUILayout.LabelField(problem, warning);
            }

            return remove;
        }

        static string Problem(MapEntity entity, MapEvent onEvent, MapEntity target, MapInput input)
        {
            if (!entity.Raises(onEvent))
                return T("Bu varlık bu olayı vermez.", "This entity never raises this event.");
            if (target == null)
                return T("Hedef seçilmedi: bu çıkış hiçbir şey yapmaz.", "No target: this output does nothing.");
            if (target.gameObject.scene != entity.gameObject.scene)
                return T("Hedef başka bir sahnede; oyun yok sayar.", "The target is in another scene; the game ignores it.");
            if (!target.Takes(input))
                return string.Format(T("{0} bu girişi almaz.", "{0} does not take this input."), target.GetType().Name);
            return null;
        }

        static TEnum Popup<TEnum>(IReadOnlyList<TEnum> options, TEnum current, params GUILayoutOption[] layout) where TEnum : struct, Enum
        {
            var list = options.ToList();
            bool foreign = !list.Contains(current);
            if (foreign)
                list.Insert(0, current);

            string[] names = list.Select(o => foreign && o.Equals(current) ? o + " (?)" : o.ToString()).ToArray();
            int index = EditorGUILayout.Popup(list.IndexOf(current), names, layout);
            return index >= 0 && index < list.Count ? list[index] : current;
        }

        /// <summary>Who wires into this entity: so a platform shows which button drives it.</summary>
        static void DrawIncoming(MapEntity entity)
        {
            var lines = new List<(MapEntity from, string text)>();
            foreach (GameObject root in entity.gameObject.scene.GetRootGameObjects())
            {
                foreach (MapEntity source in root.GetComponentsInChildren<MapEntity>(true))
                {
                    foreach (MapOutput output in source.Outputs)
                    {
                        if (output != null && output.target == entity)
                            lines.Add((source, $"{source.name}: {output.onEvent} → {output.input}"
                                + (output.delay > 0f ? $" (+{output.delay:0.##} s)" : "")));
                    }
                }
            }

            if (lines.Count == 0)
                return;

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField(T("Bu varlığa gelenler", "Wired into this entity"), EditorStyles.boldLabel);
            foreach (var (from, text) in lines)
            {
                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUILayout.LabelField(text, EditorStyles.miniLabel);
                    if (GUILayout.Button(T("Seç", "Select"), EditorStyles.miniButton, GUILayout.Width(44)))
                        Selection.activeObject = from;
                }
            }
        }
    }
}
