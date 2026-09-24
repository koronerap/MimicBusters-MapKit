using UnityEngine;

namespace Mimicraft.Gameplay
{
    /// <summary>
    /// Marks a collider (or everything under an object) as something a bullet can go THROUGH.
    ///
    /// Opt-in by design: a surface without this component stops every bullet, whatever the weapon's
    /// penetration says. The two numbers meet in PlayerCombat.FirePellet - a pellet spends this surface's
    /// resistance out of the weapon's penetration and carries on with what is left, or stops here when it
    /// has less than the surface asks.
    ///
    /// Found with GetComponentInParent from the collider that was struck, so one component on a wall
    /// prefab covers every collider in it.
    /// </summary>
    public class PenetrableSurface : MonoBehaviour
    {
        [Tooltip("Geçirmezlik: bu yüzeyi delip geçmek için merminin harcadığı delme gücü. Silahın Penetration " +
                 "değeri (kalan gücü) bundan küçükse mermi burada durur. 0 = her mermi bedelsiz geçer.")]
        [SerializeField, Min(0f)] float resistance = 1f;

        [Tooltip("Geçtikten sonra merminin hasarının ne kadarı kalır. 1 = hasar düşmez, 0.5 = yarıya iner. " +
                 "Birden fazla yüzey geçilirse çarpanlar üst üste biner.")]
        [SerializeField, Range(0f, 1f)] float damageRetained = 0.7f;

        public float Resistance => resistance;
        public float DamageRetained => damageRetained;
    }
}
