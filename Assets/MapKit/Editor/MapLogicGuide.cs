using System.Collections.Generic;
using Mimicraft.MapKit;

namespace Mimicraft.EditorTools
{
    /// <summary>
    /// The words the map logic inspector uses (plan §16.7): what each kind of entity is, what each event
    /// and input means, and what an input's number stands for. Turkish and English, with the kit's language
    /// (KitEditorLanguage). The meanings are the game's code (MapLogic) written down; when that changes,
    /// this follows.
    /// </summary>
    public static class MapLogicGuide
    {
        static string T(string tr, string en) => KitEditorLanguage.T(tr, en);

        public static string DescribeEntity(MapEntity entity)
        {
            switch (entity.GetType().Name)
            {
                case "MapButton":
                    return T("Oyuncunun etkileşim tuşuyla bastığı düğme. Bakılabilmesi için collider gerekir.",
                        "A button players press with the interact key. Needs a collider to be looked at.");
                case "MapRelay":
                    return T("Bir girişi birkaç çıkışa dağıtır, her biri kendi gecikmesiyle.",
                        "Passes one input on to several outputs, each with its own delay.");
                case "MapTimer":
                    return T("Açıkken her aralıkta Elapsed olayı verir.", "Raises Elapsed every interval while enabled.");
                case "MapCounter":
                    return T("Sayar; sayı hedefe varınca ReachedTarget olayı verir.", "Counts; raises ReachedTarget when the count reaches the target.");
                case "MapToggle":
                    return T("Bu objeyi ve çocuklarını (görüntü, collider, ışık) gösterir ya da gizler.",
                        "Shows or hides this object and its children (what is drawn, what is solid, lights).");
                case "MapRoundEvents":
                    return T("Round'un anlarını olay olarak verir. Haritada bir tane yeter.",
                        "Raises the round's moments as events. One per map is enough.");
                case "MapMover":
                    return T("Objeyi çocuklarıyla durak listesi boyunca taşır ve döndürür; üstündeki oyuncuları da taşır.",
                        "Moves and turns this object and its children along its stops, carrying the players on it.");
                case "MapTrigger":
                    return T("Görünmez bir kutu: oyuncu girince ve çıkınca olay verir.", "An invisible box: raises events when players enter and leave.");
                case "MapHurtZone":
                    return T("Kutunun içindekilere her aralıkta hasar verir.", "Damages whoever is inside the box, every interval.");
                case "MapSound":
                    return T("Kendi yerinde bir ses çalar; oyuncunun efekt sesi ayarından geçer.",
                        "Plays a sound where it stands, through the player's effects volume.");
                case "MapEffect":
                    return T("Oyunun hazır efektlerinden birini gösterir: duman, kıvılcım, toz, ateş.",
                        "Shows one of the game's own effects: smoke, sparks, dust, fire.");
                case "MapTeleport":
                    return T("Kutusuna gireni hedef objeye ışınlar; TeleportActivator girişiyle zinciri başlatanı da. Yalnızca girişle kullanılacaksa kapalı başlat.",
                        "Sends whoever walks into its box to the destination object, and with TeleportActivator whoever set the chain off. Start it disabled to use it only that way.");
                default:
                    return "";
            }
        }

        public static string EventName(MapEvent e) => e.ToString();

        public static string DescribeEvent(MapEvent e)
        {
            switch (e)
            {
                case MapEvent.Pressed: return T("Bir oyuncu bastı.", "A player pressed it.");
                case MapEvent.PressedWhileLocked: return T("Kilitliyken basıldı.", "Pressed while locked.");
                case MapEvent.Started: return T("Hareket başladı.", "Started moving.");
                case MapEvent.ArrivedAtStop: return T("Bir durağa vardı.", "Arrived at a stop.");
                case MapEvent.ArrivedAtEnd: return T("Son durağa vardı.", "Arrived at the last stop.");
                case MapEvent.ArrivedAtStart: return T("Başa döndü.", "Arrived back at the start.");
                case MapEvent.Blocked: return T("Bir oyuncu yolunu kesti.", "A player blocked it.");
                case MapEvent.Entered: return T("Bir oyuncu girdi.", "A player entered.");
                case MapEvent.Exited: return T("Bir oyuncu çıktı.", "A player left.");
                case MapEvent.Occupied: return T("İlk oyuncu girdi.", "The first player entered.");
                case MapEvent.Emptied: return T("Son oyuncu çıktı.", "The last player left.");
                case MapEvent.Hurt: return T("Birine hasar verdi.", "Hurt someone.");
                case MapEvent.Killed: return T("Birini öldürdü.", "Killed someone.");
                case MapEvent.Finished: return T("Ses bitti.", "The sound finished.");
                case MapEvent.Triggered: return T("Tetiklendi.", "Triggered.");
                case MapEvent.Elapsed: return T("Süre doldu.", "The interval elapsed.");
                case MapEvent.ReachedTarget: return T("Sayı hedefe vardı.", "The count reached the target.");
                case MapEvent.RoundStarted: return T("Round başladı (her şey başa döndü).", "A round started (everything was reset).");
                case MapEvent.HuntStarted: return T("Av başladı.", "The hunt started.");
                case MapEvent.RoundEnded: return T("Round bitti.", "The round ended.");
                case MapEvent.Teleported: return T("Birini ışınladı.", "Teleported someone.");
                default: return "";
            }
        }

        public static string DescribeInput(MapInput input)
        {
            switch (input)
            {
                case MapInput.Enable: return T("Aç (göster, başlat).", "Enable (show, start).");
                case MapInput.Disable: return T("Kapat (gizle, durdur).", "Disable (hide, stop).");
                case MapInput.Toggle: return T("Açıksa kapat, kapalıysa aç.", "Enable if disabled, disable if enabled.");
                case MapInput.Press: return T("Bir oyuncu basmış gibi.", "As if a player pressed it.");
                case MapInput.Lock: return T("Kilitle.", "Lock.");
                case MapInput.Unlock: return T("Kilidi aç.", "Unlock.");
                case MapInput.Forward: return T("Son durağa git.", "Go to the last stop.");
                case MapInput.Back: return T("Başa dön.", "Go back to the start.");
                case MapInput.GoToStop: return T("Şu durağa git.", "Go to this stop.");
                case MapInput.Reverse: return T("Gittiği yönün tersine dön.", "Turn back the way it came.");
                case MapInput.Pause: return T("Olduğu yerde dur.", "Stop where it is.");
                case MapInput.Resume: return T("Kaldığı yerden devam et.", "Carry on from where it stopped.");
                case MapInput.Play: return T("Çal.", "Play.");
                case MapInput.Stop: return T("Sustur.", "Stop.");
                case MapInput.Trigger: return T("Tetikle.", "Trigger.");
                case MapInput.Add: return T("Şu kadar ekle (0: 1).", "Add this much (0: 1).");
                case MapInput.Subtract: return T("Şu kadar çıkar (0: 1).", "Subtract this much (0: 1).");
                case MapInput.SetValue: return T("Değeri şu yap.", "Set the value to this.");
                case MapInput.ResetCount: return T("Başlangıç değerine döndür.", "Back to the start value.");
                case MapInput.HurtActivator: return T("Zinciri başlatan oyuncuya bu bölgenin hasarını bir kez ver.", "Give the player who set the chain off this zone's damage, once.");
                case MapInput.TeleportActivator: return T("Zinciri başlatan oyuncuyu bu ışınlayıcının hedefine gönder.", "Send the player who set the chain off to this teleport's destination.");
                default: return "";
            }
        }

        /// <summary>What the output's number means for this input, or null when the input takes none.</summary>
        public static string ParameterLabel(MapInput input)
        {
            switch (input)
            {
                case MapInput.GoToStop: return T("Durak", "Stop number");
                case MapInput.Add:
                case MapInput.Subtract: return T("Miktar", "Amount");
                case MapInput.SetValue: return T("Değer", "Value");
                case MapInput.HurtActivator: return T("Hasar (0: bölgenin kendi hasarı)", "Damage (0: the zone's own)");
                default: return null;
            }
        }

        /// <summary>Every input in the enum, for a target nobody has chosen yet.</summary>
        public static readonly MapInput[] AllInputs = (MapInput[])System.Enum.GetValues(typeof(MapInput));

        public static string[] Names<T>(IReadOnlyList<T> values)
        {
            var names = new string[values.Count];
            for (int i = 0; i < values.Count; i++)
                names[i] = values[i].ToString();
            return names;
        }
    }
}
