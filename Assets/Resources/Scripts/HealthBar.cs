using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Attach to the Canvas object inside each character prefab.
/// Properly handles revival — resets fill amount and re-displays when card comes back.
/// </summary>
public class HealthBar : MonoBehaviour
{
    [Header("References")]
    public Image         healthBarFill;
    public CardCharacter character;

    private Camera arCamera;
    private bool   loggedMissingFillWarning;
    private bool   loggedMissingCharacterWarning;

    void Awake()
    {
        CacheReferences();
    }

    /// <summary>Called whenever this GameObject is enabled — including after revival.</summary>
    void OnEnable()
    {
        CacheReferences();

        // Reset fill to correct amount when re-enabled (handles revival)
        if (healthBarFill != null && character != null)
        {
            float maxHp = Mathf.Max(1f, character.maxHealth);
            healthBarFill.fillAmount = Mathf.Clamp01(character.CurrentHealth / maxHp);
        }
    }

    void LateUpdate()
    {
        CacheReferences();

        // Billboard — always face camera
        if (arCamera != null)
        {
            transform.LookAt(
                transform.position + arCamera.transform.rotation * Vector3.forward,
                arCamera.transform.rotation * Vector3.up);
        }

        RefreshHealthBar();
    }

    private void CacheReferences()
    {
        if (arCamera == null)
            arCamera = Camera.main;

        if (character == null)
            character = GetComponentInParent<CardCharacter>();

        if (healthBarFill == null)
        {
            Image[] images = GetComponentsInChildren<Image>(true);
            foreach (Image image in images)
            {
                if (image != null && image.name.ToLowerInvariant().Contains("fill"))
                {
                    healthBarFill = image;
                    break;
                }
            }
        }
    }

    private void RefreshHealthBar()
    {
        if (character == null)
        {
            if (!loggedMissingCharacterWarning)
            {
                Debug.LogWarning($"[HealthBar] No CardCharacter found for {name}.", this);
                loggedMissingCharacterWarning = true;
            }
            return;
        }

        if (healthBarFill == null)
        {
            if (!loggedMissingFillWarning)
            {
                Debug.LogWarning($"[HealthBar] No HealthBarFill Image found for {name}.", this);
                loggedMissingFillWarning = true;
            }
            return;
        }

        // Update fill amount every frame
        float maxHp = Mathf.Max(1f, character.maxHealth);
        healthBarFill.fillAmount = Mathf.Clamp01(character.CurrentHealth / maxHp);

        // When dead, zero out fill and hide
        // Don't self-deactivate the whole canvas — let CardCharacter.HideCharacter handle that
        if (!character.IsAlive)
        {
            healthBarFill.fillAmount = 0f;
            gameObject.SetActive(false);
        }
    }
}