using UnityEngine;

namespace Mimicraft.MapKit
{
    /// <summary>
    /// A sound played where this object stands (plan §16.5): a lift's motor, a door's clang, a room's hum.
    /// The game plays it through its own source on the effects channel, so the player's effects volume
    /// governs it, and only so many map sounds play at once. A map carries the clip, never an AudioSource.
    /// Clips are held to the game's limits (32 MB in memory together, two minutes each, mono or stereo up
    /// to 48 kHz); the validator says so before packing.
    /// </summary>
    [AddComponentMenu("Map Kit/Logic/Map Sound")]
    public sealed class MapSound : MapEntity
    {
        [SerializeField] AudioClip clip;

        [Tooltip("Ses seviyesi, 0 ile 1 arası; oyuncunun efekt sesi ayarı üstüne uygulanır.")]
        [SerializeField, Range(0f, 1f)] float volume = 1f;

        [Tooltip("Durdurulana kadar tekrar eder.")]
        [SerializeField] bool loop;

        [Tooltip("Round başında kendiliğinden çalar (ortam sesi).")]
        [SerializeField] bool playOnStart;

        [Tooltip("Açıksa ses bu objenin yerinden gelir ve uzaklaştıkça kısılır; kapalıysa herkes her yerde aynı duyar.")]
        [SerializeField] bool spatial = true;

        [Tooltip("Bu uzaklığa kadar tam seviyede duyulur (metre).")]
        [SerializeField, Min(0.1f)] float minDistance = 1f;

        [Tooltip("Bu uzaklıkta duyulmaz olur (metre, en çok 100).")]
        [SerializeField, Min(1f)] float maxDistance = 25f;

        public AudioClip Clip => clip;
        public float Volume => volume;
        public bool Loop => loop;
        public bool PlayOnStart => playOnStart;
        public bool Spatial => spatial;
        public float MinDistance => minDistance;
        public float MaxDistance => maxDistance;

        static readonly MapEvent[] events = { MapEvent.Finished };

        static readonly MapInput[] inputs =
        {
            MapInput.Enable, MapInput.Disable, MapInput.Toggle, MapInput.Play, MapInput.Stop,
        };

        public override MapEvent[] Events => events;
        public override MapInput[] Inputs => inputs;

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            if (!spatial)
                return;

            Gizmos.color = new Color(0.6f, 0.5f, 1f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, minDistance);
            Gizmos.color = new Color(0.6f, 0.5f, 1f, 0.25f);
            Gizmos.DrawWireSphere(transform.position, Mathf.Min(maxDistance, 100f));
        }
    }
}
