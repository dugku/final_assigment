using UnityEngine;

/// <summary>
/// Visual-only projectile for turn-based combat.
/// Damage is applied immediately by CombatManager — this just animates the shot.
/// Supports both target-card attacks and direct-player attacks.
/// </summary>
[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(SphereCollider))]
public class Projectile : MonoBehaviour
{
    private CardCharacter target;
    private float speed;
    private bool hasHit;
    private Renderer rend;
    private SphereCollider sphereCollider;

    private Vector3 travelDirection;
    private bool hasDirectDirection = false;

    private const float DefaultSpeed = 1.5f;
    private const float MaxLifetime = 8f;
    private const float HitDistanceBuffer = 0.05f;

    void Awake()
    {
        rend = GetComponent<Renderer>();
        sphereCollider = GetComponent<SphereCollider>();

        Rigidbody rb = GetComponent<Rigidbody>();
        rb.useGravity = false;
        rb.isKinematic = true;

        if (sphereCollider != null)
            sphereCollider.isTrigger = true;
    }

    /// <summary>
    /// Visual-only.
    /// If targetCharacter is not null, projectile flies toward the target card.
    /// If targetCharacter is null, projectile flies forward in its starting direction.
    /// </summary>
    public void InitializeVisualOnly(CardCharacter targetCharacter, float travelSpeed, Color color)
    {
        target = targetCharacter;
        speed = travelSpeed > 0f ? travelSpeed : DefaultSpeed;

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
        {
            Vector3 targetPos = target.GetAimPoint();
            Vector3 dir = targetPos - transform.position;

            if (dir != Vector3.zero)
            {
                travelDirection = dir.normalized;
                transform.rotation = Quaternion.LookRotation(travelDirection);
                hasDirectDirection = false;
            }
        }
        else
        {
            // Direct player attack: no target card exists, so fly forward.
            travelDirection = transform.forward.normalized;

            if (travelDirection == Vector3.zero)
                travelDirection = Vector3.forward;

            hasDirectDirection = true;
        }

        Destroy(gameObject, MaxLifetime);
    }

    /// <summary>
    /// Legacy compatibility.
    /// </summary>
    public void Initialize(CardCharacter targetCharacter, float damageAmount, float travelSpeed, Color color)
    {
        InitializeVisualOnly(targetCharacter, travelSpeed, color);
    }

    void Update()
    {
        if (hasHit) return;

        if (target != null)
        {
            MoveTowardTarget();
        }
        else if (hasDirectDirection)
        {
            MoveForwardDirectAttack();
        }
    }

    private void MoveTowardTarget()
    {
        Vector3 targetPos = target.GetAimPoint();
        float distToTarget = Vector3.Distance(transform.position, targetPos);

        float hitDist =
            (sphereCollider != null ? sphereCollider.radius * transform.lossyScale.x : 0f)
            + HitDistanceBuffer;

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

    private void MoveForwardDirectAttack()
    {
        transform.position += travelDirection * speed * Time.deltaTime;
    }
}