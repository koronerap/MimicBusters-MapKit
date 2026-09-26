using UnityEngine;

namespace Mimicraft.EditorTools
{
    /// <summary>
    /// Where a map's card picture comes from: a camera pose in the scene, or a picture of the author's own.
    /// Kept in an asset beside the scene (&lt;Scene&gt;_Preview.asset, KitPreview.SettingsPath) rather than on
    /// the descriptor, for two reasons. The descriptor's fields are part of the frozen map format and go
    /// into every bundle, and this is packing-time information the game never reads. And a picture the
    /// descriptor referenced would be packed into the bundle with the scene, at full size, for nothing.
    /// </summary>
    public sealed class KitMapPreviewSettings : ScriptableObject
    {
        [Tooltip("Açıksa kart resmi aşağıdaki resimden yapılır; kapalıysa sahneden, kaydedilen kamera açısından çekilir.")]
        public bool useImage;

        [Tooltip("Kendi resmin. 16:9'a kırpılır ve 1280x720'ye ölçeklenir; dosyanın kendisi pakete girmez.")]
        public Texture2D image;

        [Tooltip("Kamera açısı kaydedildi mi. Kaydedilmemişse kit ilk doğma noktasından göz hizasında çeker.")]
        public bool hasPose;

        public Vector3 position;
        public Quaternion rotation = Quaternion.identity;

        [Range(20f, 110f)]
        public float fieldOfView = 60f;
    }
}
