using UnityEngine;

public static class PlayerConfig
{
    public const float MoveSpeed      = 5.6f;  // Smooth kiting (§7.2 fixed)
    public const float AttackDamage   = 25f;   // 5 hits vs baseline 125 HP (§7.2 fixed)
    public const float AttackCooldown = 0.42f; // Crisp melee cadence (§7.2 fixed)
    public const float MaxHP          = 180f;  // Flow sweet spot vs 10-40 enemy ATK (§7.2 fixed)
}
