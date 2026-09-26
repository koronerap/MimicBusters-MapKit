using System.IO;
using System.Linq;
using Mimicraft.Gameplay;
using Mimicraft.MapKit;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace Mimicraft.EditorTools
{
    /// <summary>
    /// A map's card picture (plan §14.2): taken from the scene with a camera, or made from the author's own
    /// picture, and always encoded here. The author's file never goes into the package as it is: whatever
    /// it carried besides pixels (metadata, extra chunks, another format behind a .png name) stays behind,
    /// and what the game receives is the plain 8-bit PNG KitPreviewImage checks for.
    ///
    /// The pose comes from the settings beside the scene (KitMapPreviewSettings), saved from the Scene view
    /// in the descriptor's inspector. Without one the camera stands at the first spawn point at eye height,
    /// looking the way the marker faces: every map gets a picture without anybody asking for one.
    /// </summary>
    public static class KitPreview
    {
        const float EyeHeight = 1.7f;

        public static string SettingsPath(Scene scene) =>
            Path.Combine(Path.GetDirectoryName(scene.path) ?? "Assets", scene.name + "_Preview.asset").Replace('\\', '/');

        public static KitMapPreviewSettings Settings(Scene scene) =>
            string.IsNullOrEmpty(scene.path) ? null : AssetDatabase.LoadAssetAtPath<KitMapPreviewSettings>(SettingsPath(scene));

        public static KitMapPreviewSettings SettingsOrCreate(Scene scene)
        {
            KitMapPreviewSettings settings = Settings(scene);
            if (settings != null || string.IsNullOrEmpty(scene.path))
                return settings;

            settings = ScriptableObject.CreateInstance<KitMapPreviewSettings>();
            AssetDatabase.CreateAsset(settings, SettingsPath(scene));
            return settings;
        }

        /// <summary>Where the picture is taken from, for the inspector and the validator, or null when the
        /// map has nothing to take it from (no saved pose, no picture, no spawn point).</summary>
        public static string Describe(Scene scene)
        {
            KitMapPreviewSettings settings = Settings(scene);
            if (settings != null && settings.useImage)
                return settings.image != null
                    ? KitEditorLanguage.T("kendi resmin: ", "your picture: ") + settings.image.name
                    : null;
            if (settings != null && settings.hasPose)
                return KitEditorLanguage.T("kaydedilen kamera açısı", "the saved camera angle");
            return FirstSpawn(scene) != null
                ? KitEditorLanguage.T("ilk doğma noktası, göz hizası", "the first spawn point, at eye height")
                : null;
        }

        /// <summary>The picture as PNG bytes, or null (and why) when there is nothing to make it from.</summary>
        public static byte[] Capture(Scene scene, out string why)
        {
            KitMapPreviewSettings settings = Settings(scene);
            Texture2D pixels = settings != null && settings.useImage
                ? FromImage(settings.image, out why)
                : FromScene(scene, settings, out why);
            if (pixels == null)
                return null;

            try
            {
                byte[] png = pixels.EncodeToPNG();
                if (png.Length > KitPreviewImage.MaxBytes)
                {
                    // A busy picture can outgrow the limit as PNG; the same pixels at a lower resolution will not.
                    Texture2D smaller = Resize(pixels, KitPreviewImage.Width * 3 / 4, KitPreviewImage.Height * 3 / 4);
                    png = smaller.EncodeToPNG();
                    Object.DestroyImmediate(smaller);
                }

                if (!KitPreviewImage.CheckHeader(png, out _, out _, out why))
                    return null;
                return png;
            }
            finally
            {
                Object.DestroyImmediate(pixels);
            }
        }

        /// <summary>Renders the scene from the saved pose, or from the first spawn point.</summary>
        static Texture2D FromScene(Scene scene, KitMapPreviewSettings settings, out string why)
        {
            Vector3 position;
            Quaternion rotation;
            float fov = 60f;
            if (settings != null && settings.hasPose)
            {
                position = settings.position;
                rotation = settings.rotation;
                fov = settings.fieldOfView;
            }
            else
            {
                MapMarker spawn = FirstSpawn(scene);
                if (spawn == null)
                {
                    why = KitEditorLanguage.T("Önizleme için kamera açısı ya da doğma noktası yok.",
                        "No camera angle or spawn point to take the preview from.");
                    return null;
                }

                position = spawn.transform.position + Vector3.up * EyeHeight;
                Vector3 forward = Vector3.ProjectOnPlane(spawn.transform.forward, Vector3.up);
                rotation = Quaternion.LookRotation(forward.sqrMagnitude > 0.001f ? forward.normalized : Vector3.forward)
                    * Quaternion.Euler(8f, 0f, 0f);
            }

            var holder = new GameObject("Map Kit Preview Camera") { hideFlags = HideFlags.HideAndDontSave };
            RenderTexture target = null;
            try
            {
                holder.transform.SetPositionAndRotation(position, rotation);

                var camera = holder.AddComponent<Camera>();
                camera.enabled = false;
                camera.fieldOfView = fov;
                camera.nearClipPlane = 0.05f;
                camera.farClipPlane = 3000f;
                camera.clearFlags = CameraClearFlags.Skybox;

                // The game's cameras post-process; a picture without it would not look like the map.
                var data = holder.AddComponent<UniversalAdditionalCameraData>();
                data.renderPostProcessing = true;
                data.antialiasing = AntialiasingMode.SubpixelMorphologicalAntiAliasing;

                var descriptor = new RenderTextureDescriptor(KitPreviewImage.Width, KitPreviewImage.Height,
                    RenderTextureFormat.ARGB32, 24) { sRGB = true };
                target = RenderTexture.GetTemporary(descriptor);

                // A render request is how a scriptable pipeline renders one camera on demand; Camera.Render is
                // the fallback for a pipeline that does not take one.
                var request = new RenderPipeline.StandardRequest { destination = target };
                if (RenderPipeline.SupportsRenderRequest(camera, request))
                    RenderPipeline.SubmitRenderRequest(camera, request);
                else
                {
                    camera.targetTexture = target;
                    camera.Render();
                    camera.targetTexture = null;
                }

                why = "";
                return Read(target, KitPreviewImage.Width, KitPreviewImage.Height);
            }
            finally
            {
                if (target != null)
                    RenderTexture.ReleaseTemporary(target);
                Object.DestroyImmediate(holder);
            }
        }

        /// <summary>The author's picture, cropped to 16:9 around its centre and scaled to the card size.
        /// Read from the file, not from the imported texture, whose import settings may have shrunk or
        /// compressed it.</summary>
        static Texture2D FromImage(Texture2D image, out string why)
        {
            if (image == null)
            {
                why = KitEditorLanguage.T("Önizleme resmi seçilmemiş.", "No preview picture is chosen.");
                return null;
            }

            string path = AssetDatabase.GetAssetPath(image);
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                if (string.IsNullOrEmpty(path) || !File.Exists(path) || !source.LoadImage(File.ReadAllBytes(path)))
                {
                    why = KitEditorLanguage.T("Önizleme resmi okunamadı: ", "The preview picture could not be read: ") + path;
                    return null;
                }

                float want = (float)KitPreviewImage.Width / KitPreviewImage.Height;
                float have = (float)source.width / source.height;
                Vector2 scale = have > want ? new Vector2(want / have, 1f) : new Vector2(1f, have / want);
                Vector2 offset = (Vector2.one - scale) * 0.5f;

                RenderTexture target = RenderTexture.GetTemporary(KitPreviewImage.Width, KitPreviewImage.Height, 0,
                    RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
                try
                {
                    Graphics.Blit(source, target, scale, offset);
                    why = "";
                    return Read(target, KitPreviewImage.Width, KitPreviewImage.Height);
                }
                finally
                {
                    RenderTexture.ReleaseTemporary(target);
                }
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
        }

        static Texture2D Resize(Texture2D source, int width, int height)
        {
            RenderTexture target = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB);
            try
            {
                Graphics.Blit(source, target);
                return Read(target, width, height);
            }
            finally
            {
                RenderTexture.ReleaseTemporary(target);
            }
        }

        /// <summary>Opaque RGB: the card is a picture, and an alpha channel only makes the file bigger.</summary>
        static Texture2D Read(RenderTexture source, int width, int height)
        {
            RenderTexture previous = RenderTexture.active;
            RenderTexture.active = source;
            var pixels = new Texture2D(width, height, TextureFormat.RGB24, false);
            pixels.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            pixels.Apply(false);
            RenderTexture.active = previous;
            return pixels;
        }

        /// <summary>The spawn point a stranger would first stand on: a lobby point, else a Modeler's, else
        /// any.</summary>
        static MapMarker FirstSpawn(Scene scene)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return null;

            var markers = scene.GetRootGameObjects()
                .SelectMany(root => root.GetComponentsInChildren<MapMarker>(true))
                .Where(m => m.Modes != MapMarkerModes.None)
                .ToList();
            return markers.FirstOrDefault(m => m.Is(MapMarkerKind.LobbySpawn))
                ?? markers.FirstOrDefault(m => m.Is(MapMarkerKind.HiderSpawn))
                ?? markers.FirstOrDefault(m => m.Is(MapMarkerKind.HunterSpawn | MapMarkerKind.ModelerSpawn | MapMarkerKind.ArenaSpawn));
        }
    }
}
