using UnityEngine;

namespace Mimicraft.MapKit
{
    /// <summary>The words a map button's prompt shows, translated by the game. Custom shows the author's
    /// own short text, in whatever language it was written.</summary>
    public enum MapButtonPrompt
    {
        Press = 0,
        Open = 1,
        Close = 2,
        Call = 3,
        Use = 4,
        Custom = 5,
    }

    /// <summary>
    /// A button a player presses with the interact key, the way a weapon is picked up or the shooting
    /// range is worked: look at it, press. Needs a collider to be looked at. Raises Pressed, or
    /// PressedWhileLocked when locked (for a "denied" sound, say).
    /// </summary>
    [AddComponentMenu("Map Kit/Logic/Map Button")]
    public sealed class MapButton : MapEntity
    {
        [Tooltip("Kim basabilir. Avcı ve Modelci ayrımı olmayan modlarda herkes basar.")]
        [SerializeField] MapWho who = MapWho.Everyone;

        [Tooltip("İki basış arasında en az kaç saniye (en az 0,25).")]
        [SerializeField, Min(0.25f)] float cooldown = 1f;

        [Tooltip("Round başına yalnızca bir kez basılabilir.")]
        [SerializeField] bool once;

        [Tooltip("Round başında kilitli mi. Kilitli düğmeye basmak Pressed değil PressedWhileLocked olayını verir.")]
        [SerializeField] bool startLocked;

        [Tooltip("Oyuncuya görünen istem. Custom seçilirse aşağıdaki kısa metin (en çok 32 karakter, çevrilmez).")]
        [SerializeField] MapButtonPrompt prompt = MapButtonPrompt.Press;

        [SerializeField] string customPrompt = "";

        [Tooltip("Bakılınca yanan parça. Boşsa bu objenin Renderer'ı.")]
        [SerializeField] Renderer highlight;

        public MapWho Who => who;
        public float Cooldown => cooldown;
        public bool Once => once;
        public bool StartLocked => startLocked;
        public MapButtonPrompt Prompt => prompt;
        public string CustomPrompt => customPrompt ?? "";
        public Renderer Highlight => highlight;

        static readonly MapEvent[] events = { MapEvent.Pressed, MapEvent.PressedWhileLocked };

        static readonly MapInput[] inputs =
        {
            MapInput.Enable, MapInput.Disable, MapInput.Toggle, MapInput.Press, MapInput.Lock, MapInput.Unlock,
        };

        public override MapEvent[] Events => events;
        public override MapInput[] Inputs => inputs;
    }
}
