using UnityEngine;

namespace Mimicraft.MapKit
{
    /// <summary>Passes one input on to several outputs, each with its own delay: one button, many things.
    /// Hammer's logic_relay.</summary>
    [AddComponentMenu("Map Kit/Logic/Map Relay")]
    public sealed class MapRelay : MapEntity
    {
        static readonly MapEvent[] events = { MapEvent.Triggered };
        static readonly MapInput[] inputs = { MapInput.Enable, MapInput.Disable, MapInput.Toggle, MapInput.Trigger };

        public override MapEvent[] Events => events;
        public override MapInput[] Inputs => inputs;
    }
}
