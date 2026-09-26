using Mimicraft.MapKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Mimicraft.EditorTools
{
    /// <summary>
    /// The descriptor's inspector: its own fields as before, then the card picture (plan §14.2). The
    /// picture's settings live beside the scene, not on the descriptor (KitMapPreviewSettings says why),
    /// so this is where an author finds them anyway: on the one object that is the map's identity.
    /// </summary>
    [CustomEditor(typeof(KitMapDescriptor))]
    public sealed class KitMapDescriptorEditor : Editor
    {
        static bool showPreview = true;

        // The last picture rendered here, and what to say about it (its size, or why there is none).
        Texture2D shown;
        string shownNote = "";

        static string T(string tr, string en) => KitEditorLanguage.T(tr, en);

        public override void OnInspectorGUI()
        {
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                KitEditorLanguage.Toolbar();
            }

            DrawDefaultInspector();

            EditorGUILayout.Space(8);
            showPreview = EditorGUILayout.Foldout(showPreview, T("Önizleme resmi (harita kartı)", "Preview picture (the map's card)"), true);
            if (showPreview)
            {
                using (new EditorGUI.IndentLevelScope())
                    DrawPreview(((KitMapDescriptor)target).gameObject.scene);
            }
        }

        void DrawPreview(Scene scene)
        {
            if (string.IsNullOrEmpty(scene.path))
            {
                EditorGUILayout.HelpBox(T("Önce sahneyi kaydet.", "Save the scene first."), MessageType.Info);
                return;
            }

            KitMapPreviewSettings settings = KitPreview.Settings(scene);
            string source = KitPreview.Describe(scene);
            EditorGUILayout.LabelField(T("Kaynak", "Source"), source ?? T("yok, kart düz renk görünür", "none, the card shows a flat colour"));

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(EditorGUI.indentLevel * 15f);
                if (GUILayout.Button(new GUIContent(T("Sahne görünümünden al", "Take from the Scene view"),
                        T("Scene view'daki kamera açısını kaydeder; paketlerken resim buradan çekilir.",
                            "Saves the Scene view's camera angle; the picture is taken from it when packing."))))
                    TakeFromSceneView(scene);

                using (new EditorGUI.DisabledScope(settings == null || !settings.hasPose))
                {
                    if (GUILayout.Button(T("Açıyı unut", "Forget the angle")))
                    {
                        Undo.RecordObject(settings, "Preview angle");
                        settings.hasPose = false;
                        Save(settings);
                    }
                }
            }

            bool useImage = settings != null && settings.useImage;
            Texture2D image = settings != null ? settings.image : null;
            EditorGUI.BeginChangeCheck();
            useImage = EditorGUILayout.ToggleLeft(T("Kendi resmimi kullan", "Use my own picture"), useImage);
            using (new EditorGUI.DisabledScope(!useImage))
                image = (Texture2D)EditorGUILayout.ObjectField(T("Resim", "Picture"), image, typeof(Texture2D), false);
            if (EditorGUI.EndChangeCheck())
            {
                settings = KitPreview.SettingsOrCreate(scene);
                Undo.RecordObject(settings, "Preview picture");
                settings.useImage = useImage;
                settings.image = image;
                Save(settings);
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.Space(EditorGUI.indentLevel * 15f);
                if (GUILayout.Button(T("Önizlemeyi göster", "Show the preview")))
                    Render(scene);
            }

            if (shown != null)
            {
                float width = EditorGUIUtility.currentViewWidth - 40f;
                Rect rect = GUILayoutUtility.GetRect(width, width * KitPreviewImage.Height / KitPreviewImage.Width);
                EditorGUI.DrawPreviewTexture(rect, shown, null, ScaleMode.ScaleToFit);
            }

            if (shownNote.Length > 0)
                EditorGUILayout.LabelField(shownNote, EditorStyles.wordWrappedMiniLabel);
        }

        void TakeFromSceneView(Scene scene)
        {
            SceneView view = SceneView.lastActiveSceneView;
            if (view == null || view.camera == null)
            {
                shownNote = T("Açık bir Scene view yok.", "There is no open Scene view.");
                return;
            }

            KitMapPreviewSettings settings = KitPreview.SettingsOrCreate(scene);
            Undo.RecordObject(settings, "Preview angle");
            settings.hasPose = true;
            settings.useImage = false;
            settings.position = view.camera.transform.position;
            settings.rotation = view.camera.transform.rotation;
            settings.fieldOfView = view.camera.fieldOfView;
            Save(settings);
            Render(scene);
        }

        void Render(Scene scene)
        {
            Clear();
            byte[] png = KitPreview.Capture(scene, out string why);
            if (png == null)
            {
                shownNote = why;
                return;
            }

            shown = new Texture2D(2, 2) { hideFlags = HideFlags.HideAndDontSave };
            shown.LoadImage(png);
            shownNote = string.Format(T("{0}x{1}, {2} KB. Paketlerken yeniden çekilir.", "{0}x{1}, {2} KB. Taken again when packing."),
                shown.width, shown.height, png.Length / 1024);
        }

        static void Save(KitMapPreviewSettings settings)
        {
            EditorUtility.SetDirty(settings);
            AssetDatabase.SaveAssetIfDirty(settings);
        }

        void Clear()
        {
            if (shown != null)
                DestroyImmediate(shown);
            shown = null;
            shownNote = "";
        }

        void OnDisable() => Clear();
    }
}
