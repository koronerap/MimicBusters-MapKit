using UnityEngine;

namespace Mimicraft.MapKit
{
    /// <summary>An invisible box that notices players: Entered and Exited for each one, Occupied when the
    /// first comes in and Emptied when the last leaves. The box is in this object's space, so it turns and
    /// scales with it. Hammer's trigger_multiple.</summary>
    [AddComponentMenu("Map Kit/Logic/Map Trigger")]
    public sealed class MapTrigger : MapEntity
    {
        [Tooltip("Kutunun merkezi, bu objeye göre.")]
        [SerializeField] Vector3 center = new Vector3(0f, 1f, 0f);

        [Tooltip("Kutunun boyutu (metre).")]
        [SerializeField] Vector3 size = new Vector3(2f, 2f, 2f);

        [Tooltip("Kimi fark eder. Avcı ve Modelci ayrımı olmayan modlarda herkesi.")]
        [SerializeField] MapWho who = MapWho.Everyone;

        public Vector3 Center => center;
        public Vector3 Size => size;
        public MapWho Who => who;

        static readonly MapEvent[] events = { MapEvent.Entered, MapEvent.Exited, MapEvent.Occupied, MapEvent.Emptied };
        static readonly MapInput[] inputs = { MapInput.Enable, MapInput.Disable, MapInput.Toggle };

        public override MapEvent[] Events => events;
        public override MapInput[] Inputs => inputs;

        void OnDrawGizmos() => MapZoneGizmo.Draw(transform, center, size, new Color(0.4f, 0.9f, 0.4f), false);

        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();
            MapZoneGizmo.Draw(transform, center, size, new Color(0.4f, 0.9f, 0.4f), true);
        }
    }

    /// <summary>A hurt zone and a trigger draw their box the same way.</summary>
    static class MapZoneGizmo
    {
        public static void Draw(Transform transform, Vector3 center, Vector3 size, Color color, bool selected)
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            color.a = selected ? 0.25f : 0.08f;
            Gizmos.color = color;
            Gizmos.DrawCube(center, size);
            color.a = selected ? 1f : 0.5f;
            Gizmos.color = color;
            Gizmos.DrawWireCube(center, size);
            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
