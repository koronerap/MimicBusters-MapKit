using System.Collections.Generic;
using UnityEngine;

namespace Mimicraft.MapKit
{
    /// <summary>
    /// A ladder: a box players climb while they are in it. Hammer's func_ladder, and climbed the way Half-Life and
    /// Counter-Strike climb one: pushing toward the ladder climbs it, looking up or down while doing so decides which
    /// way, the side keys step along it, jump lets go, and stepping back at the bottom walks off it. The game does
    /// the climbing (PlayerMovement); this only says where and which way.
    ///
    /// The box is the space a climbing player stands in - the rungs and the air in front of them - and its blue arrow
    /// (this object's Z) is the way the climber faces: toward the ladder. The climb runs along this object's up, so
    /// a ladder leaning against a wall is placed by tipping the object over on X, top toward the wall. A player is on
    /// the ladder while any part of them is in the box, so its top should reach the floor it leads to, a little above
    /// it if anything; a box that stops short leaves the climber hanging under the lip.
    ///
    /// The ladder model itself is the map's own business and may be solid; players do not need to touch it.
    /// </summary>
    [AddComponentMenu("Map Kit/Map Ladder")]
    [DisallowMultipleComponent]
    public sealed class MapLadder : MonoBehaviour
    {
        [Tooltip("Tırmanma kutusunun merkezi, bu objeye göre. Kutu merdiveni ve önündeki, tırmanan oyuncunun " +
                 "durduğu boşluğu kapsar. Mavi ok (Z) tırmananın baktığı yöndür: merdivene doğru.")]
        [SerializeField] Vector3 center = new Vector3(0f, 1.5f, 0.3f);

        [Tooltip("Tırmanma kutusunun boyutu (metre). Üstü çıkılan katın zeminine kadar, hatta biraz yukarısına " +
                 "uzanmalı; oyuncu kutudan tamamen çıkınca merdivenden çıkmış olur. Sahnede tutamaçlarla da " +
                 "boyutlandırılır.")]
        [SerializeField] Vector3 size = new Vector3(0.9f, 3f, 0.8f);

        static readonly List<MapLadder> active = new List<MapLadder>();

        /// <summary>Every ladder in play. Domain reload is off in this project, so the list is cleared with each
        /// session rather than trusted to start empty.</summary>
        public static IReadOnlyList<MapLadder> Active => active;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics() => active.Clear();

        // OnEnable rather than Awake, so switching the object off takes the ladder away with it - the one handle
        // map logic (MapToggle) has on it.
        void OnEnable() => active.Add(this);

        void OnDisable() => active.Remove(this);

        public Vector3 Center => center;
        public Vector3 Size => size;

        /// <summary>The authored box in local space, for the editor's handles.</summary>
        public Bounds LocalBox
        {
            get => new Bounds(center, size);
            set
            {
                center = value.center;
                size = value.size;
            }
        }

        /// <summary>The way the climber faces, level: toward the ladder. Falls back to the object's up when its Z
        /// points straight up or down, which has no level direction to take.</summary>
        public Vector3 Facing
        {
            get
            {
                Vector3 level = Vector3.ProjectOnPlane(transform.forward, Vector3.up);
                if (level.sqrMagnitude < 0.0001f)
                    level = Vector3.ProjectOnPlane(transform.up, Vector3.up);
                return level.sqrMagnitude < 0.0001f ? Vector3.forward : level.normalized;
            }
        }

        /// <summary>Which way climbing goes: this object's up, so a leaning ladder is climbed along its lean. Never
        /// downward, whatever the object's turn.</summary>
        public Vector3 Axis
        {
            get
            {
                Vector3 up = transform.up;
                return up.y > 0.1f ? up.normalized : Vector3.up;
            }
        }

        static readonly Color LadderColor = new Color(0.95f, 0.65f, 0.25f);

        void OnDrawGizmos() => DrawGizmo(false);

        void OnDrawGizmosSelected() => DrawGizmo(true);

        /// <summary>The box, the rungs on its far face (the ladder, where the arrow points), and an arrow from the
        /// near face along the climber's facing.</summary>
        void DrawGizmo(bool selected)
        {
            MapZoneGizmo.Draw(transform, center, size, LadderColor, selected);

            Gizmos.matrix = transform.localToWorldMatrix;
            Color line = LadderColor;
            line.a = selected ? 1f : 0.6f;
            Gizmos.color = line;

            Vector3 half = size * 0.5f;
            float ladder = center.z + half.z;
            float left = center.x - half.x * 0.7f;
            float right = center.x + half.x * 0.7f;
            float bottom = center.y - half.y;
            float top = center.y + half.y;

            Gizmos.DrawLine(new Vector3(left, bottom, ladder), new Vector3(left, top, ladder));
            Gizmos.DrawLine(new Vector3(right, bottom, ladder), new Vector3(right, top, ladder));
            for (float y = bottom + 0.3f; y < top; y += 0.3f)
                Gizmos.DrawLine(new Vector3(left, y, ladder), new Vector3(right, y, ladder));

            // The climber's facing, from where they stand to the rungs, at chest height.
            Vector3 stand = new Vector3(center.x, bottom + Mathf.Min(1.2f, size.y * 0.5f), center.z - half.z);
            Gizmos.DrawLine(stand, stand + Vector3.forward * Mathf.Max(0.4f, size.z));

            Gizmos.matrix = Matrix4x4.identity;
        }
    }
}
