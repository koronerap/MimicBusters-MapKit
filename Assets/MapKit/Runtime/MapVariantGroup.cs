using System;
using System.Collections.Generic;
using System.Text;
using Mimicraft.Gameplay;
using UnityEngine;

namespace Mimicraft.MapKit
{
    /// <summary>
    /// A part of the map that comes out differently from round to round: each round ONE of this object's children is
    /// switched on and the others off, so the Hunters cannot learn the map by heart. A blocked corridor one round and
    /// an open one the next, a wardrobe that stands in the bedroom or the hall, crates stacked three ways.
    ///
    /// Put it on an empty object and give it the versions as children. The game does the choosing (MapVariants, on
    /// the server, sent to everybody), in the modes ticked under Modes; in the others the children stay as they were
    /// authored. A child is picked by its weight (MapVariantOption, 1 without one), and with Avoid Repeat not the
    /// child of the round before when there is another. Groups sharing a Link Id pick the same child number, so two
    /// halves of one idea - a door shut here, an opening there - always agree.
    ///
    /// Groups may sit inside other groups' children: the inner one chooses too, and matters while its branch is on.
    /// Spawn markers inside a child are used only while that child is on.
    /// </summary>
    [AddComponentMenu("Map Kit/Map Variant Group")]
    [DisallowMultipleComponent]
    public sealed class MapVariantGroup : MonoBehaviour
    {
        [Tooltip("Hangi modlarda her round bir çocuk seçilsin. Seçili olmayan modlarda çocuklar sahnede " +
                 "bırakıldığı gibi kalır (açık olan açık, kapalı olan kapalı).")]
        [SerializeField] MapMarkerModes modes = MapMarkerModes.Standard;

        [Tooltip("Açıksa bir önceki roundun çocuğu, seçilebilecek başka biri varken yeniden seçilmez. Avcı aynı " +
                 "düzeni arka arkaya iki kez görmez.")]
        [SerializeField] bool avoidRepeat = true;

        [Tooltip("Aynı kimliği taşıyan gruplar her round AYNI sıradaki çocuğu seçer (birinci, ikinci...). Bir " +
                 "fikrin iki yarısı için: burada kapı kapalıyken orada geçit açık. Çocuk sayıları farklıysa sıra " +
                 "her grubun kendi sayısına göre sarılır. Boş = bağımsız.")]
        [SerializeField] string linkId = "";

        static readonly List<MapVariantGroup> active = new List<MapVariantGroup>();

        /// <summary>Raised when a group comes on - a map arriving, or a group inside another group's child as that
        /// child is chosen. The game answers it with the group's choice (MapVariants).</summary>
        public static event Action<MapVariantGroup> Enabled;

        /// <summary>Groups switched on right now. The game looks wider (inactive ones included) when it chooses.</summary>
        public static IReadOnlyList<MapVariantGroup> Active => active;

        /// <summary>Domain reload is off in this project, so statics are cleared with each session.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            active.Clear();
            Enabled = null;
        }

        // How the children were authored, read once before anything changes them, for the modes that do not choose.
        bool[] authored;
        uint key;
        bool keyed;

        public MapMarkerModes Modes => modes;
        public bool AvoidRepeat => avoidRepeat;
        public string LinkId => linkId ?? "";

        /// <summary>How many versions there are: this object's direct children.</summary>
        public int Count => transform.childCount;

        /// <summary>The child switched on alone by the last Show, or -1 (authored, or not chosen yet).</summary>
        public int Shown { get; private set; } = -1;

        public bool AppliesTo(MapMarkerModes mode) => (modes & mode) != 0;

        void OnEnable()
        {
            CaptureAuthored();
            active.Add(this);
            Enabled?.Invoke(this);
        }

        void OnDisable() => active.Remove(this);

        /// <summary>How likely a child is: its MapVariantOption's weight, 1 without one, 0 for a child out of range.</summary>
        public float WeightOf(int index)
        {
            if (index < 0 || index >= transform.childCount)
                return 0f;

            MapVariantOption option = transform.GetChild(index).GetComponent<MapVariantOption>();
            return option != null ? option.Weight : 1f;
        }

        /// <summary>A child by weight, or -1 when every weight is 0. <paramref name="avoid"/> is left out while
        /// anything else can be picked. <paramref name="roll"/> is a number in [0, 1).</summary>
        public int Pick(float roll, int avoid)
        {
            float total = 0f;
            for (int i = 0; i < Count; i++)
            {
                if (i != avoid)
                    total += WeightOf(i);
            }

            if (total <= 0f)
            {
                // Only the avoided one can be picked: better a repeat than an empty map.
                return avoid >= 0 && WeightOf(avoid) > 0f ? avoid : -1;
            }

            float target = Mathf.Clamp01(roll) * total;
            int last = -1;
            for (int i = 0; i < Count; i++)
            {
                if (i == avoid)
                    continue;

                float weight = WeightOf(i);
                if (weight <= 0f)
                    continue;

                last = i;
                if (target < weight)
                    return i;
                target -= weight;
            }

            return last;
        }

        /// <summary>Switches child <paramref name="index"/> on and every other child off. An index past the end (a
        /// map edited on one machine and not another) shows the authored state rather than nothing.</summary>
        public void Show(int index)
        {
            CaptureAuthored();
            if (index < 0 || index >= Count)
            {
                ShowAuthored();
                return;
            }

            for (int i = 0; i < Count; i++)
            {
                GameObject child = transform.GetChild(i).gameObject;
                bool on = i == index;
                if (child.activeSelf != on)
                    child.SetActive(on);
            }

            Shown = index;
        }

        /// <summary>Puts every child back the way the scene had it.</summary>
        public void ShowAuthored()
        {
            CaptureAuthored();
            for (int i = 0; i < Count && i < authored.Length; i++)
            {
                GameObject child = transform.GetChild(i).gameObject;
                if (child.activeSelf != authored[i])
                    child.SetActive(authored[i]);
            }

            Shown = -1;
        }

        void CaptureAuthored()
        {
            if (authored != null && authored.Length == Count)
                return;

            authored = new bool[Count];
            for (int i = 0; i < authored.Length; i++)
                authored[i] = transform.GetChild(i).gameObject.activeSelf;
        }

        /// <summary>The same number for the same group on every machine, so the server can say "group K shows child
        /// N" and every client knows which group it means without the group being a network object. Made from the
        /// scene's name and the path down to the group: names all the way, and the position among siblings below the
        /// top level. The top level is names only because the objects at the root of a scene are not in the same order
        /// everywhere - the network spawns its own objects there - while inside the map nothing differs.</summary>
        public uint Key
        {
            get
            {
                if (keyed)
                    return key;

                var path = new StringBuilder(gameObject.scene.name);
                var chain = new List<Transform>();
                for (Transform t = transform; t != null; t = t.parent)
                    chain.Add(t);

                for (int i = chain.Count - 1; i >= 0; i--)
                {
                    Transform t = chain[i];
                    path.Append('/').Append(t.name);
                    if (t.parent != null)
                        path.Append('#').Append(t.GetSiblingIndex());
                }

                key = Fnv1a(path.ToString());
                keyed = true;
                return key;
            }
        }

        static uint Fnv1a(string text)
        {
            uint hash = 0x811C9DC5;
            foreach (byte b in Encoding.UTF8.GetBytes(text))
            {
                hash ^= b;
                hash = unchecked(hash * 0x01000193);
            }

            return hash;
        }

        /// <summary>Whether <paramref name="point"/> sits in a version that is switched off - a spawn marker inside a
        /// child that was not chosen this round, which nobody should be put at.</summary>
        public static bool InHiddenVariant(Transform point)
        {
            for (Transform t = point; t != null && t.parent != null; t = t.parent)
            {
                if (!t.gameObject.activeSelf && t.parent.GetComponent<MapVariantGroup>() != null)
                    return true;
            }

            return false;
        }
    }
}
