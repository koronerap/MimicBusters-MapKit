using UnityEngine;

namespace Mimicraft.MapKit
{
    /// <summary>Counts, and raises ReachedTarget when the count arrives at the target: three buttons pressed,
    /// then the door. Add and Subtract take their amount from the output's parameter (1 when it is 0),
    /// SetValue the value itself. Hammer's math_counter.</summary>
    [AddComponentMenu("Map Kit/Logic/Map Counter")]
    public sealed class MapCounter : MapEntity
    {
        [Tooltip("Round başındaki değer.")]
        [SerializeField] int startValue;

        [Tooltip("Sayı bu değere varınca ReachedTarget olayı verir.")]
        [SerializeField] int target = 3;

        public int StartValue => startValue;
        public int Target => target;

        static readonly MapEvent[] events = { MapEvent.ReachedTarget };

        static readonly MapInput[] inputs =
        {
            MapInput.Enable, MapInput.Disable, MapInput.Toggle,
            MapInput.Add, MapInput.Subtract, MapInput.SetValue, MapInput.ResetCount,
        };

        public override MapEvent[] Events => events;
        public override MapInput[] Inputs => inputs;
    }
}
