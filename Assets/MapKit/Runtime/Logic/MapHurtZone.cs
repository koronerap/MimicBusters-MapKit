using UnityEngine;

namespace Mimicraft.MapKit
{
    /// <summary>
    /// Hurts whoever is inside its box, every interval, while enabled: lava, a pit, a crusher's floor. A
    /// death here is the map's, like one to a Hunter's own missed shots: nobody scores it (plan §16.6). The
    /// box is in this object's space. Hammer's trigger_hurt.
    /// </summary>
    [AddComponentMenu("Map Kit/Logic/Map Hurt Zone")]
    public sealed class MapHurtZone : MapEntity
    {
        [Tooltip("Kutunun merkezi, bu objeye göre.")]
        [SerializeField] Vector3 center = new Vector3(0f, 0.5f, 0f);

        [Tooltip("Kutunun boyutu (metre).")]
        [SerializeField] Vector3 size = new Vector3(4f, 1f, 4f);

        [Tooltip("Kime hasar verir. Avcı ve Modelci ayrımı olmayan modlarda herkese.")]
        [SerializeField] MapWho who = MapWho.Everyone;

        [Tooltip("Her aralıkta verdiği hasar.")]
        [SerializeField, Min(1)] int damage = 10;

        [Tooltip("Kaç saniyede bir (en az 0,1).")]
        [SerializeField, Min(0.1f)] float interval = 1f;

        [Tooltip("İçine gireni anında öldürür (lav, çukur).")]
        [SerializeField] bool instantKill;

        public Vector3 Center => center;
        public Vector3 Size => size;
        public MapWho Who => who;
        public int Damage => damage;
        public float Interval => interval;
        public bool InstantKill => instantKill;

        static readonly MapEvent[] events = { MapEvent.Hurt, MapEvent.Killed };
        // HurtActivator works while the zone is off: a zone used only to hurt whoever pressed a button is
        // switched off so it hurts nobody standing in it.
        static readonly MapInput[] inputs = { MapInput.Enable, MapInput.Disable, MapInput.Toggle, MapInput.HurtActivator };

        public override MapEvent[] Events => events;
        public override MapInput[] Inputs => inputs;

        void OnDrawGizmos() => MapZoneGizmo.Draw(transform, center, size, new Color(1f, 0.3f, 0.2f), false);

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            MapZoneGizmo.Draw(transform, center, size, new Color(1f, 0.3f, 0.2f), true);
        }
    }
}
