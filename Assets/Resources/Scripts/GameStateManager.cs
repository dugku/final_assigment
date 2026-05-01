using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Central state machine for the turn-based AR card game.
/// 
/// FLOW:
/// - Turn 1 (P1): placement only, no attacking
/// - Turn 2 (P2): placement only, no attacking
/// - Turn 3+ : both players can place AND attack freely
/// - Attacking also requires both players to have at least 1 card on field
/// </summary>
public class GameStateManager : MonoBehaviour
{
    public enum GamePhase
    {
        PlayerTurn,
        PassDevice,
        EndGame
    }

    [Header("References")]
    public CombatManager        combatManager;
    public GameUI               gameUI;
    public ImageTrackingManager imageTrackingManager;

    public PlayerState Player1 { get; private set; }
    public PlayerState Player2 { get; private set; }

    public GamePhase   CurrentPhase   { get; private set; }
    public PlayerState CurrentPlayer  { get; private set; }
    public PlayerState OpponentPlayer => CurrentPlayer == Player1 ? Player2 : Player1;

    public bool AttackingUnlocked { get; private set; } = false;
    public int  TurnNumber        { get; private set; } = 0;

    private bool player1HasHadFirstTurn = false;
    private bool player2HasHadFirstTurn = false;

    public CardCharacter SelectedAttacker   { get; private set; }
    public CardCharacter SelectedTarget     { get; private set; }
    public int           SelectedAttackSlot { get; private set; } = 0;

    public static GameStateManager Instance { get; private set; }

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        Player1 = new PlayerState(0);
        Player2 = new PlayerState(1);
    }

    void Start()
    {
        StartGame();
        gameUI?.OnBeginTurn(CurrentPlayer, AttackingUnlocked);
    }

    private void StartGame()
    {
        Debug.Log("=== GAME STARTED ===");

        CurrentPlayer = Player1;
        BeginTurn();
    }

    private void BeginTurn()
    {
        TurnNumber++;
        CurrentPhase = GamePhase.PlayerTurn;

        CurrentPlayer.StartTurn();

        CheckAttackUnlock();

        // FIX: update facing at the start of each turn.
        UpdateCardFacing();

        string phaseDesc = AttackingUnlocked ? "Place + Attack" : "Placement only";
        Debug.Log($"[GameStateManager] Turn {TurnNumber} — {CurrentPlayer.PlayerName} | {phaseDesc}");

        gameUI?.OnBeginTurn(CurrentPlayer, AttackingUnlocked);
    }

    private void CheckAttackUnlock()
    {
        if (AttackingUnlocked) return;

        bool bothHadFirstTurn = player1HasHadFirstTurn && player2HasHadFirstTurn;
        bool bothHaveCards    = Player1.HasCardsOnField() && Player2.HasCardsOnField();

        if (bothHadFirstTurn && bothHaveCards)
        {
            AttackingUnlocked = true;
            Debug.Log("[GameStateManager] Attacking unlocked!");
            gameUI?.ShowMessage("Both players ready — attacking is now unlocked!");
        }
    }

    // ── Card Scanned ───────────────────────────────────────────────────────

    public void OnCardScanned(CardCharacter card)
    {
        if (CurrentPhase != GamePhase.PlayerTurn) return;
        if (card == null) return;

        if (card.OwnerIndex != -1 && card.OwnerIndex != CurrentPlayer.PlayerIndex)
        {
            gameUI?.ShowMessage("That's your opponent's card!");
            return;
        }

        if (CurrentPlayer.FieldCards.Contains(card))
        {
            Debug.Log($"[GameStateManager] {card.characterName} already on field.");
            return;
        }

        if (CurrentPlayer.DeadCards.Contains(card))
        {
            gameUI?.ShowMessage($"{card.characterName} is defeated. Revive it first ({PlayerState.ReviveCost} mana).");
            return;
        }

        if (!CurrentPlayer.HandCards.Contains(card))
        {
            card.SetOwner(CurrentPlayer.PlayerIndex);
            CurrentPlayer.AddToHand(card);
        }

        bool placed = CurrentPlayer.TryPlaceCard(card);

        if (placed)
        {
            imageTrackingManager?.DetachPlacedCard(card);

            // FIX: update facing immediately after a new card is placed.
            UpdateCardFacing();

            gameUI?.RefreshUI(CurrentPlayer, AttackingUnlocked);
            gameUI?.ShowMessage($"{card.characterName} placed! Mana: {CurrentPlayer.CurrentMana}/{PlayerState.MaxMana}");
        }
        else
        {
            gameUI?.ShowMessage($"Not enough mana to place! Need {PlayerState.PlaceCost}.");
        }
    }

    // ── Attack: Step 1 — Select attacker ──────────────────────────────────

    public void OnSelectAttacker(CardCharacter card)
    {
        if (CurrentPhase != GamePhase.PlayerTurn) return;

        if (!AttackingUnlocked)
        {
            if (!player1HasHadFirstTurn || !player2HasHadFirstTurn)
                gameUI?.ShowMessage("Both players must complete their first turn before attacking!");
            else
                gameUI?.ShowMessage("Both players need at least 1 card on the field!");

            return;
        }

        if (!CurrentPlayer.FieldCards.Contains(card))
        {
            gameUI?.ShowMessage("That's not your card!");
            return;
        }

        if (CurrentPlayer.HasAttacked(card))
        {
            gameUI?.ShowMessage($"{card.characterName} already attacked this turn!");
            return;
        }

        SelectedAttacker   = card;
        SelectedTarget     = null;
        SelectedAttackSlot = 0;

        gameUI?.OnAttackerSelected(card);
        Debug.Log($"[GameStateManager] Attacker selected: {card.characterName}");
    }

    // ── Attack: Step 2 — Select attack slot ───────────────────────────────

    public void OnSelectAttackSlot(int slot)
    {
        if (SelectedAttacker == null || CurrentPhase != GamePhase.PlayerTurn) return;

        int manaCost = slot == 1
            ? SelectedAttacker.attack1ManaCost
            : SelectedAttacker.attack2ManaCost;

        if (!CurrentPlayer.CanAfford(manaCost))
        {
            string name = slot == 1
                ? SelectedAttacker.attack1Name
                : SelectedAttacker.attack2Name;

            gameUI?.ShowMessage($"Not enough mana for {name}! Need {manaCost}.");
            return;
        }

        SelectedAttackSlot = slot;

        SelectedAttacker.damagePerShot = slot == 1
            ? SelectedAttacker.attack1Damage
            : SelectedAttacker.attack2Damage;

        string attackName = slot == 1
            ? SelectedAttacker.attack1Name
            : SelectedAttacker.attack2Name;

        float damage = slot == 1
            ? SelectedAttacker.attack1Damage
            : SelectedAttacker.attack2Damage;

        Debug.Log($"[GameStateManager] Attack chosen: {attackName} ({damage} dmg, {manaCost} mana)");
        gameUI?.OnAttackSlotSelected(slot, attackName, damage, manaCost);
    }

    // ── Attack: Step 3 — Select target ────────────────────────────────────

    public void OnSelectTarget(CardCharacter card)
    {
        if (CurrentPhase != GamePhase.PlayerTurn) return;

        if (SelectedAttacker == null || SelectedAttackSlot == 0)
        {
            gameUI?.ShowMessage("Select an attack first!");
            return;
        }

        if (!OpponentPlayer.FieldCards.Contains(card))
        {
            gameUI?.ShowMessage("Invalid target!");
            return;
        }

        SelectedTarget = card;

        gameUI?.OnTargetSelected(card);
        Debug.Log($"[GameStateManager] Target: {card.characterName}");
    }

    // ── Attack: Step 4 — Confirm ──────────────────────────────────────────

    public void OnConfirmAttackPressed()
    {
        if (CurrentPhase != GamePhase.PlayerTurn) return;
        if (SelectedAttacker == null || SelectedAttackSlot == 0) return;

        int manaCost = SelectedAttackSlot == 1
            ? SelectedAttacker.attack1ManaCost
            : SelectedAttacker.attack2ManaCost;

        string attackName = SelectedAttackSlot == 1
            ? SelectedAttacker.attack1Name
            : SelectedAttacker.attack2Name;

        if (!OpponentPlayer.HasCardsOnField())
        {
            bool ok = CurrentPlayer.TryMarkAttack(SelectedAttacker, manaCost);
            if (!ok) return;

            int damage = Mathf.RoundToInt(SelectedAttacker.damagePerShot);

            OpponentPlayer.TakeDamage(damage);
            combatManager?.FireProjectileOnly(SelectedAttacker, null);

            gameUI?.ShowMessage($"{attackName}! {OpponentPlayer.PlayerName} takes {damage} damage!");

            if (!OpponentPlayer.IsAlive)
            {
                EnterEndGame();
                return;
            }
        }
        else if (SelectedTarget != null)
        {
            bool ok = CurrentPlayer.TryMarkAttack(SelectedAttacker, manaCost);
            if (!ok) return;

            combatManager?.ExecuteAttack(SelectedAttacker, SelectedTarget);
        }
        else
        {
            gameUI?.ShowMessage("Select a target first!");
            return;
        }

        ClearAttackSelection();

        // FIX: facing might change after attacks/deaths.
        UpdateCardFacing();

        gameUI?.RefreshUI(CurrentPlayer, AttackingUnlocked);
    }

    // ── Revive ─────────────────────────────────────────────────────────────

    public void OnReviveCardPressed(CardCharacter card)
    {
        if (CurrentPhase != GamePhase.PlayerTurn) return;

        bool revived = CurrentPlayer.TryReviveCard(card);

        if (revived)
        {
            UpdateCardFacing();

            gameUI?.ShowMessage($"{card.characterName} revived! Scan it to place.");
            gameUI?.RefreshUI(CurrentPlayer, AttackingUnlocked);
        }
        else
        {
            gameUI?.ShowMessage($"Need {PlayerState.ReviveCost} mana to revive!");
        }
    }

    // ── End Turn ───────────────────────────────────────────────────────────

    public void OnEndTurnPressed()
    {
        Debug.Log($"[GameStateManager] End Turn pressed. Phase: {CurrentPhase}");

        if (CurrentPhase != GamePhase.PlayerTurn) return;

        if (CurrentPlayer == Player1)
            player1HasHadFirstTurn = true;
        else
            player2HasHadFirstTurn = true;

        ClearAttackSelection();

        CurrentPhase  = GamePhase.PassDevice;
        CurrentPlayer = CurrentPlayer == Player1 ? Player2 : Player1;

        UpdateCardFacing();

        gameUI?.OnPassDevice(CurrentPlayer);
    }

    public void OnPassDeviceConfirmed() => BeginTurn();

    // ── Card Death ─────────────────────────────────────────────────────────

    public void OnCardDied(CardCharacter card)
    {
        if (card == null) return;

        if (Player1.AllCards.Contains(card))
            Player1.NotifyCardDied(card);
        else
            Player2.NotifyCardDied(card);

        if (SelectedAttacker == card || SelectedTarget == card)
            ClearAttackSelection();

        // FIX: retarget surviving cards after death.
        UpdateCardFacing();

        gameUI?.RefreshUI(CurrentPlayer, AttackingUnlocked);
    }

    // ── Facing Helpers ─────────────────────────────────────────────────────

    private void UpdateCardFacing()
    {
        UpdateFacingForSide(Player1.FieldCards, Player2.FieldCards);
        UpdateFacingForSide(Player2.FieldCards, Player1.FieldCards);
    }

    private void UpdateFacingForSide(List<CardCharacter> ownCards, List<CardCharacter> enemyCards)
    {
        if (ownCards == null) return;

        foreach (CardCharacter card in ownCards)
        {
            if (card == null || !card.IsAlive) continue;

            CardCharacter target = GetClosestAliveCard(card, enemyCards);
            card.SetTarget(target);
        }
    }

    private CardCharacter GetClosestAliveCard(CardCharacter source, List<CardCharacter> possibleTargets)
    {
        if (source == null || possibleTargets == null) return null;

        CardCharacter closest = null;
        float bestDistance = float.MaxValue;

        foreach (CardCharacter target in possibleTargets)
        {
            if (target == null || !target.IsAlive) continue;

            float distance = Vector3.Distance(source.transform.position, target.transform.position);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                closest = target;
            }
        }

        return closest;
    }

    // ── General Helpers ────────────────────────────────────────────────────

    private void ClearAttackSelection()
    {
        SelectedAttacker   = null;
        SelectedTarget     = null;
        SelectedAttackSlot = 0;
    }

    private void EnterEndGame()
    {
        CurrentPhase = GamePhase.EndGame;

        PlayerState winner =
            !Player1.IsAlive ? Player2 :
            !Player2.IsAlive ? Player1 :
            null;

        Debug.Log($"=== GAME OVER — {winner?.PlayerName ?? "Draw"} wins! ===");

        gameUI?.OnEndGame(winner);
    }
}