using UnityEngine;

/// <summary>
/// Attach to each character prefab.
/// Turn-based — attacks triggered by GameStateManager.
/// Supports two attacks per card with different damage and mana costs.
/// </summary>
public class CardCharacter : MonoBehaviour
{
    [Header("Identity")]
    public string characterName = "Unknown";

    [Header("Attack 1 (Cheap)")]
    public string attack1Name     = "Attack 1";
    public float  attack1Damage   = 20f;
    public int    attack1ManaCost = 2;

    [Header("Attack 2 (Expensive)")]
    public string attack2Name     = "Attack 2";
    public float  attack2Damage   = 40f;
    public int    attack2ManaCost = 4;

    [Header("Stats")]
    public float maxHealth = 100f;

    [Header("Projectile")]
    public GameObject projectilePrefab;
    public Color      projectileColor = Color.white;
    public float      projectileSpeed = 5f;
    public Transform  firePoint;

    [Header("Rotation")]
    public float rotationSpeed = 5f;

    // ── Runtime ────────────────────────────────────────────────────────
    public float CurrentHealth { get; private set; }
    public bool  IsAlive       { get; private set; } = true;
    public int   OwnerIndex    { get; private set; } = -1;
    public float damagePerShot { get; set; } = 20f;

    private CardCharacter currentTarget;
    private Renderer[]    cachedRenderers;
    private Collider[]    cachedColliders;
    private Canvas[]      cachedCanvases;

    void Awake()
    {
        CurrentHealth   = maxHealth;
        damagePerShot   = attack1Damage;
        cachedRenderers = GetComponentsInChildren<Renderer>(true);
        cachedColliders = GetComponentsInChildren<Collider>(true);
        cachedCanvases  = GetComponentsInChildren<Canvas>(true);
    }

    void Update()
    {
        if (IsAlive && currentTarget != null && currentTarget.IsAlive)
        {
            Vector3 dir = currentTarget.transform.position - transform.position;
            dir.y = 0f;
            if (dir != Vector3.zero)
            {
                Quaternion targetRot = Quaternion.LookRotation(dir);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRot,
                    Time.deltaTime * rotationSpeed);
            }
        }
    }

    // ── Setup ──────────────────────────────────────────────────────────
    public void SetOwner(int playerIndex) => OwnerIndex = playerIndex;

    public void ResetForNewGame()
    {
        CurrentHealth = maxHealth;
        IsAlive       = true;
        currentTarget = null;
        damagePerShot = attack1Damage;

        // Show all renderers and colliders
        ShowCharacter();

        // Explicitly re-enable each health bar canvas and its HealthBar component
        // This is necessary because gameObject.SetActive(false) in Die() deactivates
        // children — so HealthBar.OnEnable won't fire until we explicitly re-enable it
        foreach (Canvas cv in cachedCanvases)
        {
            if (cv == null) continue;
            cv.enabled = true;

            // Find HealthBar on the canvas or its children and re-enable it
            HealthBar[] healthBars = cv.GetComponentsInChildren<HealthBar>(true);
            foreach (HealthBar hb in healthBars)
            {
                if (hb != null && !hb.gameObject.activeSelf)
                {
                    hb.gameObject.SetActive(true);
                }
            }
        }
    }

    public void SetTarget(CardCharacter target) => currentTarget = target;

    // ── Combat ─────────────────────────────────────────────────────────
    public void TakeDamage(float amount)
    {
        if (!IsAlive) return;
        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        Debug.Log($"{characterName} took {amount} damage. HP: {CurrentHealth}/{maxHealth}");
        if (CurrentHealth <= 0f) Die();
    }

    private void Die()
    {
        if (!IsAlive) return;
        IsAlive       = false;
        currentTarget = null;
        Debug.Log($"{characterName} defeated!");
        HideCharacter();
        GameStateManager.Instance?.OnCardDied(this);
    }

    // ── Projectile Helpers ─────────────────────────────────────────────
    public Vector3 GetProjectileSpawnPoint()
    {
        if (firePoint != null) return firePoint.position;
        return GetAimPoint();
    }

    public Vector3 GetAimPoint()
    {
        if (firePoint != null) return firePoint.position;
        Collider col = GetComponentInChildren<Collider>();
        if (col  != null) return col.bounds.center;
        Renderer rend = GetComponentInChildren<Renderer>();
        if (rend != null) return rend.bounds.center;
        return transform.position + Vector3.up * 0.5f;
    }

    // ── Visibility ─────────────────────────────────────────────────────
    private void ShowCharacter()
    {
        foreach (Renderer r  in cachedRenderers) if (r  != null) r.enabled  = true;
        foreach (Collider c  in cachedColliders) if (c  != null) c.enabled  = true;
        foreach (Canvas   cv in cachedCanvases)  if (cv != null) cv.enabled = true;
    }

    private void HideCharacter()
    {
        foreach (Renderer r  in cachedRenderers) if (r  != null) r.enabled  = false;
        foreach (Collider c  in cachedColliders) if (c  != null) c.enabled  = false;
        foreach (Canvas   cv in cachedCanvases)  if (cv != null) cv.enabled = false;
    }
}