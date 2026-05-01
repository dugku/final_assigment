using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ImageTrackingManager : MonoBehaviour
{
    private ARTrackedImageManager ourTrackedImages;
    public GameObject[] ourModelPrefabs;

    [Header("References")]
    public CombatManager combatManager;
    public GameStateManager gameStateManager;

    [Header("Tracking Settings")]
    public float scanCooldownSeconds = 1.5f;
    public float trackingLostGracePeriod = 2.0f;

    [Header("Demo Lock Settings")]
    public int maxCardsPerPlayer = 2;

    /*
     * Player-specific card keys:
     *
     * 0:HoodedRouge = Player 1's Hooded Rogue
     * 1:Paladin     = Player 2's Paladin
     *
     * This prevents both players from sharing the same CardCharacter instance.
     */
    private readonly Dictionary<string, GameObject> spawnedObjects =
        new Dictionary<string, GameObject>();

    private readonly Dictionary<string, CardCharacter> spawnedCharacters =
        new Dictionary<string, CardCharacter>();

    /*
     * Prevents the same marker from triggering placement every frame.
     * Key is playerIndex:cardName.
     */
    private readonly Dictionary<string, float> scanCooldowns =
        new Dictionary<string, float>();

    /*
     * Tracks temporary loss of image tracking.
     * Key is playerIndex:cardName.
     */
    private readonly Dictionary<string, float> lostTimers =
        new Dictionary<string, float>();

    /*
     * Tracks ownership of a physical AR marker.
     *
     * Example:
     * trackable id "ABC123" -> "0:HoodedRouge"
     *
     * This prevents Player 1's already-placed marker from becoming
     * Player 2's card when the turn changes.
     */
    private readonly Dictionary<string, string> claimedTrackables =
        new Dictionary<string, string>();

    void Awake()
    {
        Debug.Log("[ITM VERSION] DEMO_LOCKED_4_UNIQUE_CARDS_V1");

        ourTrackedImages = GetComponent<ARTrackedImageManager>();

        if (gameStateManager == null)
            gameStateManager = FindObjectOfType<GameStateManager>();

        if (combatManager == null)
            combatManager = FindObjectOfType<CombatManager>();
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
        // Cooldowns stop repeated scans every frame.
        foreach (string key in new List<string>(scanCooldowns.Keys))
        {
            scanCooldowns[key] -= Time.deltaTime;

            if (scanCooldowns[key] <= 0f)
                scanCooldowns.Remove(key);
        }

        // Tracking-lost grace timer.
        foreach (string key in new List<string>(lostTimers.Keys))
        {
            lostTimers[key] -= Time.deltaTime;

            if (lostTimers[key] <= 0f)
            {
                lostTimers.Remove(key);

                if (spawnedCharacters.TryGetValue(key, out CardCharacter character))
                {
                    if (character == null) continue;

                    /*
                     * Do not hide placed field cards.
                     * Once a card is placed, it should stay visible even
                     * if AR tracking for the marker is lost.
                     */
                    bool isOnField =
                        gameStateManager != null &&
                        gameStateManager.Player1 != null &&
                        gameStateManager.Player2 != null &&
                        (
                            gameStateManager.Player1.FieldCards.Contains(character) ||
                            gameStateManager.Player2.FieldCards.Contains(character)
                        );

                    if (!isOnField)
                    {
                        character.gameObject.SetActive(false);
                        Debug.Log($"[ITM] Hiding {key} after tracking was lost.");
                    }
                }
            }
        }
    }

    private void WhenTrackedImagesChange(ARTrackedImagesChangedEventArgs eventArgs)
    {
        foreach (ARTrackedImage trackedImage in eventArgs.added)
        {
            TryHandleTrackedImage(trackedImage, "ADDED");
        }

        foreach (ARTrackedImage trackedImage in eventArgs.updated)
        {
            HandleUpdatedTrackedImage(trackedImage);
        }

        foreach (ARTrackedImage trackedImage in eventArgs.removed)
        {
            HandleRemovedTrackedImage(trackedImage);
        }
    }

    private void HandleUpdatedTrackedImage(ARTrackedImage trackedImage)
    {
        if (trackedImage == null) return;

        string cardName = trackedImage.referenceImage.name;
        string trackableKey = MakeTrackableKey(trackedImage);

        if (trackedImage.trackingState == TrackingState.Tracking)
        {
            /*
             * If this physical marker is already claimed, only the same
             * player/card key may interact with it.
             */
            if (claimedTrackables.TryGetValue(trackableKey, out string claimedByKey))
            {
                lostTimers.Remove(claimedByKey);

                string currentKey = GetCurrentPlayerKey(cardName);

                if (currentKey != claimedByKey)
                {
                    Debug.Log(
                        $"[ITM] Ignoring tracked marker {cardName}. " +
                        $"Physical marker is claimed by {claimedByKey}, current key is {currentKey}."
                    );

                    return;
                }
            }

            TryHandleTrackedImage(trackedImage, "UPDATED");
        }
        else
        {
            if (claimedTrackables.TryGetValue(trackableKey, out string claimedByKey))
            {
                if (!lostTimers.ContainsKey(claimedByKey))
                {
                    lostTimers[claimedByKey] = trackingLostGracePeriod;
                    Debug.Log($"[ITM] Tracking lost: {claimedByKey}.");
                }
            }
        }
    }

    private void HandleRemovedTrackedImage(ARTrackedImage trackedImage)
    {
        if (trackedImage == null) return;

        string cardName = trackedImage.referenceImage.name;
        string trackableKey = MakeTrackableKey(trackedImage);

        Debug.Log($"[ITM] REMOVED event: {cardName}");

        /*
         * Do NOT remove claimedTrackables here.
         * If you remove the claim immediately, the same physical marker can
         * come back during the other player's turn and get claimed by them.
         */
        if (claimedTrackables.TryGetValue(trackableKey, out string claimedByKey))
        {
            if (!lostTimers.ContainsKey(claimedByKey))
                lostTimers[claimedByKey] = trackingLostGracePeriod;
        }
    }

    private void TryHandleTrackedImage(ARTrackedImage trackedImage, string eventType)
    {
        if (trackedImage == null) return;
        if (trackedImage.trackingState != TrackingState.Tracking) return;

        if (gameStateManager == null)
        {
            Debug.LogWarning("[ITM] Missing GameStateManager reference.");
            return;
        }

        PlayerState currentPlayer = gameStateManager.CurrentPlayer;

        if (currentPlayer == null)
        {
            Debug.LogWarning("[ITM] No current player.");
            return;
        }

        if (gameStateManager.CurrentPhase != GameStateManager.GamePhase.PlayerTurn)
            return;

        string cardName = trackedImage.referenceImage.name;
        int playerIndex = currentPlayer.PlayerIndex;

        string key = MakeKey(playerIndex, cardName);
        string trackableKey = MakeTrackableKey(trackedImage);

        Debug.Log(
            $"[ITM] {eventType} event: {cardName} | " +
            $"player:{currentPlayer.PlayerName} | key:{key} | trackable:{trackableKey}"
        );

        /*
         * DEMO-SAFE PLAYER CARD LOCK.
         *
         * Player 1:
         * - HoodedRouge
         * - BirdKnight
         *
         * Player 2:
         * - ArmoredGuard
         * - Paladin
         */
        if (!IsCardAllowedForPlayer(playerIndex, cardName))
        {
            Debug.Log(
                $"[ITM] Ignoring {cardName}. It is not allowed for {currentPlayer.PlayerName}."
            );
            return;
        }

        /*
         * Physical marker ownership check.
         */
        if (claimedTrackables.TryGetValue(trackableKey, out string claimedByKey))
        {
            if (claimedByKey != key)
            {
                Debug.Log(
                    $"[ITM] Ignoring {cardName}. This physical marker is already claimed by {claimedByKey}, " +
                    $"but current player would claim it as {key}."
                );

                return;
            }
        }

        if (scanCooldowns.ContainsKey(key))
            return;

        /*
         * Existing card for this player.
         * This mostly matters for revived cards.
         */
        if (spawnedCharacters.TryGetValue(key, out CardCharacter existingCharacter))
        {
            if (existingCharacter == null)
            {
                spawnedCharacters.Remove(key);
                spawnedObjects.Remove(key);
                return;
            }

            if (currentPlayer.HandCards.Contains(existingCharacter))
            {
                if (!currentPlayer.CanAfford(PlayerState.PlaceCost))
                {
                    Debug.Log($"[ITM] {currentPlayer.PlayerName} cannot afford to re-place {cardName}.");
                    return;
                }

                Debug.Log($"[ITM] Re-placing existing {cardName} for {currentPlayer.PlayerName}.");

                MoveExistingCardToMarker(existingCharacter, trackedImage);

                scanCooldowns[key] = scanCooldownSeconds;
                claimedTrackables[trackableKey] = key;

                gameStateManager.OnCardScanned(existingCharacter);
                return;
            }

            if (currentPlayer.FieldCards.Contains(existingCharacter))
            {
                Debug.Log($"[ITM] Ignoring {cardName} — already on {currentPlayer.PlayerName}'s field.");
                return;
            }

            if (currentPlayer.DeadCards.Contains(existingCharacter))
            {
                Debug.Log($"[ITM] Ignoring {cardName} — it is dead. Revive it first.");
                return;
            }

            Debug.Log($"[ITM] Ignoring {cardName} — existing object found but not in hand/field/dead.");
            return;
        }

        /*
         * Demo lock: only two cards per player.
         * This prevents accidental third-card placement if AR misreads something.
         */
        if (currentPlayer.FieldCards.Count >= maxCardsPerPlayer)
        {
            Debug.Log(
                $"[ITM] Ignoring {cardName}. {currentPlayer.PlayerName} already has " +
                $"{currentPlayer.FieldCards.Count}/{maxCardsPerPlayer} cards on the field."
            );
            return;
        }

        /*
         * New card for this player.
         */
        if (!currentPlayer.CanAfford(PlayerState.PlaceCost))
        {
            Debug.Log($"[ITM] {currentPlayer.PlayerName} cannot afford to place {cardName}.");
            return;
        }

        GameObject matchingPrefab = FindMatchingPrefab(cardName);

        Debug.Log(
            $"[ITM CHECK] Marker detected: {cardName} | " +
            $"Prefab chosen: {(matchingPrefab != null ? matchingPrefab.name : "NULL")}"
        );

        if (matchingPrefab == null)
        {
            Debug.LogWarning(
                $"[ITM] No prefab found for marker '{cardName}'. " +
                $"Make sure prefab.name exactly matches the reference image name."
            );
            return;
        }

        Debug.Log($"[ITM] Spawning NEW {cardName} for {currentPlayer.PlayerName}.");

        GameObject spawned = Instantiate(
            matchingPrefab,
            trackedImage.transform.position,
            trackedImage.transform.rotation
        );

        // Keep your original 180-degree model flip.
        spawned.transform.rotation *= Quaternion.Euler(0f, 180f, 0f);

        CardCharacter character = spawned.GetComponent<CardCharacter>();

        Debug.Log(
            $"[ITM CHECK] Spawned object: {spawned.name} | " +
            $"CardCharacter name: {(character != null ? character.characterName : "NULL")}"
        );

        if (character == null)
        {
            Debug.LogWarning($"[ITM] Spawned prefab '{cardName}' has no CardCharacter component.");
            Destroy(spawned);
            return;
        }

        character.SetOwner(playerIndex);

        spawnedObjects[key] = spawned;
        spawnedCharacters[key] = character;
        scanCooldowns[key] = scanCooldownSeconds;
        claimedTrackables[trackableKey] = key;

        if (combatManager != null && !combatManager.characters.Contains(character))
            combatManager.characters.Add(character);

        gameStateManager.OnCardScanned(character);
    }

    private bool IsCardAllowedForPlayer(int playerIndex, string cardName)
    {
        // Player 1's demo deck
        if (playerIndex == 0)
        {
            return cardName == "HoodedRouge" ||
                   cardName == "BirdKnight";
        }

        // Player 2's demo deck
        if (playerIndex == 1)
        {
            return cardName == "ArmoredGuard" ||
                   cardName == "Paladin";
        }

        return false;
    }

    private void MoveExistingCardToMarker(CardCharacter character, ARTrackedImage trackedImage)
    {
        if (character == null || trackedImage == null) return;

        GameObject go = character.gameObject;

        go.transform.SetParent(null, true);
        go.transform.position = trackedImage.transform.position;
        go.transform.rotation = trackedImage.transform.rotation * Quaternion.Euler(0f, 180f, 0f);

        go.SetActive(true);
    }

    private string MakeKey(int playerIndex, string cardName)
    {
        return $"{playerIndex}:{cardName}";
    }

    private string MakeTrackableKey(ARTrackedImage trackedImage)
    {
        return trackedImage.trackableId.ToString();
    }

    private string GetCurrentPlayerKey(string cardName)
    {
        if (gameStateManager == null) return null;
        if (gameStateManager.CurrentPlayer == null) return null;

        return MakeKey(gameStateManager.CurrentPlayer.PlayerIndex, cardName);
    }

    private GameObject FindMatchingPrefab(string cardName)
    {
        foreach (GameObject prefab in ourModelPrefabs)
        {
            if (prefab != null && prefab.name == cardName)
                return prefab;
        }

        return null;
    }

    /*
     * GameStateManager still calls this after successful placement.
     * Keep it so GameStateManager compiles.
     *
     * Cards are already instantiated into world space, so this just makes
     * absolutely sure the card is independent from AR tracking.
     */
    public void DetachPlacedCard(CardCharacter character)
    {
        if (character == null) return;

        character.transform.SetParent(null, true);

        Debug.Log($"[ITM] {character.characterName} placed as independent world object.");
    }
}