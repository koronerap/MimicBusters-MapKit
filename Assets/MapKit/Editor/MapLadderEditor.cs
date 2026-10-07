using Mimicraft.MapKit;
using UnityEditor;
using UnityEditor.IMGUI.Controls;
using UnityEngine;

namespace Mimicraft.EditorTools
{
    /// <summary>The ladder's Inspector and its box handles: the box is resized in the Scene view the way a collider
    /// is, and the Inspector says in one line what the box and the arrow mean.</summary>
    [CustomEditor(typeof(MapLadder)), CanEditMultipleObjects]
    public sealed class MapLadderEditor : Editor
    {
        readonly BoxBoundsHandle handle = new BoxBoundsHandle();

        static string T(string tr, string en) => KitEditorLanguage.T(tr, en);

        public override void OnInspectorGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                KitEditorLanguage.Toolbar();
            }

            EditorGUILayout.HelpBox(T(
                "Kutunun içindeki oyuncu merdivene tırmanır. Mavi ok (Z) tırmananın baktığı yöndür: merdivene doğru. " +
                "Tırmanış objenin yukarısı boyuncadır; duvara yaslı merdiven için objeyi X'te, tepesi duvara doğru " +
                "yatır. Merdivene doğru ilerlemek tırmandırır; yukarı bakmak çıkarır, aşağı bakmak indirir; yan " +
                "tuşlar yana kaydırır; zıplamak bırakır. Kutunun üstü çıkılan katın zeminine kadar uzanmalı.",
                "A player inside the box climbs the ladder. The blue arrow (Z) is the way the climber faces: toward " +
                "the ladder. The climb runs along the object's up; for a ladder leaning on a wall, tip the object on " +
                "X, top toward the wall. Moving toward the ladder climbs it; looking up goes up, looking down goes " +
                "down; the side keys step along it; jump lets go. The top of the box should reach the floor it leads " +
                "to."), MessageType.None);

            DrawDefaultInspector();
        }

        void OnSceneGUI()
        {
            var ladder = (MapLadder)target;
            Bounds box = ladder.LocalBox;

            handle.center = box.center;
            handle.size = box.size;
            handle.SetColor(new Color(0.95f, 0.65f, 0.25f));

            using (new Handles.DrawingScope(ladder.transform.localToWorldMatrix))
            using (var check = new EditorGUI.ChangeCheckScope())
            {
                handle.DrawHandle();

                if (!check.changed)
                    return;

                Undo.RecordObject(ladder, "Resize Map Ladder");
                ladder.LocalBox = new Bounds(handle.center, handle.size);
                EditorUtility.SetDirty(ladder);
            }
        }
    }
}
