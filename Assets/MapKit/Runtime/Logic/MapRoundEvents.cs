using UnityEngine;

namespace Mimicraft.MapKit
{
    /// <summary>The round, as events a map can wire: RoundStarted (every entity has just been put back as
    /// it was authored), HuntStarted (Standard's hunt; modes without preparation start hunting at once),
    /// RoundEnded. Hammer's logic_auto, with the round's other moments too.</summary>
    [AddComponentMenu("Map Kit/Logic/Map Round Events")]
    public sealed class MapRoundEvents : MapEntity
    {
        static readonly MapEvent[] events = { MapEvent.RoundStarted, MapEvent.HuntStarted, MapEvent.RoundEnded };
        static readonly MapInput[] inputs = { MapInput.Enable, MapInput.Disable, MapInput.Toggle };

        public override MapEvent[] Events => events;
        public override MapInput[] Inputs => inputs;
    }
}
