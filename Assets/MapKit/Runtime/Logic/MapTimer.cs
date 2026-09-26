using UnityEngine;

namespace Mimicraft.MapKit
{
    /// <summary>Raises Elapsed every interval while enabled; with a random extra, somewhere between interval
    /// and interval + extra. Enable starts (or restarts) it, Disable stops it. Hammer's logic_timer.</summary>
    [AddComponentMenu("Map Kit/Logic/Map Timer")]
    public sealed class MapTimer : MapEntity
    {
        [Tooltip("Kaç saniyede bir (en az 0,1).")]
        [SerializeField, Min(0.1f)] float interval = 5f;

        [Tooltip("Her seferinde aralığa eklenen 0 ile bu kadar saniye arası rastgele süre.")]
        [SerializeField, Min(0f)] float randomExtra;

        public float Interval => interval;
        public float RandomExtra => randomExtra;

        static readonly MapEvent[] events = { MapEvent.Elapsed };
        static readonly MapInput[] inputs = { MapInput.Enable, MapInput.Disable, MapInput.Toggle };

        public override MapEvent[] Events => events;
        public override MapInput[] Inputs => inputs;
    }
}
