using UnityEngine;

namespace Mimicraft.MapKit
{
    /// <summary>Shows and hides this object and its children, what is drawn and what is solid: a wall that
    /// opens, a bridge that appears. Enable shows, Disable hides; Start Enabled is whether it is there when
    /// the round starts. Other map entities under it keep working. Hammer's func_brush.</summary>
    [AddComponentMenu("Map Kit/Logic/Map Toggle")]
    public sealed class MapToggle : MapEntity
    {
        static readonly MapEvent[] events = { };
        static readonly MapInput[] inputs = { MapInput.Enable, MapInput.Disable, MapInput.Toggle };

        public override MapEvent[] Events => events;
        public override MapInput[] Inputs => inputs;
    }
}
