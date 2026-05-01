using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// Editor utility — right-click the component and select "Generate UI".
/// Fantasy dark-amber theme. AR camera stays visible in the middle of the screen.
///
/// LAYOUT (bottom → top, as anchor fractions):
///   0.00 – 0.06   Safe zone (iOS home bar)
///   0.06 – 0.20   Bottom bar  — stats + action buttons
///   0.20 – 0.33   Field strip — your cards / dead cards
///   0.33 – 0.82   [AR CAMERA]
///   0.82 – 0.88   Instruction strip
///   0.88 – 1.00   Top bar — turn banner + opponent HP
/// </summary>
public class UIGenerator : MonoBehaviour
{
    [Header("Assign before generating")]
    public GameStateManager gameStateManager;
    public Canvas           targetCanvas;

    // ── Design Tokens ──────────────────────────────────────────────────────
    // Dark parchment backgrounds
    static readonly Color BG_SCREEN    = new Color(0.102f, 0.067f, 0.031f, 1.00f); // #1a1108
    static readonly Color BG_DARK      = new Color(0.082f, 0.051f, 0.016f, 0.97f); // #150d04
    static readonly Color BG_PANEL     = new Color(0.137f, 0.082f, 0.031f, 0.97f); // #231508
    static readonly Color BG_TOP       = new Color(0.118f, 0.063f, 0.016f, 0.98f); // #1e1004
    static readonly Color BG_CARD      = new Color(0.137f, 0.082f, 0.031f, 0.95f); // #231508
    static readonly Color BG_DEAD_CARD = new Color(0.125f, 0.031f, 0.031f, 0.95f); // #200808
    static readonly Color BG_CHIP      = new Color(0.165f, 0.102f, 0.016f, 1.00f); // #2a1a04

    // Accent colours
    static readonly Color GOLD         = new Color(0.941f, 0.706f, 0.161f, 1f); // #f0b429  — selections, banners
    static readonly Color GOLD_DARK    = new Color(0.784f, 0.588f, 0.039f, 1f); // #c8960a  — confirm button bg
    static readonly Color AMBER_BORDER = new Color(0.353f, 0.227f, 0.063f, 1f); // #5a3a10  — card borders
    static readonly Color AMBER_DIM    = new Color(0.541f, 0.416f, 0.157f, 1f); // #8a6a28  — sub-labels
    static readonly Color GREEN_BTN    = new Color(0.180f, 0.431f, 0.180f, 1f); // #2e6e2e  — confirm / ready
    static readonly Color GREEN_TXT    = new Color(0.627f, 0.941f, 0.627f, 1f); // #a0f0a0
    static readonly Color BLUE_BTN     = new Color(0.102f, 0.180f, 0.369f, 1f); // #1a2e5e  — end turn
    static readonly Color BLUE_TXT     = new Color(0.565f, 0.722f, 1.000f, 1f); // #90b8ff
    static readonly Color RED_DARK     = new Color(0.125f, 0.031f, 0.031f, 1f); // #200808
    static readonly Color RED_BORDER   = new Color(0.353f, 0.063f, 0.063f, 1f); // #5a1010
    static readonly Color MANA_BG      = new Color(0.051f, 0.118f, 0.188f, 1f); // #0d1e30
    static readonly Color MANA_BORDER  = new Color(0.102f, 0.251f, 0.376f, 1f); // #1a4060

    // Text
    static readonly Color TXT_GOLD     = new Color(0.941f, 0.706f, 0.161f, 1f); // #f0b429
    static readonly Color TXT_PARCHMENT= new Color(0.910f, 0.816f, 0.565f, 1f); // #e8d090
    static readonly Color TXT_DIM      = new Color(0.541f, 0.416f, 0.157f, 1f); // #8a6a28
    static readonly Color TXT_HP       = new Color(0.910f, 0.439f, 0.439f, 1f); // #e87070
    static readonly Color TXT_MP       = new Color(0.439f, 0.667f, 0.933f, 1f); // #70aaee
    static readonly Color TXT_DEAD     = new Color(0.753f, 0.502f, 0.502f, 1f); // #c08080
    static readonly Color TXT_CONFIRM  = new Color(0.102f, 0.055f, 0.000f, 1f); // #1a0e00
    static readonly Color WHITE        = new Color(0.980f, 0.980f, 0.980f, 1f);

    // ──────────────────────────────────────────────────────────────────────
    [ContextMenu("Generate UI")]
    public void GenerateUI()
    {
        if (targetCanvas == null)
        {
            Debug.LogError("[UIGenerator] Assign the Canvas first!");
            return;
        }

        foreach (Transform child in targetCanvas.transform)
            DestroyImmediate(child.gameObject);

        GameUI gameUI = targetCanvas.GetComponent<GameUI>()
                     ?? targetCanvas.gameObject.AddComponent<GameUI>();

        gameUI.passDevicePanel  = BuildPassDevicePanel(targetCanvas.transform, gameUI);
        gameUI.turnPanel        = BuildTurnPanel(targetCanvas.transform, gameUI);
        gameUI.endGamePanel     = BuildEndGamePanel(targetCanvas.transform, gameUI);
        gameUI.messageText      = BuildMessageToast(targetCanvas.transform);
        gameUI.cardButtonPrefab = BuildCardButtonPrefab();

        Debug.Log("[UIGenerator] Fantasy UI generated!");
        EditorUtility.SetDirty(targetCanvas.gameObject);
    }

    // ── Pass Device Panel ──────────────────────────────────────────────────
    // Full-screen overlay shown between turns.
    // Centre: avatar icon → "Pass to" → player name → subtitle → "I'm Ready" button.
    // Footer: shows previous player's remaining HP.
    private GameObject BuildPassDevicePanel(Transform parent, GameUI gameUI)
    {
        GameObject panel = CreatePanel(parent, "PassDevicePanel", BG_SCREEN);
        SetRT(panel, Vector2.zero, Vector2.one);
        NoRaycast(panel);

        // Top bar — just a label
        GameObject topBar = CreatePanel(panel.transform, "TopBar", BG_TOP);
        SetRT(topBar, new Vector2(0f, 0.88f), new Vector2(1f, 1f));
        NoRaycast(topBar);
        TextMeshProUGUI topLbl = CreateText(topBar.transform, "TopLabel",
            "HAND OVER THE DEVICE", 13, TXT_GOLD, true);
        SetRT(topLbl.gameObject, Vector2.zero, Vector2.one);
        topLbl.alignment = TextAlignmentOptions.Center;

        // Avatar circle
        GameObject avatar = CreatePanel(panel.transform, "Avatar", BG_PANEL);
        SetRT(avatar, new Vector2(0.35f, 0.66f), new Vector2(0.65f, 0.78f));
        NoRaycast(avatar);
        Image avImg = avatar.GetComponent<Image>();
        avImg.color = BG_PANEL;

        // "Pass to" label
        TextMeshProUGUI passTo = CreateText(panel.transform, "PassToLabel",
            "Pass to", 18, TXT_DIM, false);
        SetRT(passTo.gameObject, new Vector2(0.1f, 0.61f), new Vector2(0.9f, 0.67f));
        passTo.alignment = TextAlignmentOptions.Center;

        // Player name — big gold
        TextMeshProUGUI playerName = CreateText(panel.transform, "PassDeviceText",
            "Player 2", 32, TXT_GOLD, true);
        SetRT(playerName.gameObject, new Vector2(0.1f, 0.53f), new Vector2(0.9f, 0.62f));
        playerName.alignment = TextAlignmentOptions.Center;
        gameUI.passDeviceText = playerName;

        // Subtitle
        TextMeshProUGUI sub = CreateText(panel.transform, "PassSub",
            "Cover the screen while handing over", 14, TXT_DIM, false);
        SetRT(sub.gameObject, new Vector2(0.08f, 0.47f), new Vector2(0.92f, 0.54f));
        sub.alignment = TextAlignmentOptions.Center;

        // Ready button
        Button readyBtn = CreateStyledButton(panel.transform, "PassDeviceButton",
            "I'm Ready", GREEN_BTN, GREEN_TXT, 20);
        SetRT(readyBtn.gameObject, new Vector2(0.12f, 0.35f), new Vector2(0.88f, 0.44f));
        gameUI.passDeviceButton = readyBtn;

        // Footer — previous player HP (cosmetic)
        GameObject footer = CreatePanel(panel.transform, "Footer", BG_TOP);
        SetRT(footer, new Vector2(0f, 0f), new Vector2(1f, 0.08f));
        NoRaycast(footer);

        panel.SetActive(false);
        return panel;
    }

    // ── Turn Panel ─────────────────────────────────────────────────────────
    private GameObject BuildTurnPanel(Transform parent, GameUI gameUI)
    {
        GameObject panel = CreatePanelNoImage(parent, "TurnPanel");
        SetRT(panel, Vector2.zero, Vector2.one);

        // ── TOP BAR (88%–100%) ────────────────────────────────────────────
        GameObject topBar = CreatePanel(panel.transform, "TopBar", BG_TOP);
        SetRT(topBar, new Vector2(0f, 0.88f), new Vector2(1f, 1.0f));
        NoRaycast(topBar);

        // HP chip — left
        GameObject hpChip = CreatePanel(topBar.transform, "HPChip", BG_CHIP);
        SetRT(hpChip, new Vector2(0.02f, 0.15f), new Vector2(0.22f, 0.85f));
        NoRaycast(hpChip);
        TextMeshProUGUI hpChipTxt = CreateText(hpChip.transform, "ChipHP", "74 HP", 13, TXT_HP, true);
        SetRT(hpChipTxt.gameObject, Vector2.zero, Vector2.one);
        hpChipTxt.alignment = TextAlignmentOptions.Center;

        // Turn banner — centre
        TextMeshProUGUI turnText = CreateText(topBar.transform, "TurnBannerText",
            "Player 1's Turn", 15, TXT_GOLD, true);
        SetRT(turnText.gameObject, new Vector2(0.24f, 0f), new Vector2(0.76f, 1f));
        turnText.alignment = TextAlignmentOptions.Center;
        gameUI.turnBannerText = turnText;

        // Opponent HP — right
        TextMeshProUGUI oppHP = CreateText(topBar.transform, "OpponentPlayerHP",
            "P2  58 HP", 13, TXT_HP, false);
        SetRT(oppHP.gameObject, new Vector2(0.76f, 0f), new Vector2(0.98f, 1f));
        oppHP.alignment = TextAlignmentOptions.MidlineRight;
        gameUI.opponentPlayerHP = oppHP;

        // ── INSTRUCTION STRIP (82%–88%) ────────────────────────────────────
        GameObject instrStrip = CreatePanel(panel.transform, "InstrStrip",
            new Color(0.137f, 0.082f, 0.031f, 0.96f));
        SetRT(instrStrip, new Vector2(0f, 0.82f), new Vector2(1f, 0.88f));
        NoRaycast(instrStrip);

        // Gold left bar accent
        GameObject instrBar = CreatePanel(instrStrip.transform, "LeftBar", GOLD);
        SetRT(instrBar, Vector2.zero, new Vector2(0.008f, 1f));
        NoRaycast(instrBar);

        TextMeshProUGUI instr = CreateText(instrStrip.transform, "InstructionsText",
            "Scan a card to place it.", 13, TXT_DIM, false);
        SetRT(instr.gameObject, new Vector2(0.02f, 0f), new Vector2(0.97f, 1f));
        instr.alignment = TextAlignmentOptions.MidlineLeft;
        gameUI.instructionsText = instr;

        // ── ATTACK SELECTION PANEL (68%–82%) — appears after tapping a card ──
        GameObject atkSel = CreatePanel(panel.transform, "AttackSelectionPanel",
            new Color(0.118f, 0.067f, 0.016f, 0.98f));
        SetRT(atkSel, new Vector2(0f, 0.68f), new Vector2(1f, 0.82f));
        NoRaycast(atkSel);
        gameUI.attackSelectionPanel = atkSel;

        // "Choose:" label
        TextMeshProUGUI chooseLbl = CreateText(atkSel.transform, "ChooseLabel",
            "CHOOSE:", 11, TXT_DIM, true);
        SetRT(chooseLbl.gameObject, new Vector2(0.02f, 0f), new Vector2(0.16f, 1f));
        chooseLbl.alignment = TextAlignmentOptions.MidlineLeft;

        // Attack 1 button (left)
        Button atk1Btn = CreateStyledButton(atkSel.transform, "Attack1Button",
            "", BG_PANEL, WHITE, 12);
        SetRT(atk1Btn.gameObject, new Vector2(0.17f, 0.06f), new Vector2(0.57f, 0.94f));
        AddBorder(atk1Btn.gameObject, AMBER_BORDER);
        ClearButtonText(atk1Btn);
        TextMeshProUGUI atk1Lbl = CreateText(atk1Btn.transform, "Attack1Label",
            "Attack 1\n20 dmg  |  2 mana", 11, TXT_PARCHMENT, false);
        SetRT(atk1Lbl.gameObject, Vector2.zero, Vector2.one);
        atk1Lbl.alignment = TextAlignmentOptions.Center;
        gameUI.attack1Button = atk1Btn;
        gameUI.attack1Label  = atk1Lbl;

        // Attack 2 button (right)
        Button atk2Btn = CreateStyledButton(atkSel.transform, "Attack2Button",
            "", BG_PANEL, WHITE, 12);
        SetRT(atk2Btn.gameObject, new Vector2(0.59f, 0.06f), new Vector2(0.98f, 0.94f));
        AddBorder(atk2Btn.gameObject, AMBER_BORDER);
        ClearButtonText(atk2Btn);
        TextMeshProUGUI atk2Lbl = CreateText(atk2Btn.transform, "Attack2Label",
            "Attack 2\n40 dmg  |  4 mana", 11, TXT_PARCHMENT, false);
        SetRT(atk2Lbl.gameObject, Vector2.zero, Vector2.one);
        atk2Lbl.alignment = TextAlignmentOptions.Center;
        gameUI.attack2Button = atk2Btn;
        gameUI.attack2Label  = atk2Lbl;

        atkSel.SetActive(false);

        // ── ENEMY CARD PANEL (55%–82%) — appears when choosing a target ────
        GameObject enemyPanel = CreatePanel(panel.transform, "EnemyCardPanel",
            new Color(0.118f, 0.063f, 0.016f, 0.97f));
        SetRT(enemyPanel, new Vector2(0f, 0.55f), new Vector2(1f, 0.82f));
        NoRaycast(enemyPanel);
        gameUI.enemyCardPanel = enemyPanel;

        TextMeshProUGUI enemyLbl = CreateText(enemyPanel.transform, "EnemyLabel",
            "SELECT TARGET", 11, TXT_HP, true);
        SetRT(enemyLbl.gameObject, new Vector2(0.03f, 0.82f), new Vector2(0.97f, 1f));
        enemyLbl.alignment = TextAlignmentOptions.MidlineLeft;

        GameObject enemyCont = CreateLayoutContainer(enemyPanel.transform, "EnemyCardContainer");
        SetRT(enemyCont, new Vector2(0.02f, 0.04f), new Vector2(0.98f, 0.82f));
        gameUI.enemyCardContainer = enemyCont.transform;

        enemyPanel.SetActive(false);

        // ── YOUR FIELD STRIP (20%–33%) ─────────────────────────────────────
        GameObject fieldStrip = CreatePanel(panel.transform, "FieldStrip",
            new Color(0.094f, 0.118f, 0.055f, 0.92f));
        SetRT(fieldStrip, new Vector2(0f, 0.20f), new Vector2(1f, 0.33f));
        NoRaycast(fieldStrip);

        // Gold left bar accent
        GameObject fieldBar = CreatePanel(fieldStrip.transform, "LeftBar",
            new Color(0.471f, 0.784f, 0.235f, 1f));
        SetRT(fieldBar, Vector2.zero, new Vector2(0.007f, 1f));
        NoRaycast(fieldBar);

        TextMeshProUGUI fieldLbl = CreateText(fieldStrip.transform, "FieldLabel",
            "YOUR CARDS", 10, new Color(0.471f, 0.784f, 0.235f, 1f), true);
        SetRT(fieldLbl.gameObject, new Vector2(0.02f, 0.72f), new Vector2(0.6f, 1f));
        fieldLbl.alignment = TextAlignmentOptions.MidlineLeft;

        GameObject fieldCont = CreateLayoutContainer(fieldStrip.transform, "FieldCardContainer");
        SetRT(fieldCont, new Vector2(0.01f, 0.04f), new Vector2(0.99f, 0.72f));
        gameUI.fieldCardContainer = fieldCont.transform;

        // ── DEAD CARDS STRIP (same slot as field) — shown when cards die ───
        GameObject deadPanel = CreatePanel(panel.transform, "DeadCardPanel",
            new Color(0.125f, 0.047f, 0.047f, 0.92f));
        SetRT(deadPanel, new Vector2(0f, 0.20f), new Vector2(1f, 0.33f));
        NoRaycast(deadPanel);
        gameUI.deadCardPanel = deadPanel;

        GameObject deadBar = CreatePanel(deadPanel.transform, "LeftBar", TXT_HP);
        SetRT(deadBar, Vector2.zero, new Vector2(0.007f, 1f));
        NoRaycast(deadBar);

        TextMeshProUGUI deadLbl = CreateText(deadPanel.transform, "DeadLabel",
            $"DEFEATED  —  {PlayerState.ReviveCost} mana to revive", 10, TXT_HP, false);
        SetRT(deadLbl.gameObject, new Vector2(0.02f, 0.72f), new Vector2(0.97f, 1f));
        deadLbl.alignment = TextAlignmentOptions.MidlineLeft;

        GameObject deadCont = CreateLayoutContainer(deadPanel.transform, "DeadCardContainer");
        SetRT(deadCont, new Vector2(0.01f, 0.04f), new Vector2(0.99f, 0.72f));
        gameUI.deadCardContainer = deadCont.transform;

        deadPanel.SetActive(false);

        // ── BOTTOM BAR (6%–20%) ────────────────────────────────────────────
        GameObject bottomBar = CreatePanel(panel.transform, "BottomBar", BG_DARK);
        SetRT(bottomBar, new Vector2(0f, 0.06f), new Vector2(1f, 0.20f));
        NoRaycast(bottomBar);

        // Stats row — top 45% of bottom bar
        GameObject statsRow = CreatePanelNoImage(bottomBar.transform, "StatsRow");
        SetRT(statsRow, new Vector2(0f, 0.52f), new Vector2(1f, 1f));
        NoRaycast(statsRow);

        // HP pill
        GameObject hpPill = CreatePanel(statsRow.transform, "HPPill",
            new Color(0.165f, 0.031f, 0.031f, 0.9f));
        SetRT(hpPill, new Vector2(0.02f, 0.10f), new Vector2(0.36f, 0.90f));
        NoRaycast(hpPill);
        TextMeshProUGUI hpTxt = CreateText(hpPill.transform, "CurrentPlayerHP",
            "HP  100/100", 13, TXT_HP, true);
        SetRT(hpTxt.gameObject, Vector2.zero, Vector2.one);
        hpTxt.alignment = TextAlignmentOptions.Center;
        gameUI.currentPlayerHP = hpTxt;

        // Mana pill
        GameObject manaPill = CreatePanel(statsRow.transform, "ManaPill", MANA_BG);
        SetRT(manaPill, new Vector2(0.38f, 0.10f), new Vector2(0.72f, 0.90f));
        NoRaycast(manaPill);
        TextMeshProUGUI manaTxt = CreateText(manaPill.transform, "CurrentPlayerMana",
            "MP  10/10", 13, TXT_MP, true);
        SetRT(manaTxt.gameObject, Vector2.zero, Vector2.one);
        manaTxt.alignment = TextAlignmentOptions.Center;
        gameUI.currentPlayerMana = manaTxt;

        // Buttons row — bottom 52% of bottom bar
        GameObject btnRow = CreatePanelNoImage(bottomBar.transform, "BtnRow");
        SetRT(btnRow, new Vector2(0f, 0f), new Vector2(1f, 0.52f));
        NoRaycast(btnRow);

        // Confirm Attack button (hidden until attack is ready)
        Button confirmBtn = CreateStyledButton(btnRow.transform, "ConfirmAttackButton",
            "Confirm Attack", GREEN_BTN, GREEN_TXT, 14);
        SetRT(confirmBtn.gameObject, new Vector2(0.02f, 0.08f), new Vector2(0.49f, 0.92f));
        gameUI.confirmAttackButton = confirmBtn;
        confirmBtn.gameObject.SetActive(false);

        // End Turn button
        Button endTurnBtn = CreateStyledButton(btnRow.transform, "EndTurnButton",
            "End Turn", BLUE_BTN, BLUE_TXT, 14);
        SetRT(endTurnBtn.gameObject, new Vector2(0.51f, 0.08f), new Vector2(0.98f, 0.92f));
        gameUI.endTurnButton = endTurnBtn;

        panel.SetActive(false);
        return panel;
    }

    // ── End Game Panel ─────────────────────────────────────────────────────
    // Full-screen overlay with crown glyph, winner name, HP/MP stats, Play Again.
    private GameObject BuildEndGamePanel(Transform parent, GameUI gameUI)
    {
        GameObject panel = CreatePanel(parent, "EndGamePanel", BG_SCREEN);
        SetRT(panel, Vector2.zero, Vector2.one);
        NoRaycast(panel);

        // Top bar
        GameObject topBar = CreatePanel(panel.transform, "TopBar", BG_TOP);
        SetRT(topBar, new Vector2(0f, 0.88f), new Vector2(1f, 1f));
        NoRaycast(topBar);
        TextMeshProUGUI topLbl = CreateText(topBar.transform, "TopLabel",
            "GAME OVER", 13, TXT_GOLD, true);
        SetRT(topLbl.gameObject, Vector2.zero, Vector2.one);
        topLbl.alignment = TextAlignmentOptions.Center;

        // "Champion" sub-label
        TextMeshProUGUI champLbl = CreateText(panel.transform, "ChampLabel",
            "CHAMPION", 12, TXT_DIM, true);
        SetRT(champLbl.gameObject, new Vector2(0.1f, 0.66f), new Vector2(0.9f, 0.72f));
        champLbl.alignment = TextAlignmentOptions.Center;

        // Winner name — large gold
        TextMeshProUGUI winnerText = CreateText(panel.transform, "WinnerText",
            "Player 1 Wins!", 38, TXT_GOLD, true);
        SetRT(winnerText.gameObject, new Vector2(0.05f, 0.55f), new Vector2(0.95f, 0.67f));
        winnerText.alignment = TextAlignmentOptions.Center;
        gameUI.winnerText = winnerText;

        // Sub-line
        TextMeshProUGUI sub = CreateText(panel.transform, "SubText",
            "Opponent has been defeated", 16, TXT_DIM, false);
        SetRT(sub.gameObject, new Vector2(0.1f, 0.49f), new Vector2(0.9f, 0.56f));
        sub.alignment = TextAlignmentOptions.Center;

        // Divider line
        GameObject divLine = CreatePanel(panel.transform, "Divider", AMBER_BORDER);
        SetRT(divLine, new Vector2(0.30f, 0.47f), new Vector2(0.70f, 0.472f));
        NoRaycast(divLine);

        // Play Again button
        Button restartBtn = CreateStyledButton(panel.transform, "RestartButton",
            "Play Again", GOLD_DARK, TXT_CONFIRM, 22);
        SetRT(restartBtn.gameObject, new Vector2(0.15f, 0.32f), new Vector2(0.85f, 0.42f));
        gameUI.restartButton = restartBtn;

        panel.SetActive(false);
        return panel;
    }

    // ── Message Toast ──────────────────────────────────────────────────────
    // Floats in the AR camera zone. Gold left bar, dark parchment background.
    private TextMeshProUGUI BuildMessageToast(Transform parent)
    {
        GameObject go = CreatePanel(parent, "MessageToast",
            new Color(0.118f, 0.063f, 0.016f, 0.97f));
        SetRT(go, new Vector2(0.03f, 0.47f), new Vector2(0.97f, 0.54f));
        NoRaycast(go);

        // Gold left bar
        GameObject bar = CreatePanel(go.transform, "GoldBar", GOLD);
        SetRT(bar, Vector2.zero, new Vector2(0.010f, 1f));
        NoRaycast(bar);

        // Amber border outline via outline image
        Image img = go.GetComponent<Image>();
        img.color = new Color(0.118f, 0.063f, 0.016f, 0.97f);

        TextMeshProUGUI txt = CreateText(go.transform, "MessageText", "", 14, TXT_PARCHMENT, false);
        SetRT(txt.gameObject, new Vector2(0.022f, 0f), new Vector2(0.978f, 1f));
        txt.alignment = TextAlignmentOptions.MidlineLeft;

        go.SetActive(false);
        return txt;
    }

    // ── Card Button Prefab ─────────────────────────────────────────────────
    // Dark parchment card with amber left stripe and gold selection highlight.
    // Two text children: CardName (bold, parchment) + ActionLabel (small, dim).
    private GameObject BuildCardButtonPrefab()
    {
        GameObject go = new GameObject("CardButtonPrefab");
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(130f, 58f);

        Image img = go.AddComponent<Image>();
        img.color = BG_CARD;

        Button btn = go.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor      = BG_CARD;
        cb.highlightedColor = new Color(0.20f, 0.14f, 0.06f, 0.97f); // slightly lighter
        cb.pressedColor     = new Color(0.10f, 0.06f, 0.02f, 0.97f);
        cb.disabledColor    = new Color(0.10f, 0.07f, 0.03f, 0.55f);
        btn.colors = cb;

        // Amber left stripe
        GameObject stripe = new GameObject("Stripe");
        stripe.transform.SetParent(go.transform, false);
        RectTransform srt = stripe.AddComponent<RectTransform>();
        srt.anchorMin = Vector2.zero;
        srt.anchorMax = new Vector2(0.045f, 1f);
        srt.offsetMin = srt.offsetMax = Vector2.zero;
        Image si = stripe.AddComponent<Image>();
        si.color = GOLD_DARK;
        si.raycastTarget = false;

        // Gold bottom border (selection indicator — always present, coloured on select via tint)
        GameObject bord = new GameObject("BottomBorder");
        bord.transform.SetParent(go.transform, false);
        RectTransform brt = bord.AddComponent<RectTransform>();
        brt.anchorMin = new Vector2(0f, 0f);
        brt.anchorMax = new Vector2(1f, 0.06f);
        brt.offsetMin = brt.offsetMax = Vector2.zero;
        Image bi = bord.AddComponent<Image>();
        bi.color = AMBER_BORDER;
        bi.raycastTarget = false;

        // Card name
        TextMeshProUGUI name = CreateText(go.transform, "CardName", "Card", 13, TXT_PARCHMENT, true);
        RectTransform nrt = name.GetComponent<RectTransform>();
        nrt.anchorMin = new Vector2(0.07f, 0.48f);
        nrt.anchorMax = new Vector2(0.97f, 0.94f);
        nrt.offsetMin = nrt.offsetMax = Vector2.zero;
        name.alignment = TextAlignmentOptions.MidlineLeft;

        // Action label
        TextMeshProUGUI action = CreateText(go.transform, "ActionLabel", "Action", 10, TXT_DIM, false);
        RectTransform art = action.GetComponent<RectTransform>();
        art.anchorMin = new Vector2(0.07f, 0.07f);
        art.anchorMax = new Vector2(0.97f, 0.48f);
        art.offsetMin = art.offsetMax = Vector2.zero;
        action.alignment = TextAlignmentOptions.MidlineLeft;

        string path = "Assets/Resources/Prefabs/CardButtonPrefab.prefab";
        System.IO.Directory.CreateDirectory("Assets/Resources/Prefabs");
        GameObject pf = PrefabUtility.SaveAsPrefabAsset(go, path);
        DestroyImmediate(go);
        Debug.Log($"[UIGenerator] Card button prefab saved to {path}");
        return pf;
    }

    // ── Helpers ────────────────────────────────────────────────────────────
    private Button CreateStyledButton(Transform parent, string objName, string label,
        Color bg, Color fg, int fontSize)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        go.AddComponent<Image>().color = bg;
        Button btn = go.AddComponent<Button>();
        ColorBlock c = btn.colors;
        c.normalColor      = bg;
        c.highlightedColor = Brighten(bg, 0.10f);
        c.pressedColor     = Darken(bg, 0.20f);
        c.disabledColor    = new Color(bg.r, bg.g, bg.b, 0.45f);
        btn.colors = c;
        if (!string.IsNullOrEmpty(label))
        {
            TextMeshProUGUI t = CreateText(go.transform, "Text", label, fontSize, fg, true);
            RectTransform rt = t.GetComponent<RectTransform>();
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = rt.offsetMax = Vector2.zero;
            t.alignment = TextAlignmentOptions.Center;
        }
        return btn;
    }

    private void AddBorder(GameObject go, Color borderColor)
    {
        // Adds a thin outline image behind the button for the amber border effect
        GameObject border = new GameObject("Border");
        border.transform.SetParent(go.transform, false);
        border.transform.SetAsFirstSibling();
        RectTransform rt = border.AddComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-1f, -1f);
        rt.offsetMax = new Vector2(1f, 1f);
        Image img = border.AddComponent<Image>();
        img.color = borderColor;
        img.raycastTarget = false;
    }

    private void ClearButtonText(Button btn)
    {
        foreach (Transform child in btn.transform)
            DestroyImmediate(child.gameObject);
    }

    private GameObject CreatePanel(Transform parent, string objName, Color color)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        go.AddComponent<Image>().color = color;
        return go;
    }

    private GameObject CreatePanelNoImage(Transform parent, string objName)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        return go;
    }

    private GameObject CreateLayoutContainer(Transform parent, string objName)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        HorizontalLayoutGroup h = go.AddComponent<HorizontalLayoutGroup>();
        h.spacing               = 8f;
        h.childAlignment        = TextAnchor.MiddleCenter;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight= true;
        h.padding               = new RectOffset(6, 6, 3, 3);
        return go;
    }

    private TextMeshProUGUI CreateText(Transform parent, string objName, string content,
        int fontSize, Color color, bool bold)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text               = content;
        t.fontSize           = fontSize;
        t.color              = color;
        t.fontStyle          = bold ? FontStyles.Bold : FontStyles.Normal;
        t.enableWordWrapping = true;
        t.raycastTarget      = false;
        return t;
    }

    private void SetRT(GameObject go, Vector2 min, Vector2 max)
    {
        RectTransform rt = go.GetComponent<RectTransform>()
                        ?? go.AddComponent<RectTransform>();
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
    }

    private void NoRaycast(GameObject go)
    {
        Image img = go.GetComponent<Image>();
        if (img != null) img.raycastTarget = false;
    }

    private Color Brighten(Color c, float amount) =>
        new Color(Mathf.Min(c.r + amount, 1f),
                  Mathf.Min(c.g + amount, 1f),
                  Mathf.Min(c.b + amount, 1f), c.a);

    private Color Darken(Color c, float amount) =>
        new Color(c.r * (1f - amount), c.g * (1f - amount),
                  c.b * (1f - amount), c.a);
}
#endif