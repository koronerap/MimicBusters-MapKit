using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace Mimicraft.Gameplay
{
    /// <summary>
    /// A box that says which Adaptive Probe Volume baking set lights the part of the world inside it.
    ///
    /// It exists because only ONE baking set can be active at a time. That is not a misconfiguration to
    /// work around, it is what the engine does - the core package says so where SetActiveBakingSet is
    /// declared: "can be used when loading additively two scenes belonging to different baking sets to
    /// control which one is active". So a map and a Hunter room baked into separate sets cannot both be
    /// lit at once, however they are configured.
    ///
    /// What makes that survivable here is the shape of this game: a player is either on the map or in the
    /// room, never looking at both. Lighting is a property of what a camera can see, and each client only
    /// has one camera - so each client only ever needs one set, and can be given the right one.
    ///
    /// A box of its own rather than a field on MapBoundsVolume, even though the two describe overlapping
    /// regions. Bounds are a rule about where a player may BE and lighting is about what a camera can
    /// SEE; tying them together would mean a room could not be lit until somebody had baked its play
    /// area, which are two unrelated jobs on two different days.
    /// </summary>
    public class ProbeVolumeRegion : MonoBehaviour
    {
        [Tooltip("Bu bölgeyi aydınlatan APV Baking Set. Sahne bu set ile bakelenmiş olmalı.")]
        [SerializeField] ProbeVolumeBakingSet bakingSet;

        [Tooltip("Bölgenin kutusu - bu objenin yerel uzayında. Kamerası burada olan oyuncu yukarıdaki " +
                 "seti kullanır.")]
        [SerializeField] Vector3 center;

        [SerializeField] Vector3 size = new Vector3(64f, 32f, 64f);

        [Tooltip("Birden fazla bölge çakışırsa YÜKSEK olan kazanır. Avcı odası haritanın içine " +
                 "gömülüyse odaya daha yüksek bir değer ver.")]
        [SerializeField] int priority;

        static readonly List<ProbeVolumeRegion> active = new List<ProbeVolumeRegion>();

        /// <summary>Every region currently loaded.</summary>
        public static IReadOnlyList<ProbeVolumeRegion> Active => active;

        /// <summary>Domain reload is off in this project, so a static list survives leaving Play mode and
        /// the next session starts holding regions from the last one.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => active.Clear();

        /// <summary>Raised by every region as it enables; the game's ProbeVolumeRegionSwitcher listens and
        /// makes sure it exists. This component lives in Mimicraft.MapKit, shared with the map kit, and
        /// cannot name game code. Not cleared in ResetStatics, for the same reason as
        /// MapBoundsVolume.Registered.</summary>
        public static event System.Action Registered;

        public ProbeVolumeBakingSet BakingSet => bakingSet;
        public int Priority => priority;

        void OnEnable()
        {
            active.Add(this);

            // From here rather than from a scene object, because regions arrive from different scenes -
            // the map's and the room's - and neither is reliably first.
            Registered?.Invoke();
        }

        void OnDisable() => active.Remove(this);

        public bool Contains(Vector3 worldPoint) =>
            new Bounds(center, size).Contains(transform.InverseTransformPoint(worldPoint));

        /// <summary>The region a point is in, highest priority first. Null when it is in none, which the
        /// switcher reads as "leave the lighting alone" rather than as "unlight everything".</summary>
        public static ProbeVolumeRegion For(Vector3 worldPoint)
        {
            ProbeVolumeRegion best = null;

            foreach (ProbeVolumeRegion region in active)
            {
                if (region == null || region.bakingSet == null || !region.Contains(worldPoint))
                    continue;

                if (best == null || region.priority > best.priority)
                    best = region;
            }

            return best;
        }

        void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = bakingSet != null
                ? new Color(0.4f, 0.7f, 1f, 0.8f)
                : new Color(0.9f, 0.5f, 0.2f, 0.8f);
            Gizmos.DrawWireCube(center, size);
        }
    }
}
