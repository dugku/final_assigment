using UnityEngine;

/// <summary>
/// Visual-only projectile for turn-based combat.
/// Damage is applied immediately by CombatManager — this just animates the shot.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class Projectile : MonoBehaviour
{
    private CardCharacter target;
    private float         speed;
    private bool          hasHit;
    private Renderer      rend;
    private SphereCollider sphereCollider;

    private const float DefaultSpeed      = 1.5f;
    private const float MaxLifetime       = 8f;
    private const float HitDistanceBuffer = 0.05f;

    void Awake()
    {
        rend          = GetComponent<Renderer>();
        sphereCollider = GetComponent<SphereCollider>();

        Rigidbody rb  = GetComponent<Rigidbody>();
        rb.useGravity  = false;
        rb.isKinematic = true;
        sphereCollider.isTrigger = true;
    }

    /// <summary>
    /// Visual-only — damage already applied by CombatManager.
    /// Target can be null for direct player attacks (fires straight forward).
    /// </summary>
    public void InitializeVisualOnly(CardCharacter targetCharacter, float travelSpeed, Color color)
    {
        target = targetCharacter;
        speed  = travelSpeed > 0f ? travelSpeed : DefaultSpeed;

        if (rend != null)
        {
            rend.material.color = color;
            if (rend.material.HasProperty("_EmissionColor"))
            {
                rend.material.EnableKeyword("_EMISSION");
                rend.material.SetColor("_EmissionColor", color * 0.6f);
            }
        }

        if (target != null)
            transform.LookAt(target.GetAimPoint());

        Destroy(gameObject, MaxLifetime);
    }

    /// <summary>Legacy — kept for compatibility. Use InitializeVisualOnly for turn-based.</summary>
    public void Initialize(CardCharacter targetCharacter, float damageAmount, float travelSpeed, Color color)
    {
        InitializeVisualOnly(targetCharacter, travelSpeed, color);
    }

    void Update()
    {
        if (hasHit || target == null) return;

        Vector3 targetPos      = target.GetAimPoint();
        float   distToTarget   = Vector3.Distance(transform.position, targetPos);
        float   hitDist        = (sphereCollider != null ? sphereCollider.radius * transform.lossyScale.x : 0f) + HitDistanceBuffer;

        if (distToTarget <= hitDist)
        {
            hasHit = true;
            Destroy(gameObject);
            return;
        }

        transform.position = Vector3.MoveTowards(transform.position, targetPos, speed * Time.deltaTime);
        Vector3 dir = (targetPos - transform.position).normalized;
        if (dir != Vector3.zero)
            transform.rotation = Quaternion.LookRotation(dir);
    }
}