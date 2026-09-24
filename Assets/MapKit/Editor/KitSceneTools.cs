using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;

namespace Mimicraft.EditorTools
{
    /// <summary>Small repairs the validator points at, and the kit's way to the map bounds bake.</summary>
    public static class KitSceneTools
    {
        /// <summary>The same bake as the game's Mimicraft > Setup menu and the volume's own Inspector
        /// button, where an author looks for it: every MapBoundsVolume in the open scenes.</summary>
        [MenuItem("Map Kit/Bake Map Bounds", priority = 5)]
        static void BakeBounds() => MapBoundsBaker.BakeOpenScenes();

        /// <summary>Cuts a copied scene loose from the original's baked lighting.
        ///
        /// A duplicated scene keeps pointing at the original's LightingData.asset, and Generate Lighting
        /// then writes into THAT file, over the original's light (measured on the HQ spike). With the
        /// reference cleared, the next bake makes the scene a file of its own, in its own folder. Nothing
        /// is deleted: the original keeps its lighting.</summary>
        [MenuItem("Map Kit/Tools/Detach Copied Lighting", priority = 60)]
        static void DetachCopiedLighting()
        {
            Scene scene = SceneManager.GetActiveScene();
            string foreign = KitMapValidator.ForeignLightingData(scene);
            if (foreign == null)
            {
                EditorUtility.DisplayDialog("Map Kit", KitEditorLanguage.T(
                    "Bu sahnenin ışık verisi kendisine ait ya da henüz yok. Ayrılacak bir şey yok.",
                    "This scene's lighting data is its own, or there is none yet. Nothing to detach."), KitEditorLanguage.T("Tamam", "OK"));
                return;
            }

            if (!EditorUtility.DisplayDialog("Map Kit", string.Format(KitEditorLanguage.T(
                        "Sahne şu ışık verisini kullanıyor:\n{0}\n\nBağlantı kesilecek; o dosyaya ve sahibine dokunulmaz. Sonra Generate Lighting bu sahneye kendi verisini yapar.",
                        "The scene uses this lighting data:\n{0}\n\nThe link will be cut; that file and its scene are left alone. Generate Lighting then makes this scene its own."), foreign),
                    KitEditorLanguage.T("Ayır", "Detach"), KitEditorLanguage.T("İptal", "Cancel")))
                return;

            Lightmapping.lightingDataAsset = null;
            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            if (KitMapValidator.HasForeignProbeData(scene))
                EditorUtility.DisplayDialog("Map Kit", KitEditorLanguage.T(
                    "Işık verisi ayrıldı. Probe verisi de başka bir sahnenin Baking Set'inde: Lighting penceresinin Adaptive Probe Volumes bölümünde bu sahne için yeni bir Baking Set oluştur, sonra Generate Lighting.",
                    "The lighting data is detached. The probe data is in another scene's Baking Set too: make a new Baking Set for this scene under Adaptive Probe Volumes in the Lighting window, then Generate Lighting."),
                    KitEditorLanguage.T("Tamam", "OK"));
        }
    }
}
