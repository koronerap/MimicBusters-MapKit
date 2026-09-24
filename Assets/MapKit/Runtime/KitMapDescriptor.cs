using UnityEngine;
using Mimicraft.Gameplay;

namespace Mimicraft.MapKit
{
    /// <summary>
    /// The one component every kit map carries: what the map is called, who made it, which modes it is
    /// for, and the environment its scene was baked with.
    ///
    /// Passive by rule, like everything a bundle may construct: no Awake, no OnEnable, nothing that runs.
    /// The game reads it after the scene has passed its audit. A component that did work on its own
    /// would do it with the author's data before anyone had looked at that data.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class KitMapDescriptor : MonoBehaviour
    {
        [Tooltip("Haritanın oyunda görünen adı. Boş bırakılırsa sahnenin adı kullanılır.")]
        [SerializeField] string mapName = "";

        [Tooltip("Harita kartında görünen kısa açıklama.")]
        [SerializeField, TextArea(2, 4)] string description = "";

        [Tooltip("Yapımcının görünen adı. Steam adından bağımsızdır, boş bırakılabilir.")]
        [SerializeField] string author = "";

        [Tooltip("Haritanın oynanabildiği modlar. Her mod için gereken işaretçiler Doğrula'da kontrol edilir.")]
        [SerializeField] MapMarkerModes modes = MapMarkerModes.Standard | MapMarkerModes.Practice;

        [Tooltip("Paketleme anında sahnenin ışık ayarlarından doldurulur; elle düzenlenmez.")]
        [SerializeField, HideInInspector] KitEnvironment environment = new KitEnvironment();

        public string MapName => mapName;
        public string Description => description;
        public string Author => author;
        public MapMarkerModes Modes => modes;
        public KitEnvironment Environment => environment;

#if UNITY_EDITOR
        /// <summary>For the packer: replaces the captured environment with the open scene's.</summary>
        public void EditorSetEnvironment(KitEnvironment captured) => environment = captured;
#endif
    }
}
