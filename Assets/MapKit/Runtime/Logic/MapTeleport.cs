using UnityEngine;

namespace Mimicraft.MapKit
{
    /// <summary>
    /// Sends players to its destination: whoever walks into its box while it is enabled, and, through the
    /// TeleportActivator input, the player who set a chain off (pressed a button, say), wherever they are.
    /// A teleport used only that way is left switched off, so its box takes nobody. The destination is any
    /// object in the map; the player lands facing the way it faces, or keeps their own facing. Raises
    /// Teleported. Hammer's trigger_teleport.
    /// </summary>
    [AddComponentMenu("Map Kit/Logic/Map Teleport")]
    public sealed class MapTeleport : MapEntity
    {
        [Tooltip("Kutunun merkezi, bu objeye göre.")]
        [SerializeField] Vector3 center = new Vector3(0f, 1f, 0f);

        [Tooltip("Kutunun boyutu (metre). Açıkken içine giren ışınlanır.")]
        [SerializeField] Vector3 size = new Vector3(1.5f, 2f, 1.5f);

        [Tooltip("Kimi ışınlar. Avcı ve Modelci ayrımı olmayan modlarda herkesi.")]
        [SerializeField] MapWho who = MapWho.Everyone;

        [Tooltip("Oyuncunun varacağı yer: haritadaki herhangi bir obje. Oyuncu onun baktığı yöne bakar.")]
        [SerializeField] Transform destination;

        [Tooltip("Açıksa oyuncu kendi baktığı yöne bakmaya devam eder.")]
        [SerializeField] bool keepFacing;

        public Vector3 Center => center;
        public Vector3 Size => size;
        public MapWho Who => who;
        public Transform Destination => destination;
        public bool KeepFacing => keepFacing;

        static readonly MapEvent[] events = { MapEvent.Teleported };
        static readonly MapInput[] inputs = { MapInput.Enable, MapInput.Disable, MapInput.Toggle, MapInput.TeleportActivator };

        public override MapEvent[] Events => events;
        public override MapInput[] Inputs => inputs;

        void OnDrawGizmos() => MapZoneGizmo.Draw(transform, center, size, new Color(0.35f, 0.75f, 1f), false);

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            MapZoneGizmo.Draw(transform, center, size, new Color(0.35f, 0.75f, 1f), true);
            if (destination == null)
                return;

            Gizmos.color = new Color(0.35f, 0.75f, 1f, 0.9f);
            Gizmos.DrawLine(transform.TransformPoint(center), destination.position + Vector3.up);
            Gizmos.DrawWireSphere(destination.position + Vector3.up * 0.9f, 0.4f);
            Gizmos.DrawRay(destination.position + Vector3.up * 0.9f, destination.forward * 1.2f);
        }
    }
}
