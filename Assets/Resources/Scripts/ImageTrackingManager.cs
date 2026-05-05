using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARSubsystems;

public class ImageTrackingManager : MonoBehaviour
{
    private ARTrackedImageManager ourTrackedImages;

    [Header("Card Prefabs")]
    public GameObject[] ourModelPrefabs;

    [Header("References")]
    public CombatManager combatManager;
    public GameStateManager gameStateManager;

    [Header("Tracking Settings")]
    public float scanCooldownSeconds = 1.5f;
    public float trackingLostGracePeriod = 2.0f;

    [Header("Demo Lock Settings")]
    public int maxCardsPerPlayer = 2;

    private readonly Dictionary<string, GameObject> spawnedObjects = new Dictionary<string, GameObject>();
    private readonly Dictionary<string, CardCharacter> spawnedCharacters = new Dictionary<string, CardCharacter>();
    private readonly Dictionary<string, float> scanCooldowns = new Dictionary<string, float>();
    private readonly Dictionary<string, float> lostTimers = new Dictionary<string, float>();

    /*
     * This is intentionally strict again.
     * A physical marker claimed by Player 1 should NOT later become Player 2's card.
     */
    private readonly Dictionary<string, string> claimedTrackables = new Dictionary<string, string>();

    // Field cards are locked so AR image updates cannot move them.
    // Death/revive unlocks them so they can be scanned again.
    private readonly HashSet<string> lockedPlacedCards = new HashSet<string>();

    private const string VersionTag = "V5_REVERTED_DEMO_LOCK_SAFE";

    private void Awake()
    {
        Debug.Log($"[ITM VERSION] {VersionTag}");

        ourTrackedImages = GetComponent<ARTrackedImageManager>();

        if (ourTrackedImages == null)
        {
            Debug.LogError("[ITM] Missing ARTrackedImageManager on this GameObject.");
        }
        else if (ourTrackedImages.trackedImagePrefab != null)
        {
            Debug.LogWarning("[ITM] Clearing ARTrackedImageManager.trackedImagePrefab because this script spawns cards manually.");
            ourTrackedImages.trackedImagePrefab = null;
        }

        if (gameStateManager == null)
            gameStateManager = FindObjectOfType<GameStateManager>();

        if (combatManager == null)
            combatManager = FindObjectOfType<CombatManager>();
    }

    private void OnEnable()
    {
        if (ourTrackedImages != null)
            ourTrackedImages.trackedImagesChanged += WhenTrackedImagesChange;
    }

    private void OnDisable()
    {
        if (ourTrackedImages != null)
            ourTrackedImages.trackedImagesChanged -= WhenTrackedImagesChange;
    }

    private void Update()
    {
        TickScanCooldowns();
        TickLostTrackingTimers();
    }

    private void TickScanCooldowns()
    {
        foreach (string key in new List<string>(scanCooldowns.Keys))
        {
            scanCooldowns[key] -= Time.deltaTime;
            if (scanCooldowns[key] <= 0f)
                scanCooldowns.Remove(key);
        }
    }

    private void TickLostTrackingTimers()
    {
        foreach (string key in new List<string>(lostTimers.Keys))
        {
            lostTimers[key] -= Time.deltaTime;
            if (lostTimers[key] > 0f)
                continue;

            lostTimers.Remove(key);

            if (!spawnedCharacters.TryGetValue(key, out CardCharacter character))
                continue;

            if (character == null)
                continue;

            // Never hide field cards. Placed cards should stay visible even if tracking is lost.
            if (IsCardOnEitherField(character))
                continue;

            character.gameObject.SetActive(false);
            Debug.Log($"[ITM] Hiding {key} after tracking was lost.");
        }
    }

    private void WhenTrackedImagesChange(ARTrackedImagesChangedEventArgs eventArgs)
    {
        foreach (ARTrackedImage trackedImage in eventArgs.added)
            TryHandleTrackedImage(trackedImage, "ADDED");

        foreach (ARTrackedImage trackedImage in eventArgs.updated)
            HandleUpdatedTrackedImage(trackedImage);

        foreach (ARTrackedImage trackedImage in eventArgs.removed)
            HandleRemovedTrackedImage(trackedImage);
    }

    private void HandleUpdatedTrackedImage(ARTrackedImage trackedImage)
    {
        if (trackedImage == null)
            return;

        string trackableKey = MakeTrackableKey(trackedImage);

        if (trackedImage.trackingState != TrackingState.Tracking)
        {
            if (claimedTrackables.TryGetValue(trackableKey, out string claimedByKey))
            {
                if (!lostTimers.ContainsKey(claimedByKey))
                    lostTimers[claimedByKey] = trackingLostGracePeriod;
            }

            return;
        }

        if (gameStateManager == null || gameStateManager.CurrentPlayer == null)
            return;

        string cardName = NormalizeCardName(trackedImage.referenceImage.name);
        PlayerState currentPlayer = gameStateManager.CurrentPlayer;
        int playerIndex = currentPlayer.PlayerIndex;
        string key = MakeKey(playerIndex, cardName);

        /*
         * Strict ownership stays on.
         * If the physical marker belongs to the other player, ignore it.
         * If it belongs to this same key and the card is in hand after revive,
         * allow it to be scanned again.
         */
        if (claimedTrackables.TryGetValue(trackableKey, out string claimedByExistingKey))
        {
            if (claimedByExistingKey != key)
            {
                lostTimers.Remove(claimedByExistingKey);
                return;
            }

            if (spawnedCharacters.TryGetValue(key, out CardCharacter existingCharacter))
            {
                if (IsCardOnEitherField(existingCharacter))
                {
                    lostTimers.Remove(key);
                    return;
                }

                if (!currentPlayer.HandCards.Contains(existingCharacter))
                    return;
            }
        }

        if (lockedPlacedCards.Contains(key))
            return;

        if (scanCooldowns.ContainsKey(key))
            return;

        if (!IsCardAllowedForPlayer(playerIndex, cardName))
            return;

        TryHandleTrackedImage(trackedImage, "UPDATED");
    }

    private void HandleRemovedTrackedImage(ARTrackedImage trackedImage)
    {
        if (trackedImage == null)
            return;

        string cardName = NormalizeCardName(trackedImage.referenceImage.name);
        string trackableKey = MakeTrackableKey(trackedImage);

        Debug.Log($"[ITM] REMOVED event: {cardName}");

        // Do not remove claimedTrackables here.
        // Otherwise the same physical marker can be re-claimed by the other player.
        if (claimedTrackables.TryGetValue(trackableKey, out string claimedByKey))
        {
            if (!lostTimers.ContainsKey(claimedByKey))
                lostTimers[claimedByKey] = trackingLostGracePeriod;
        }
    }

    private void TryHandleTrackedImage(ARTrackedImage trackedImage, string eventType)
    {
        if (trackedImage == null)
            return;

        if (trackedImage.trackingState != TrackingState.Tracking)
            return;

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

        string rawCardName = trackedImage.referenceImage.name;
        string cardName = NormalizeCardName(rawCardName);
        int playerIndex = currentPlayer.PlayerIndex;
        string key = MakeKey(playerIndex, cardName);
        string trackableKey = MakeTrackableKey(trackedImage);

        Debug.Log($"[ITM] {eventType} event: raw:{rawCardName} normalized:{cardName} | player:{currentPlayer.PlayerName} | key:{key} | trackable:{trackableKey}");

        if (lockedPlacedCards.Contains(key))
        {
            Debug.Log($"[ITM] Ignoring {cardName}. {key} is already world-locked.");
            return;
        }

        if (!IsCardAllowedForPlayer(playerIndex, cardName))
        {
            Debug.Log($"[ITM] Ignoring {cardName}. It is not allowed for {currentPlayer.PlayerName}.");
            return;
        }

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

        if (spawnedCharacters.TryGetValue(key, out CardCharacter existingCharacter))
        {
            HandleExistingCharacterScan(existingCharacter, trackedImage, currentPlayer, cardName, key, trackableKey);
            return;
        }

        if (currentPlayer.FieldCards.Count >= maxCardsPerPlayer)
        {
            Debug.Log($"[ITM] Ignoring {cardName}. {currentPlayer.PlayerName} already has {currentPlayer.FieldCards.Count}/{maxCardsPerPlayer} cards on field.");
            return;
        }

        if (!currentPlayer.CanAfford(PlayerState.PlaceCost))
        {
            Debug.Log($"[ITM] {currentPlayer.PlayerName} cannot afford to place {cardName}.");
            return;
        }

        SpawnNewCard(trackedImage, currentPlayer, cardName, key, trackableKey);
    }

    private void HandleExistingCharacterScan(CardCharacter existingCharacter, ARTrackedImage trackedImage, PlayerState currentPlayer, string cardName, string key, string trackableKey)
    {
        if (existingCharacter == null)
        {
            spawnedCharacters.Remove(key);
            spawnedObjects.Remove(key);
            lockedPlacedCards.Remove(key);
            return;
        }

        if (IsCardOnEitherField(existingCharacter))
        {
            lockedPlacedCards.Add(key);
            Debug.Log($"[ITM] Ignoring {cardName} — already on field and locked.");
            return;
        }

        if (currentPlayer.DeadCards.Contains(existingCharacter))
        {
            Debug.Log($"[ITM] Ignoring {cardName} — dead card must be revived first.");
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
            LockPlacedCard(existingCharacter, key);
            return;
        }

        Debug.Log($"[ITM] Ignoring {cardName} — existing object found but not in hand/field/dead.");
    }

    private void SpawnNewCard(ARTrackedImage trackedImage, PlayerState currentPlayer, string cardName, string key, string trackableKey)
    {
        GameObject matchingPrefab = FindMatchingPrefab(cardName);

        Debug.Log($"[ITM CHECK] Marker detected: {cardName} | Prefab chosen: {(matchingPrefab != null ? matchingPrefab.name : "NULL")}");

        if (matchingPrefab == null)
        {
            Debug.LogWarning($"[ITM] No prefab found for normalized marker '{cardName}'. Check prefab names in ourModelPrefabs.");
            return;
        }

        GameObject spawned = Instantiate(matchingPrefab, trackedImage.transform.position, trackedImage.transform.rotation);
        spawned.name = $"{cardName}_{currentPlayer.PlayerName}";
        spawned.transform.SetParent(null, true);
        spawned.transform.rotation *= Quaternion.Euler(0f, 180f, 0f);

        CardCharacter character = spawned.GetComponent<CardCharacter>();

        Debug.Log($"[ITM CHECK] Spawned object: {spawned.name} | CardCharacter name: {(character != null ? character.characterName : "NULL")}");

        if (character == null)
        {
            Debug.LogWarning($"[ITM] Spawned prefab '{cardName}' has no CardCharacter component on the root.");
            Destroy(spawned);
            return;
        }

        character.SetOwner(currentPlayer.PlayerIndex);

        spawnedObjects[key] = spawned;
        spawnedCharacters[key] = character;
        scanCooldowns[key] = scanCooldownSeconds;
        claimedTrackables[trackableKey] = key;

        if (combatManager != null && !combatManager.characters.Contains(character))
            combatManager.characters.Add(character);

        gameStateManager.OnCardScanned(character);
        LockPlacedCard(character, key);
    }

    private void MoveExistingCardToMarker(CardCharacter character, ARTrackedImage trackedImage)
    {
        if (character == null || trackedImage == null)
            return;

        if (IsCardOnEitherField(character))
        {
            Debug.LogWarning($"[ITM] Blocked teleport attempt for field card: {character.characterName}");
            return;
        }

        GameObject go = character.gameObject;
        go.SetActive(true);
        go.transform.SetParent(null, true);
        go.transform.position = trackedImage.transform.position;
        go.transform.rotation = trackedImage.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
    }

    private void LockPlacedCard(CardCharacter character, string key)
    {
        if (character == null)
            return;

        character.transform.SetParent(null, true);
        lockedPlacedCards.Add(key);
        lostTimers.Remove(key);

        Debug.Log($"[ITM] {character.characterName} world-locked. key={key} pos={character.transform.position} rot={character.transform.rotation.eulerAngles}");
    }

    public void DetachPlacedCard(CardCharacter character)
    {
        if (character == null)
            return;

        character.transform.SetParent(null, true);

        string key = FindKeyForCharacter(character);
        if (!string.IsNullOrEmpty(key))
            LockPlacedCard(character, key);
        else
            Debug.LogWarning($"[ITM] DetachPlacedCard could not find key for {character.characterName}.");
    }

    public void OnCardDied(CardCharacter character)
    {
        string key = FindKeyForCharacter(character);
        if (string.IsNullOrEmpty(key))
            return;

        lockedPlacedCards.Remove(key);
        lostTimers.Remove(key);
        Debug.Log($"[ITM] {character.characterName} died. Unlocked {key} so it can be revived/re-scanned later.");
    }

    public void UnlockForRevive(CardCharacter character)
    {
        string key = FindKeyForCharacter(character);
        if (string.IsNullOrEmpty(key))
        {
            Debug.LogWarning($"[ITM] UnlockForRevive could not find key for {character?.characterName ?? "NULL"}.");
            return;
        }

        lockedPlacedCards.Remove(key);
        scanCooldowns.Remove(key);
        lostTimers.Remove(key);

        // Do not call ResetForNewGame here.
        // Revive moves the card back to hand; scanning places it and PlayerState.TryPlaceCard resets/shows it.
        if (character != null)
            character.gameObject.SetActive(true);

        Debug.Log($"[ITM] Revive unlocked {key}. Scan the physical card to place it again.");
    }

    public void ClearAllSpawnedCards()
    {
        foreach (GameObject go in spawnedObjects.Values)
        {
            if (go != null)
                Destroy(go);
        }

        // Safety net: destroy any card objects that were not in the dictionary.
        foreach (CardCharacter character in FindObjectsOfType<CardCharacter>(true))
        {
            if (character != null)
                Destroy(character.gameObject);
        }

        spawnedObjects.Clear();
        spawnedCharacters.Clear();
        scanCooldowns.Clear();
        lostTimers.Clear();
        claimedTrackables.Clear();
        lockedPlacedCards.Clear();

        if (combatManager != null)
            combatManager.characters.Clear();

        Debug.Log("[ITM] Cleared all spawned card objects and tracking dictionaries.");
    }

    private bool IsCardOnEitherField(CardCharacter character)
    {
        if (character == null || gameStateManager == null)
            return false;

        bool p1 = gameStateManager.Player1 != null && gameStateManager.Player1.FieldCards.Contains(character);
        bool p2 = gameStateManager.Player2 != null && gameStateManager.Player2.FieldCards.Contains(character);
        return p1 || p2;
    }

    private bool IsCardAllowedForPlayer(int playerIndex, string cardName)
    {
        // Demo-safe lock. This prevents Player 1's visible marker from becoming Player 2's card.
        if (playerIndex == 0)
        {
            return cardName == "HoodedRouge" ||
                   cardName == "BirdKnight";
        }

        if (playerIndex == 1)
        {
            return cardName == "ArmoredGuard" ||
                   cardName == "Paladin";
        }

        return false;
    }

    private string NormalizeCardName(string rawName)
    {
        if (string.IsNullOrEmpty(rawName))
            return rawName;

        if (rawName.Contains("HoodedRouge") || rawName.Contains("HoodedRogue")) return "HoodedRouge";
        if (rawName.Contains("BirdKnight")) return "BirdKnight";
        if (rawName.Contains("ArmoredGuard")) return "ArmoredGuard";
        if (rawName.Contains("Paladin")) return "Paladin";

        int underscoreIndex = rawName.IndexOf('_');
        if (underscoreIndex > 0)
            return rawName.Substring(0, underscoreIndex);

        return rawName;
    }

    private string MakeKey(int playerIndex, string cardName)
    {
        return $"{playerIndex}:{cardName}";
    }

    private string MakeTrackableKey(ARTrackedImage trackedImage)
    {
        return trackedImage.trackableId.ToString();
    }

    private string FindKeyForCharacter(CardCharacter character)
    {
        if (character == null)
            return null;

        foreach (KeyValuePair<string, CardCharacter> pair in spawnedCharacters)
        {
            if (pair.Value == character)
                return pair.Key;
        }

        return null;
    }

    private GameObject FindMatchingPrefab(string cardName)
    {
        foreach (GameObject prefab in ourModelPrefabs)
        {
            if (prefab == null)
                continue;

            string prefabCardName = NormalizeCardName(prefab.name);
            if (prefabCardName == cardName)
                return prefab;
        }

        return null;
    }
}
