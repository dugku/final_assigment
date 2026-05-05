using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Tracks a single player's HP, mana, hand, field, and dead cards.
/// </summary>
public class PlayerState
{
    public const int MaxHP       = 100;
    public const int MaxMana     = 20;
    public const int ManaPerTurn = 5;
    public const int PlaceCost   = 3;
    public const int AttackCost  = 2;  // default / attack 1 cost
    public const int ReviveCost  = 4;

    public int    PlayerIndex { get; private set; }
    public string PlayerName  => $"Player {PlayerIndex + 1}";

    public int  CurrentHP   { get; private set; }
    public int  CurrentMana { get; private set; }
    public bool IsAlive     => CurrentHP > 0;

    public List<CardCharacter> AllCards   { get; private set; } = new List<CardCharacter>();
    public List<CardCharacter> FieldCards { get; private set; } = new List<CardCharacter>();
    public List<CardCharacter> DeadCards  { get; private set; } = new List<CardCharacter>();
    public List<CardCharacter> HandCards  { get; private set; } = new List<CardCharacter>();

    private HashSet<CardCharacter> attackedThisTurn = new HashSet<CardCharacter>();

    public PlayerState(int playerIndex)
    {
        PlayerIndex = playerIndex;
        CurrentHP   = MaxHP;
        CurrentMana = MaxMana;
    }

    public void StartTurn()
    {
        CurrentMana = Mathf.Min(CurrentMana + ManaPerTurn, MaxMana);
        attackedThisTurn.Clear();
        Debug.Log($"[{PlayerName}] Turn started. Mana: {CurrentMana}/{MaxMana}  HP: {CurrentHP}/{MaxHP}");
    }

    public void AddToHand(CardCharacter card)
    {
        if (!AllCards.Contains(card))  AllCards.Add(card);
        if (!HandCards.Contains(card)) HandCards.Add(card);
    }

    public bool CanAfford(int cost) => CurrentMana >= cost;

    public bool SpendMana(int cost)
    {
        if (!CanAfford(cost)) return false;
        CurrentMana -= cost;
        return true;
    }

    public bool TryPlaceCard(CardCharacter card)
    {
        if (!HandCards.Contains(card)) return false;
        if (!SpendMana(PlaceCost))     return false;
        HandCards.Remove(card);
        FieldCards.Add(card);
        card.gameObject.SetActive(true);
        card.ResetForNewGame();
        Debug.Log($"[{PlayerName}] Placed {card.characterName}. Mana left: {CurrentMana}");
        return true;
    }

    public bool HasAttacked(CardCharacter card) => attackedThisTurn.Contains(card);

    /// <summary>Marks a card as having attacked, spending the given mana cost.</summary>
    public bool TryMarkAttack(CardCharacter card, int manaCost)
    {
        if (!FieldCards.Contains(card)) return false;
        if (HasAttacked(card))          return false;
        if (!SpendMana(manaCost))       return false;
        attackedThisTurn.Add(card);
        return true;
    }

    public bool TryReviveCard(CardCharacter card)
    {
        if (!DeadCards.Contains(card)) return false;
        if (!SpendMana(ReviveCost))    return false;
        DeadCards.Remove(card);
        HandCards.Add(card);
        Debug.Log($"[{PlayerName}] Revived {card.characterName} to hand.");
        return true;
    }

    public void NotifyCardDied(CardCharacter card)
    {
        FieldCards.Remove(card);
        attackedThisTurn.Remove(card);
        if (!DeadCards.Contains(card)) DeadCards.Add(card);
    }

    public void TakeDamage(int amount)
    {
        CurrentHP = Mathf.Max(0, CurrentHP - amount);
        Debug.Log($"[{PlayerName}] Took {amount} damage. HP: {CurrentHP}/{MaxHP}");
    }

    public bool HasCardsOnField() => FieldCards.Count > 0;

    public List<CardCharacter> GetAttackableCards()
    {
        List<CardCharacter> ready = new List<CardCharacter>();
        foreach (CardCharacter card in FieldCards)
            if (!HasAttacked(card)) ready.Add(card);
        return ready;
    }
}