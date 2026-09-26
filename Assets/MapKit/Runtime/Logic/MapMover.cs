using System;
using UnityEngine;

namespace Mimicraft.MapKit
{
    /// <summary>How a mover goes on after reaching its last stop.</summary>
    public enum MapMoverLoop
    {
        /// <summary>Goes where it is sent and stays: a door, a lift, a drawbridge.</summary>
        Once = 0,

        /// <summary>To the end and back, for as long as it runs: a ferry.</summary>
        PingPong = 1,

        /// <summary>Round its stops and from the last straight back to the first: a carousel.</summary>
        Loop = 2,
    }

    /// <summary>What a mover does when a player is in its way and cannot be pushed aside.</summary>
    public enum MapMoverBlocked
    {
        /// <summary>Turns back, the way a lift door reopens.</summary>
        Reverse = 0,

        /// <summary>Carries on and hurts whoever is caught.</summary>
        Crush = 1,

        /// <summary>Carries on through them.</summary>
        Ignore = 2,
    }

    /// <summary>One place a mover can be, relative to where it stands in the scene: its start.</summary>
    [Serializable]
    public sealed class MapMoverStop
    {
        [Tooltip("Başlangıca göre konum (metre), üst objenin ekseninde.")]
        public Vector3 position = new Vector3(0f, 3f, 0f);

        [Tooltip("Başlangıca göre dönüş (derece).")]
        public Vector3 rotation;

        [Tooltip("Gidip gelirken ya da dönerken bu durakta kaç saniye bekler.")]
        [Min(0f)] public float wait;
    }

    /// <summary>
    /// Moves and turns this object, children included, along a list of stops (plan §16.2). Where it
    /// stands in the scene is its start; each stop is relative to that. Players standing on it or clinging
    /// to it are carried along; one it runs into is pushed, and when that is not possible, the mover does
    /// what When Blocked says.
    ///
    /// Forward sends it to the last stop, Back to the start, GoToStop to a stop by number (0 is the
    /// start). With Return After set, it comes back by itself that many seconds after reaching the end:
    /// "press the button, the platform goes up, a while later it comes down". PingPong and Loop run on
    /// their own while enabled.
    ///
    /// Nothing that moves may be Static: static batching freezes it in place, and its baked light would
    /// stay behind. The kit's validator says so; the game's probes light it wherever it goes.
    /// </summary>
    [AddComponentMenu("Map Kit/Logic/Map Mover")]
    public sealed class MapMover : MapEntity
    {
        [Tooltip("Başlangıçtan sonraki duraklar. 0 numaralı durak başlangıcın kendisidir.")]
        [SerializeField] MapMoverStop[] stops = { new MapMoverStop() };

        [Tooltip("Hız, metre/saniye (en az 0,05).")]
        [SerializeField, Min(0.05f)] float speed = 2f;

        [Tooltip("Dönüş hızı, derece/saniye.")]
        [SerializeField, Min(1f)] float angularSpeed = 90f;

        [Tooltip("Açıksa hızlanarak kalkar, yavaşlayarak durur.")]
        [SerializeField] bool smooth = true;

        [SerializeField] MapMoverLoop loop = MapMoverLoop.Once;

        [Tooltip("Gidip gelirken ya da dönerken başlangıçta kaç saniye bekler.")]
        [SerializeField, Min(0f)] float waitAtStart;

        [Tooltip("Once: son durağa vardıktan kaç saniye sonra kendiliğinden başa döner. Eksi: dönmez.")]
        [SerializeField] float returnAfter = -1f;

        [Tooltip("Round başında kendiliğinden kalkar: Once ileri gider, PingPong ve Loop dönmeye başlar.")]
        [SerializeField] bool autoStart;

        [Tooltip("Yolunu kesen, itilemeyen bir oyuncu olursa ne yapar.")]
        [SerializeField] MapMoverBlocked whenBlocked = MapMoverBlocked.Reverse;

        [Tooltip("Crush: sıkıştırdığı oyuncuya saniyede verdiği hasar.")]
        [SerializeField, Min(0)] int crushDamage = 25;

        public MapMoverStop[] Stops => stops ?? Array.Empty<MapMoverStop>();
        public float Speed => speed;
        public float AngularSpeed => angularSpeed;
        public bool Smooth => smooth;
        public MapMoverLoop LoopMode => loop;
        public float WaitAtStart => waitAtStart;
        public float ReturnAfter => returnAfter;
        public bool AutoStart => autoStart;
        public MapMoverBlocked WhenBlocked => whenBlocked;
        public int CrushDamage => crushDamage;

        static readonly MapEvent[] events =
        {
            MapEvent.Started, MapEvent.ArrivedAtStop, MapEvent.ArrivedAtEnd, MapEvent.ArrivedAtStart, MapEvent.Blocked,
        };

        static readonly MapInput[] inputs =
        {
            MapInput.Enable, MapInput.Disable, MapInput.Toggle,
            MapInput.Forward, MapInput.Back, MapInput.GoToStop, MapInput.Reverse, MapInput.Pause, MapInput.Resume,
        };

        public override MapEvent[] Events => events;
        public override MapInput[] Inputs => inputs;

        /// <summary>The path, as the stops' world positions from where the mover stands now: for the gizmo
        /// while it has not moved, which in the editor is always.</summary>
        protected override void OnDrawGizmosSelected()
        {
            base.OnDrawGizmosSelected();

            Transform parent = transform.parent;
            Vector3 start = transform.localPosition;
            Vector3 previous = transform.position;
            Gizmos.color = new Color(0.3f, 0.85f, 1f, 0.9f);
            Bounds size = LocalBounds();

            foreach (MapMoverStop stop in Stops)
            {
                if (stop == null)
                    continue;

                Vector3 local = start + stop.position;
                Vector3 world = parent != null ? parent.TransformPoint(local) : local;
                Gizmos.DrawLine(previous, world);
                Gizmos.DrawWireSphere(world, 0.15f);

                Quaternion turn = transform.localRotation * Quaternion.Euler(stop.rotation);
                Quaternion worldTurn = parent != null ? parent.rotation * turn : turn;
                Gizmos.matrix = Matrix4x4.TRS(world, worldTurn, transform.lossyScale);
                Gizmos.DrawWireCube(size.center, size.size);
                Gizmos.matrix = Matrix4x4.identity;
                previous = world;
            }

            if (loop == MapMoverLoop.Loop)
                Gizmos.DrawLine(previous, transform.position);
        }

        /// <summary>What the mover carries, in its own space, for drawing it at its stops.</summary>
        Bounds LocalBounds()
        {
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            bool any = false;
            foreach (Renderer renderer in GetComponentsInChildren<Renderer>())
            {
                Bounds world = renderer.bounds;
                Vector3 min = transform.InverseTransformPoint(world.min);
                Vector3 max = transform.InverseTransformPoint(world.max);
                var local = new Bounds((min + max) * 0.5f, Vector3.zero);
                local.Encapsulate(min);
                local.Encapsulate(max);
                if (any)
                    bounds.Encapsulate(local);
                else
                    bounds = local;
                any = true;
            }
            return any ? bounds : new Bounds(Vector3.zero, Vector3.one * 0.5f);
        }
    }
}
