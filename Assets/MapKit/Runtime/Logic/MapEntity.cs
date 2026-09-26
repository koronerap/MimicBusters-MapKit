using System;
using System.Collections.Generic;
using UnityEngine;

namespace Mimicraft.MapKit
{
    /// <summary>
    /// A piece of map logic (plan §16): a button, a moving platform, a timer. Data only, like every
    /// component a map may carry: the game reads these fields and does the work (MapLogic), so a map still
    /// brings no code.
    ///
    /// Every entity has a number, unique in its scene, that the network uses to name it: the scene is the
    /// same file on every machine (the readiness gate compares hashes), so the number means the same entity
    /// everywhere. The kit assigns it (MapLogicIds) when the scene is validated or packed.
    /// </summary>
    public abstract class MapEntity : MonoBehaviour
    {
        [Tooltip("Ağdaki numarası. Kit kendisi verir (Doğrula, Paketle); elle değiştirme.")]
        [SerializeField] int entityId;

        [Tooltip("Round başında açık mı. Kapalı bir varlık tepki vermez ve çıkışlarını tetiklemez; " +
                 "Enable girişi açar.")]
        [SerializeField] bool startEnabled = true;

        [Tooltip("Bu varlığın olayları başka varlıklara ne yaptırır. Olay, hedef, giriş, gecikme.")]
        [SerializeField] MapOutput[] outputs = Array.Empty<MapOutput>();

        public int EntityId => entityId;
        public bool StartEnabled => startEnabled;
        public IReadOnlyList<MapOutput> Outputs => outputs ?? Array.Empty<MapOutput>();

        /// <summary>The events this kind of entity raises.</summary>
        public abstract MapEvent[] Events { get; }

        /// <summary>The inputs this kind of entity takes.</summary>
        public abstract MapInput[] Inputs { get; }

        public bool Takes(MapInput input) => Array.IndexOf(Inputs, input) >= 0;

        public bool Raises(MapEvent e) => Array.IndexOf(Events, e) >= 0;

#if UNITY_EDITOR
        /// <summary>For the kit (MapLogicIds).</summary>
        public void EditorSetEntityId(int id) => entityId = id;
#endif

        /// <summary>A line to each target, so a map's wiring can be read in the Scene view: Hammer's I/O
        /// lines.</summary>
        protected virtual void OnDrawGizmosSelected()
        {
            if (outputs == null)
                return;

            Gizmos.color = new Color(1f, 0.75f, 0.2f, 0.9f);
            foreach (MapOutput output in outputs)
            {
                if (output != null && output.target != null && output.target != this)
                    Gizmos.DrawLine(transform.position, output.target.transform.position);
            }
        }
    }
}
