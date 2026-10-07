using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mimicraft.EditorTools
{
    /// <summary>
    /// Map Kit ▸ Game Look Preview: the game's own post-processing in the editor, so a map is lit for what players will
    /// see. The game puts a global Volume over every map from its own shell scene - exposure +0.5, Neutral tonemapping,
    /// contrast, saturation, bloom (KitGameLook.asset, a copy of the game's Settings/SampleSceneProfile.asset) - and a
    /// map judged without it looks about half a stop darker in the kit than in the game. Lit up to look right in the
    /// kit, it came out washed out in the game.
    ///
    /// A hidden object that is never saved: it is not in the scene file, the validator does not see it, and nothing of
    /// it is packed. Lowest priority, so a Volume the map carries itself still has the last word. On by default; the
    /// menu item switches it.
    /// </summary>
    [InitializeOnLoad]
    public static class KitGameLook
    {
        const string Pref = "MimicBustersKit.GameLookPreview";
        const string MenuPath = "Map Kit/Game Look Preview";
        const string ObjectName = "Kit Game Look Preview (not saved)";

        static KitGameLook()
        {
            // After the editor has finished loading: assets and scenes are not ready inside a static constructor.
            EditorApplication.delayCall += Apply;
            UnityEditor.SceneManagement.EditorSceneManager.sceneOpened += (_, __) => Apply();
        }

        static bool Enabled => EditorPrefs.GetBool(Pref, true) && !InGameProject;

        /// <summary>The kit's folder is also part of the game's own project, which has the real Volumes in its scenes -
        /// a menu with a look of its own among them. A preview there would add the map look to scenes that have none
        /// and change how the game's editor shows them; so it is the kit's alone.</summary>
        static bool InGameProject => System.Type.GetType("Mimicraft.Networking.GameModeController, Assembly-CSharp") != null;

        [MenuItem(MenuPath, priority = 7)]
        static void Toggle()
        {
            EditorPrefs.SetBool(Pref, !Enabled);
            Apply();
        }

        [MenuItem(MenuPath, true)]
        static bool ToggleValidate()
        {
            Menu.SetChecked(MenuPath, Enabled);
            return !InGameProject;
        }

        static void Apply()
        {
            Volume existing = Find();
            if (!Enabled)
            {
                if (existing != null)
                    Object.DestroyImmediate(existing.gameObject);
                return;
            }

            if (existing != null)
                return;

            VolumeProfile profile = LoadProfile();
            if (profile == null)
                return;

            var go = new GameObject(ObjectName) { hideFlags = HideFlags.HideAndDontSave };
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = -1000f;
            volume.sharedProfile = profile;
        }

        static Volume Find()
        {
            foreach (Volume volume in Resources.FindObjectsOfTypeAll<Volume>())
            {
                if (volume != null && volume.gameObject.name == ObjectName
                    && (volume.gameObject.hideFlags & HideFlags.DontSave) != 0)
                    return volume;
            }

            return null;
        }

        static VolumeProfile LoadProfile()
        {
            foreach (string guid in AssetDatabase.FindAssets("KitGameLook t:VolumeProfile"))
            {
                var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(AssetDatabase.GUIDToAssetPath(guid));
                if (profile != null)
                    return profile;
            }

            return null;
        }
    }
}
