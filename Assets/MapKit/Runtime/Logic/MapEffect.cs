using UnityEngine;

namespace Mimicraft.MapKit
{
    /// <summary>The game's own effects a map may show. Part of the map format: never renumber.</summary>
    public enum MapEffectPreset
    {
        Smoke = 0,
        Steam = 1,
        Fire = 2,
        Sparks = 3,
        Dust = 4,
        Drips = 5,
    }

    /// <summary>
    /// One of the game's own particle effects where this object stands (plan §16.5): a map picks which, and
    /// its colour, size and amount, within limits. A map never brings a ParticleSystem: the component can
    /// do nearly anything (sub-emitters, collisions, particles without number), and a flashing one is a
    /// visual safety problem. Enabled, it emits continuously; Play adds one burst, the way sparks fly when
    /// a button is pressed.
    /// </summary>
    [AddComponentMenu("Map Kit/Logic/Map Effect")]
    public sealed class MapEffect : MapEntity
    {
        [SerializeField] MapEffectPreset preset = MapEffectPreset.Smoke;

        [Tooltip("Renk; efektin kendi renginin üstüne çarpılır.")]
        [SerializeField] Color tint = Color.white;

        [Tooltip("Parçacıkların boyu, 0,25 ile 4 kat arası.")]
        [SerializeField, Range(0.25f, 4f)] float size = 1f;

        [Tooltip("Yoğunluk, 0 ile 4 kat arası.")]
        [SerializeField, Range(0f, 4f)] float amount = 1f;

        public MapEffectPreset Preset => preset;
        public Color Tint => tint;
        public float Size => size;
        public float Amount => amount;

        static readonly MapEvent[] events = { };
        static readonly MapInput[] inputs = { MapInput.Enable, MapInput.Disable, MapInput.Toggle, MapInput.Play };

        public override MapEvent[] Events => events;
        public override MapInput[] Inputs => inputs;

        void OnDrawGizmos()
        {
            Gizmos.color = new Color(1f, 0.6f, 0.2f, 0.7f);
            Gizmos.DrawWireSphere(transform.position, 0.2f * size);
            Gizmos.DrawLine(transform.position, transform.position + transform.up * 0.6f * size);
        }
    }
}
