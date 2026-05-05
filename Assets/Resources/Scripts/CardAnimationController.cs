using System.Collections;
using UnityEngine;

public class CardAnimationController : MonoBehaviour
{
    [Header("Animator")]
    [SerializeField] private Animator animator;

    [Header("Timing")]
    [SerializeField] private float attack1HitDelay = 0.45f;
    [SerializeField] private float attack2HitDelay = 0.60f;

    private static readonly int Attack1Hash = Animator.StringToHash("Attack1");
    private static readonly int Attack2Hash = Animator.StringToHash("Attack2");
    private static readonly int HitHash     = Animator.StringToHash("Hit");
    private static readonly int DeathHash   = Animator.StringToHash("Death");

    private void Awake()
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (animator != null)
            animator.applyRootMotion = false;
        else
            Debug.LogWarning($"[CardAnimationController] No Animator found on {name}.");
    }

    public void PlayAttack(int attackSlot)
    {
        if (animator == null) return;

        if (attackSlot == 1)
            animator.SetTrigger(Attack1Hash);
        else
            animator.SetTrigger(Attack2Hash);
    }

    public void PlayHit()
    {
        if (animator == null) return;
        animator.SetTrigger(HitHash);
    }

    public void PlayDeath()
    {
        if (animator == null) return;
        animator.SetTrigger(DeathHash);
    }

    public float GetHitDelay(int attackSlot)
    {
        return attackSlot == 1 ? attack1HitDelay : attack2HitDelay;
    }
}