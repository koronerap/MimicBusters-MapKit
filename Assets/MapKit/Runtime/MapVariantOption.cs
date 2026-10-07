using UnityEngine;

namespace Mimicraft.MapKit
{
    /// <summary>
    /// How likely one version of a MapVariantGroup is to come up. Optional: a child without it weighs 1. Put on a
    /// direct child of the group.
    /// </summary>
    [AddComponentMenu("Map Kit/Map Variant Option")]
    [DisallowMultipleComponent]
    public sealed class MapVariantOption : MonoBehaviour
    {
        [Tooltip("Bu çocuğun seçilme ağırlığı. Bileşensiz çocuklar 1 sayılır: 2, ötekilerin iki katı sık gelir; " +
                 "0.5 yarısı kadar. 0 = hiç seçilmez (sahnede düzenlemek için tutulur).")]
        [SerializeField, Min(0f)] float weight = 1f;

        public float Weight => Mathf.Max(0f, weight);
    }
}
