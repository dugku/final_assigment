using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Manages all in-game UI for the turn-based AR card game.
/// Supports two-attack selection flow:
/// 1. Tap your card → Attack 1 / Attack 2 buttons appear
/// 2. Pick an attack → enemy targets appear
/// 3. Tap a target → Confirm button appears
/// </summary>
public class GameUI : MonoBehaviour
{
    // ── Panels ─────────────────────────────────────────────────────────────
    [Header("Panels")]
    public GameObject passDevicePanel;
    public GameObject turnPanel;
    public GameObject endGamePanel;

    // ── Pass Device ────────────────────────────────────────────────────────
    [Header("Pass Device")]
    public TextMeshProUGUI passDeviceText;
    public Button          passDeviceButton;

    // ── Turn Panel ─────────────────────────────────────────────────────────
    [Header("Turn Panel")]
    public TextMeshProUGUI turnBannerText;
    public TextMeshProUGUI currentPlayerHP;
    public TextMeshProUGUI currentPlayerMana;
    public TextMeshProUGUI opponentPlayerHP;
    public TextMeshProUGUI instructionsText;

    // Your field cards
    public Transform  fieldCardContainer;
    public GameObject cardButtonPrefab;

    // Attack selection panel — appears after tapping your card
    public GameObject attackSelectionPanel;
    public Button     attack1Button;
    public Button     attack2Button;
    public TextMeshProUGUI attack1Label;
    public TextMeshProUGUI attack2Label;

    // Enemy cards panel
    public Transform  enemyCardContainer;
    public GameObject enemyCardPanel;

    // Dead cards
    public Transform  deadCardContainer;
    public GameObject deadCardPanel;

    // Buttons
    public Button confirmAttackButton;
    public Button endTurnButton;

    // ── End Game ───────────────────────────────────────────────────────────
    [Header("End Game")]
    public TextMeshProUGUI winnerText;
    public Button          restartButton;

    // ── Toast ──────────────────────────────────────────────────────────────
    [Header("Toast")]
    public TextMeshProUGUI messageText;
    private Coroutine messageCoroutine;

    // ── Internal ───────────────────────────────────────────────────────────
    private GameStateManager gsm;
    private PlayerState      currentPlayer;
    private bool             initialized = false;
    private PlayerState      pendingPlayer;
    private bool             pendingAttackUnlocked;
    private bool             hasPendingTurn = false;

    void Awake() => HideAllPanels();

    void Start()
    {
        gsm = GameStateManager.Instance;
        if (gsm == null) gsm = FindObjectOfType<GameStateManager>();

        if (gsm == null)
        {
            Debug.LogError("[GameUI] Could not find GameStateManager!");
            return;
        }

        if (passDeviceButton)    passDeviceButton.onClick.AddListener(OnPassDeviceConfirmed);
        if (confirmAttackButton) confirmAttackButton.onClick.AddListener(() => gsm?.OnConfirmAttackPressed());
        if (endTurnButton)       endTurnButton.onClick.AddListener(() => gsm?.OnEndTurnPressed());

        // Wire attack slot buttons
        if (attack1Button) attack1Button.onClick.AddListener(() => gsm?.OnSelectAttackSlot(1));
        if (attack2Button) attack2Button.onClick.AddListener(() => gsm?.OnSelectAttackSlot(2));

        initialized = true;
        Debug.Log("[GameUI] Initialized.");

        if (hasPendingTurn)
        {
            hasPendingTurn = false;
            OnBeginTurn(pendingPlayer, pendingAttackUnlocked);
        }
    }

    // ── Called by GameStateManager ─────────────────────────────────────────

    public void OnBeginTurn(PlayerState player, bool attackUnlocked)
    {
        if (!initialized)
        {
            pendingPlayer         = player;
            pendingAttackUnlocked = attackUnlocked;
            hasPendingTurn        = true;
            return;
        }

        currentPlayer = player;
        HideAllPanels();
        turnPanel?.SetActive(true);

        UpdateStats();
        BuildTurnPanel(attackUnlocked);

        string attackStatus = attackUnlocked
            ? "Attack available — tap a card"
            : "Place cards to unlock attacks";

        SetTurnBanner($"{player.PlayerName}'s Turn");
        SetInstructions($"Scan card to place.  {attackStatus}");

        attackSelectionPanel?.SetActive(false);
        confirmAttackButton?.gameObject.SetActive(false);
        enemyCardPanel?.SetActive(false);

        Debug.Log($"[GameUI] Turn panel shown for {player.PlayerName}");
    }

    public void OnPassDevice(PlayerState nextPlayer)
    {
        if (!initialized) return;
        HideAllPanels();
        passDevicePanel?.SetActive(true);
        if (passDeviceText != null)
            passDeviceText.text = $"Pass the device to\n{nextPlayer.PlayerName}";
    }

    public void OnEndGame(PlayerState winner)
    {
        if (!initialized) return;
        HideAllPanels();
        endGamePanel?.SetActive(true);
        if (winnerText != null)
            winnerText.text = winner != null ? $"{winner.PlayerName} Wins!" : "Draw!";
    }

    public void RefreshUI(PlayerState player, bool attackUnlocked)
    {
        if (!initialized) return;
        currentPlayer = player;
        UpdateStats();
        BuildTurnPanel(attackUnlocked);
        attackSelectionPanel?.SetActive(false);
        enemyCardPanel?.SetActive(false);
        confirmAttackButton?.gameObject.SetActive(false);
    }

    /// <summary>Step 1 — Card tapped, show Attack 1 / Attack 2 buttons.</summary>
    public void OnAttackerSelected(CardCharacter card)
    {
        if (!initialized) return;

        // Update attack button labels with name, damage and mana cost
        if (attack1Label != null)
            attack1Label.text = $"{card.attack1Name}\n{card.attack1Damage} dmg  |  {card.attack1ManaCost} mana";
        if (attack2Label != null)
            attack2Label.text = $"{card.attack2Name}\n{card.attack2Damage} dmg  |  {card.attack2ManaCost} mana";

        // Grey out attacks player can't afford
        if (attack1Button != null)
            attack1Button.interactable = gsm.CurrentPlayer.CanAfford(card.attack1ManaCost);
        if (attack2Button != null)
            attack2Button.interactable = gsm.CurrentPlayer.CanAfford(card.attack2ManaCost);

        attackSelectionPanel?.SetActive(true);
        enemyCardPanel?.SetActive(false);
        confirmAttackButton?.gameObject.SetActive(false);

        SetInstructions($"{card.characterName} — choose an attack");
    }

    /// <summary>Step 2 — Attack chosen, show enemy targets.</summary>
    public void OnAttackSlotSelected(int slot, string attackName, float damage, int manaCost)
    {
        attackSelectionPanel?.SetActive(false);
        enemyCardPanel?.SetActive(true);
        BuildEnemyCards();
        SetInstructions($"{attackName} ({damage} dmg) — select a target");
        confirmAttackButton?.gameObject.SetActive(false);
    }

    /// <summary>Step 3 — Target chosen, show Confirm button.</summary>
    public void OnTargetSelected(CardCharacter card)
    {
        if (!initialized) return;
        SetInstructions($"Attack {card.characterName}? Tap Confirm!");
        confirmAttackButton?.gameObject.SetActive(true);
    }

    public void ShowMessage(string message)
    {
        if (messageText == null) return;
        if (messageCoroutine != null) StopCoroutine(messageCoroutine);
        messageCoroutine = StartCoroutine(ShowMessageCoroutine(message));
    }

    // ── Internal builders ──────────────────────────────────────────────────

    private void OnPassDeviceConfirmed() => gsm?.OnPassDeviceConfirmed();

    private void BuildTurnPanel(bool attackUnlocked)
    {
        if (currentPlayer == null) return;

        BuildCardButtons(fieldCardContainer, currentPlayer.FieldCards,
            card => gsm?.OnSelectAttacker(card), "Tap to Attack", attackUnlocked);

        bool hasDead = currentPlayer.DeadCards.Count > 0;
        deadCardPanel?.SetActive(hasDead);
        if (hasDead)
            BuildCardButtons(deadCardContainer, currentPlayer.DeadCards,
                card => gsm?.OnReviveCardPressed(card), "Revive", true);
    }

    private void BuildEnemyCards()
    {
        if (gsm == null) return;
        PlayerState opponent = gsm.OpponentPlayer;

        if (opponent.HasCardsOnField())
        {
            BuildCardButtons(enemyCardContainer, opponent.FieldCards,
                card => gsm.OnSelectTarget(card), "Target", true);
        }
        else
        {
            ClearContainer(enemyCardContainer);
            if (cardButtonPrefab != null && enemyCardContainer != null)
            {
                GameObject btn = Instantiate(cardButtonPrefab, enemyCardContainer);
                SetCardButtonText(btn, opponent.PlayerName, "Direct Attack");
                btn.GetComponent<Button>()?.onClick.AddListener(
                    () => gsm?.OnConfirmAttackPressed());
            }
        }
    }

    private void BuildCardButtons(Transform container, List<CardCharacter> cards,
        System.Action<CardCharacter> callback, string label, bool interactable)
    {
        if (container == null || cardButtonPrefab == null) return;
        ClearContainer(container);
        foreach (CardCharacter card in cards)
        {
            GameObject btn = Instantiate(cardButtonPrefab, container);
            SetCardButtonText(btn, card.characterName, label);
            Button b = btn.GetComponent<Button>();
            if (b != null)
            {
                b.interactable = interactable;
                CardCharacter captured = card;
                b.onClick.AddListener(() => callback(captured));
            }
        }
    }

    private void SetCardButtonText(GameObject btn, string title, string subtitle)
    {
        TextMeshProUGUI[] texts = btn.GetComponentsInChildren<TextMeshProUGUI>();
        if (texts.Length >= 1) texts[0].text = title;
        if (texts.Length >= 2) texts[1].text = subtitle;
    }

    private void ClearContainer(Transform container)
    {
        if (container == null) return;
        foreach (Transform child in container)
            Destroy(child.gameObject);
    }

    private void UpdateStats()
    {
        if (gsm == null || currentPlayer == null) return;
        PlayerState opponent = gsm.OpponentPlayer;
        if (currentPlayerHP   != null) currentPlayerHP.text   = $"HP: {currentPlayer.CurrentHP}/{PlayerState.MaxHP}";
        if (currentPlayerMana != null) currentPlayerMana.text = $"Mana: {currentPlayer.CurrentMana}/{PlayerState.MaxMana}";
        if (opponentPlayerHP  != null) opponentPlayerHP.text  = $"{opponent.PlayerName}  HP: {opponent.CurrentHP}/{PlayerState.MaxHP}";
    }

    private void HideAllPanels()
    {
        passDevicePanel?.SetActive(false);
        turnPanel?.SetActive(false);
        endGamePanel?.SetActive(false);
        attackSelectionPanel?.SetActive(false);
        messageText?.gameObject.SetActive(false);
    }

    private void SetTurnBanner(string text) { if (turnBannerText != null) turnBannerText.text = text; }
    private void SetInstructions(string text) { if (instructionsText != null) instructionsText.text = text; }

    private IEnumerator ShowMessageCoroutine(string message)
    {
        if (messageText == null) yield break;
        messageText.gameObject.SetActive(true);
        messageText.text = message;
        yield return new WaitForSeconds(2.5f);
        messageText.gameObject.SetActive(false);
    }
}