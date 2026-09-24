using UnityEngine;

namespace Mimicraft.Gameplay
{
    /// <summary>
    /// Switches a map object on only in the game modes it belongs to.
    ///
    /// The same idea as MapMarker one step along: that says which modes a POINT is for, this says which
    /// modes an OBJECT is for. Both exist because one map serves every mode, and what a mode needs from
    /// the geometry differs - Gartic wants a barrier around the table so guessers cannot walk in front
    /// of the model, and Standard wants that barrier gone because it is a wall in the middle of a hiding
    /// map.
    ///
    /// Deliberately not a marker KIND. A marker is a point somebody reads a position off; this is a
    /// thing that is either there or not, and the difference matters in the Inspector: you put this on
    /// the collider itself, where anybody looking at that collider can see why it sometimes is not
    /// there.
    ///
    /// Works on anything - a collider, a sign, a whole subtree, a light. The object is what the author
    /// chose it to be; this only decides whether it exists this round.
    ///
    /// Applied by MapLoader once the map is in and the mode is known. It runs on clients too: a barrier
    /// nobody but the server knows about is a barrier players walk through on their own screen and get
    /// shoved back out of.
    /// </summary>
    [DisallowMultipleComponent]
    public class MapModeObject : MonoBehaviour
    {
        [Tooltip("Bu obje hangi modlarda AÇIK olsun. Birden fazlası seçilebilir. " +
                 "Hiçbiri seçili değilse obje hiçbir modda görünmez - bir şeyi geçici olarak devre " +
                 "dışı bırakmanın yolu bu. Everything = her modda açık, yani bu bileşen hiçbir şey " +
                 "yapmaz.")]
        [SerializeField] MapMarkerModes modes = (MapMarkerModes)~0;

        [Tooltip("İşaretliyse obje seçili modlarda KAPALI olur - kural tersine döner. " +
                 "'Gartic hariç her yerde olsun' demek, üç modu tek tek seçmekten kısa ve bir mod " +
                 "eklendiğinde kendiliğinden doğru kalır.")]
        [SerializeField] bool invert;

        /// <summary>Whether this object should exist in the given mode.</summary>
        public bool AppliesTo(MapMarkerModes mode)
        {
            bool listed = (modes & mode) != 0;
            return invert ? !listed : listed;
        }

        /// <summary>Switches every one of these under `root` to match the mode.
        ///
        /// includeInactive, which is the point of doing it this way: an object authored switched OFF is
        /// how you keep a Gartic-only barrier out of the way in the scene view, and it has to be able to
        /// come back ON for its own mode. Reading only active objects would make that a one-way trip.</summary>
        public static int ApplyAll(GameObject root, MapMarkerModes mode)
        {
            if (root == null)
                return 0;

            int changed = 0;

            foreach (MapModeObject item in root.GetComponentsInChildren<MapModeObject>(true))
            {
                bool wanted = item.AppliesTo(mode);
                if (item.gameObject.activeSelf == wanted)
                    continue;

                item.gameObject.SetActive(wanted);
                changed++;
            }

            return changed;
        }

        /// <summary>The same, for a map that arrived as an additively loaded scene rather than a prefab.
        /// A scene has no single root, so every root object is walked.</summary>
        public static int ApplyAll(UnityEngine.SceneManagement.Scene scene, MapMarkerModes mode)
        {
            if (!scene.IsValid() || !scene.isLoaded)
                return 0;

            int changed = 0;

            foreach (GameObject root in scene.GetRootGameObjects())
                changed += ApplyAll(root, mode);

            return changed;
        }
    }
}
