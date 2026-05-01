using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ImageTrackingManager : MonoBehaviour
{
    private ARTrackedImageManager ourTrackedImages;
    public GameObject[] ourModelPrefabs;

    [Header("References")]
    public CombatManager    combatManager;
    public GameStateManager gameStateManager;

    [Header("Tracking Settings")]
    public float trackingLostGracePeriod = 2.0f;

    // Simple approach — one spawned object per card name, period.
    // Both players share the same instance. The "both players same card"
    // feature is handled by detaching and re-scanning, but only ONE
    // model exists per card at a time to prevent duplicates.
    private readonly Dictionary<string, GameObject>    spawnedObjects    = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, CardCharacter> spawnedCharacters = new Dictionary<string, CardCharacter>();
    private readonly Dictionary<string, float>         lostTimers        = new Dictionary<string, float>();

    // Global cooldown per card name — prevents any re-spawn within window
    private readonly Dictionary<string, float> globalCooldowns = new Dictionary<string, float>();
    private const float GlobalCooldown = 5.0f;

    // Cards that have been placed and detached
    private readonly HashSet<string> detachedCards = new HashSet<string>();

    void Awake()
    {
        ourTrackedImages = GetComponent<ARTrackedImageManager>();
    }

    void OnEnable()
    {
        if (ourTrackedImages != null)
            ourTrackedImages.trackedImagesChanged += WhenTrackedImagesChange;
    }

    void OnDisable()
    {
        if (ourTrackedImages != null)
            ourTrackedImages.trackedImagesChanged -= WhenTrackedImagesChange;
    }

    void Update()
    {
        foreach (var key in new List<string>(lostTimers.Keys))
        {
            lostTimers[key] -= Time.deltaTime;
            if (lostTimers[key] <= 0f) lostTimers.Remove(key);
        }
        foreach (var key in new List<string>(globalCooldowns.Keys))
        {
            globalCooldowns[key] -= Time.deltaTime;
            if (globalCooldowns[key] <= 0f) globalCooldowns.Remove(key);
        }
    }

    private void WhenTrackedImagesChange(ARTrackedImagesChangedEventArgs eventArgs)
    {
        foreach (var trackedImage in eventArgs.added)
        {
            string cardName = trackedImage.referenceImage.name;
            Debug.Log($"[ITM] ADDED event: {cardName} | spawned:{spawnedObjects.ContainsKey(cardName)} | detached:{detachedCards.Contains(cardName)} | cooldown:{globalCooldowns.ContainsKey(cardName)}");

            // Already spawned and NOT detached — ignore, same card
            if (spawnedObjects.ContainsKey(cardName) && !detachedCards.Contains(cardName))
            {
                Debug.Log($"[ITM] Ignoring {cardName} — already spawned and not detached.");
                continue;
            }

            // Already spawned and detached — this is other player's turn, allow re-spawn
            // But only if not on cooldown
            if (spawnedObjects.ContainsKey(cardName) && detachedCards.Contains(cardName))
            {
                if (globalCooldowns.ContainsKey(cardName))
                {
                    Debug.Log($"[ITM] Ignoring {cardName} — on cooldown after detach.");
                    continue;
                }
                // Remove old entry to allow fresh spawn
                spawnedObjects.Remove(cardName);
                spawnedCharacters.Remove(cardName);
                detachedCards.Remove(cardName);
            }

            // On cooldown — ignore
            if (globalCooldowns.ContainsKey(cardName))
            {
                Debug.Log($"[ITM] Ignoring {cardName} — on global cooldown.");
                continue;
            }

            GameObject matchingPrefab = FindMatchingPrefab(cardName);
            if (matchingPrefab == null)
            {
                Debug.LogWarning($"[ITM] No prefab for '{cardName}'.");
                continue;
            }

            Debug.Log($"[ITM] Spawning {cardName}.");
            GameObject spawned = Instantiate(matchingPrefab, trackedImage.transform);
            spawned.transform.localPosition = Vector3.zero;
            spawned.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            spawned.SetActive(false);

            spawnedObjects[cardName] = spawned;
            globalCooldowns[cardName] = GlobalCooldown;

            CardCharacter character = spawned.GetComponent<CardCharacter>();
            if (character != null)
            {
                spawnedCharacters[cardName] = character;
                if (combatManager != null && !combatManager.characters.Contains(character))
                    combatManager.characters.Add(character);

                Debug.Log($"[ITM] Notifying GameStateManager: {cardName}");
                gameStateManager?.OnCardScanned(character);
            }
            else
            {
                Debug.LogWarning($"[ITM] {cardName} has no CardCharacter!");
            }
        }

        foreach (var trackedImage in eventArgs.updated)
        {
            string cardName = trackedImage.referenceImage.name;
            if (!spawnedCharacters.TryGetValue(cardName, out CardCharacter character)) continue;
            if (character == null) continue;

            if (trackedImage.trackingState == TrackingState.Tracking)
            {
                lostTimers.Remove(cardName);

                // Revival check
                TryPlaceForCurrentPlayer(cardName, character);

                if (!character.gameObject.activeSelf && character.IsAlive
                    && !detachedCards.Contains(cardName))
                    character.gameObject.SetActive(true);
            }
            else
            {
                if (!lostTimers.ContainsKey(cardName) && !detachedCards.Contains(cardName))
                {
                    lostTimers[cardName] = trackingLostGracePeriod;
                    Debug.Log($"[ITM] Tracking lost: {cardName}.");
                }
            }
        }
    }

    /// <summary>
    /// Called by GameStateManager after successful placement.
    /// Detaches model from AR so same card can be scanned by other player.
    /// </summary>
    public void DetachPlacedCard(CardCharacter character)
    {
        string cardName = FindCardName(character);
        if (cardName == null) return;
        if (detachedCards.Contains(cardName)) return;

        GameObject go       = character.gameObject;
        Vector3    worldPos = go.transform.position;
        Quaternion worldRot = go.transform.rotation;

        go.transform.SetParent(null, true);
        go.transform.position = worldPos;
        go.transform.rotation = worldRot;

        detachedCards.Add(cardName);

        // Reset cooldown so other player can scan this card
        globalCooldowns.Remove(cardName);

        Debug.Log($"[ITM] Detached {character.characterName} — ready for other player to scan.");
    }

    private void TryPlaceForCurrentPlayer(string cardName, CardCharacter character)
    {
        if (gameStateManager == null) return;
        PlayerState currentPlayer = gameStateManager.CurrentPlayer;
        if (currentPlayer == null) return;
        if (!currentPlayer.HandCards.Contains(character)) return;
        if (globalCooldowns.ContainsKey(cardName + "_revival")) return;

        Debug.Log($"[ITM] Revival: {cardName} in {currentPlayer.PlayerName}'s hand.");
        globalCooldowns[cardName + "_revival"] = GlobalCooldown;
        gameStateManager?.OnCardScanned(character);
    }

    private string FindCardName(CardCharacter character)
    {
        foreach (var kvp in spawnedCharacters)
            if (kvp.Value == character) return kvp.Key;
        return null;
    }

    private GameObject FindMatchingPrefab(string cardName)
    {
        foreach (GameObject prefab in ourModelPrefabs)
            if (prefab != null && prefab.name == cardName)
                return prefab;
        return null;
    }
}