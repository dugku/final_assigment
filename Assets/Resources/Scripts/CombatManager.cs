using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Handles manual combat execution for the turn-based system.
/// No longer auto-fires — attacks are triggered by GameStateManager.
/// </summary>
public class CombatManager : MonoBehaviour
{
    [Header("Characters (filled at runtime by ImageTrackingManager)")]
    public List<CardCharacter> characters = new List<CardCharacter>();

    /// <summary>
    /// Executes a full attack from attacker to target card.
    /// Called by GameStateManager.
    /// </summary>
    public void ExecuteAttack(CardCharacter attacker, CardCharacter target)
    {
        if (attacker == null || target == null) return;
        if (!attacker.IsAlive || !target.IsAlive) return;

        Debug.Log($"[CombatManager] {attacker.characterName} attacks {target.characterName}");

        // Fire the projectile visually
        FireProjectileOnly(attacker, target);

        // Apply damage immediately (projectile is just visual)
        target.TakeDamage(attacker.damagePerShot);
    }

    /// <summary>
    /// Fires a projectile from attacker toward target (or forward if target is null for direct attacks).
    /// </summary>
    public void FireProjectileOnly(CardCharacter attacker, CardCharacter target)
    {
        if (attacker == null) return;
        if (attacker.projectilePrefab == null)
        {
            Debug.LogWarning($"[CombatManager] {attacker.characterName} has no projectile prefab.");
            return;
        }

        Vector3 origin = attacker.GetProjectileSpawnPoint();
        GameObject projGO = Instantiate(attacker.projectilePrefab, origin, Quaternion.identity);
        Projectile proj = projGO.GetComponent<Projectile>();

        if (proj != null)
        {
            proj.InitializeVisualOnly(target, attacker.projectileSpeed, attacker.projectileColor);
        }
    }
}