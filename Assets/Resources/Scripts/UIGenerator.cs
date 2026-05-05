using UnityEngine;
using UnityEngine.UI;
using TMPro;

#if UNITY_EDITOR
using UnityEditor;

/// <summary>
/// Cleaner AR UI generator for the turn-based card game.
/// Goal: keep the middle of the screen open for AR content.
///
/// Layout:
///   0.00 - 0.06  bottom safe zone
///   0.06 - 0.19  compact bottom dock: HP / Mana / buttons
///   0.19 - 0.30  compact card hand strip
///   0.30 - 0.80  AR view kept mostly clear
///   0.80 - 0.86  temporary action sheets only when needed
///   0.86 - 0.94  small instruction toast
///   0.94 - 1.00  compact top HUD
/// </summary>
public class UIGenerator : MonoBehaviour
{
    [Header("Assign before generating")]
    public GameStateManager gameStateManager;
    public Canvas targetCanvas;

    // Main palette
    static readonly Color CLEAR_PANEL  = new Color(0.02f, 0.015f, 0.010f, 0.58f);
    static readonly Color PANEL        = new Color(0.070f, 0.045f, 0.025f, 0.82f);
    static readonly Color PANEL_SOLID  = new Color(0.070f, 0.045f, 0.025f, 0.94f);
    static readonly Color CARD_BG      = new Color(0.100f, 0.070f, 0.040f, 0.92f);
    static readonly Color GOLD         = new Color(0.941f, 0.706f, 0.161f, 1f);
    static readonly Color GOLD_DARK    = new Color(0.784f, 0.588f, 0.039f, 1f);
    static readonly Color RED          = new Color(0.910f, 0.439f, 0.439f, 1f);
    static readonly Color BLUE         = new Color(0.439f, 0.667f, 0.933f, 1f);
    static readonly Color GREEN        = new Color(0.450f, 0.900f, 0.500f, 1f);
    static readonly Color TEXT         = new Color(0.930f, 0.860f, 0.650f, 1f);
    static readonly Color MUTED        = new Color(0.650f, 0.540f, 0.320f, 1f);
    static readonly Color DARK_TEXT    = new Color(0.080f, 0.045f, 0.000f, 1f);
    static readonly Color BLUE_BTN     = new Color(0.075f, 0.130f, 0.290f, 0.96f);
    static readonly Color GREEN_BTN    = new Color(0.120f, 0.360f, 0.140f, 0.96f);
    static readonly Color RED_PANEL    = new Color(0.150f, 0.040f, 0.040f, 0.88f);

    // Text was too small on-device. Keep this high for phone AR.
    // If text starts clipping on a smaller phone, lower this to 1.35f.
    const float FONT_SCALE = 1.60f;

    [ContextMenu("Generate UI")]
    public void GenerateUI()
    {
        if (targetCanvas == null)
        {
            Debug.LogError("[UIGenerator] Assign the Canvas first!");
            return;
        }

        ConfigureCanvasForPhoneAR(targetCanvas);

        foreach (Transform child in targetCanvas.transform)
            DestroyImmediate(child.gameObject);

        GameUI gameUI = targetCanvas.GetComponent<GameUI>()
                     ?? targetCanvas.gameObject.AddComponent<GameUI>();

        gameUI.passDevicePanel  = BuildPassDevicePanel(targetCanvas.transform, gameUI);
        gameUI.turnPanel        = BuildTurnPanel(targetCanvas.transform, gameUI);
        gameUI.endGamePanel     = BuildEndGamePanel(targetCanvas.transform, gameUI);
        gameUI.messageText      = BuildMessageToast(targetCanvas.transform);
        gameUI.cardButtonPrefab = BuildCardButtonPrefab();

        EditorUtility.SetDirty(targetCanvas.gameObject);
        Debug.Log("[UIGenerator] Clean AR UI generated.");
    }

    private GameObject BuildTurnPanel(Transform parent, GameUI gameUI)
    {
        GameObject panel = CreatePanelNoImage(parent, "TurnPanel");
        SetRT(panel, Vector2.zero, Vector2.one);

        // Compact top HUD. No fake top-left HP chip.
        GameObject top = CreatePanel(panel.transform, "TopHUD", CLEAR_PANEL);
        SetRT(top, new Vector2(0.025f, 0.925f), new Vector2(0.975f, 0.992f));
        NoRaycast(top);

        TextMeshProUGUI turnText = CreateText(top.transform, "TurnBannerText", "Player 1's Turn", 18, GOLD, true);
        SetRT(turnText.gameObject, new Vector2(0.25f, 0f), new Vector2(0.75f, 1f));
        turnText.alignment = TextAlignmentOptions.Center;
        gameUI.turnBannerText = turnText;

        TextMeshProUGUI oppHP = CreateText(top.transform, "OpponentPlayerHP", "Enemy HP: 100/100", 15, RED, true);
        SetRT(oppHP.gameObject, new Vector2(0.72f, 0f), new Vector2(0.98f, 1f));
        oppHP.alignment = TextAlignmentOptions.MidlineRight;
        gameUI.opponentPlayerHP = oppHP;

        // Instruction toast near top, not a full-width slab.
        GameObject instrBox = CreatePanel(panel.transform, "InstructionToast", PANEL);
        SetRT(instrBox, new Vector2(0.04f, 0.845f), new Vector2(0.96f, 0.915f));
        NoRaycast(instrBox);

        TextMeshProUGUI instr = CreateText(instrBox.transform, "InstructionsText", "Scan a card to place it.", 16, TEXT, true);
        SetRT(instr.gameObject, new Vector2(0.04f, 0f), new Vector2(0.96f, 1f));
        instr.alignment = TextAlignmentOptions.MidlineLeft;
        gameUI.instructionsText = instr;

        // Attack selection: compact bottom sheet above your cards.
        GameObject atkSel = CreatePanel(panel.transform, "AttackSelectionPanel", PANEL_SOLID);
        SetRT(atkSel, new Vector2(0.035f, 0.300f), new Vector2(0.965f, 0.460f));
        gameUI.attackSelectionPanel = atkSel;

        TextMeshProUGUI choose = CreateText(atkSel.transform, "ChooseLabel", "Choose attack", 16, GOLD, true);
        SetRT(choose.gameObject, new Vector2(0.04f, 0.68f), new Vector2(0.96f, 0.98f));
        choose.alignment = TextAlignmentOptions.MidlineLeft;

        Button atk1 = CreateStyledButton(atkSel.transform, "Attack1Button", "", CARD_BG, TEXT, 12);
        SetRT(atk1.gameObject, new Vector2(0.04f, 0.08f), new Vector2(0.49f, 0.66f));
        TextMeshProUGUI atk1Lbl = CreateText(atk1.transform, "Attack1Label", "Attack 1" + System.Environment.NewLine + "20 dmg | 2 mana", 16, TEXT, true);
        SetRT(atk1Lbl.gameObject, new Vector2(0.04f, 0f), new Vector2(0.96f, 1f));
        atk1Lbl.alignment = TextAlignmentOptions.Center;
        gameUI.attack1Button = atk1;
        gameUI.attack1Label = atk1Lbl;

        Button atk2 = CreateStyledButton(atkSel.transform, "Attack2Button", "", CARD_BG, TEXT, 12);
        SetRT(atk2.gameObject, new Vector2(0.51f, 0.08f), new Vector2(0.96f, 0.66f));
        TextMeshProUGUI atk2Lbl = CreateText(atk2.transform, "Attack2Label", "Attack 2" + System.Environment.NewLine + "40 dmg | 4 mana", 16, TEXT, true);
        SetRT(atk2Lbl.gameObject, new Vector2(0.04f, 0f), new Vector2(0.96f, 1f));
        atk2Lbl.alignment = TextAlignmentOptions.Center;
        gameUI.attack2Button = atk2;
        gameUI.attack2Label = atk2Lbl;
        atkSel.SetActive(false);

        // Target selection: same compact area, not covering the characters.
        GameObject enemyPanel = CreatePanel(panel.transform, "EnemyCardPanel", PANEL_SOLID);
        SetRT(enemyPanel, new Vector2(0.035f, 0.300f), new Vector2(0.965f, 0.460f));
        gameUI.enemyCardPanel = enemyPanel;

        TextMeshProUGUI targetLbl = CreateText(enemyPanel.transform, "EnemyLabel", "Select target", 16, RED, true);
        SetRT(targetLbl.gameObject, new Vector2(0.04f, 0.68f), new Vector2(0.96f, 0.98f));
        targetLbl.alignment = TextAlignmentOptions.MidlineLeft;

        GameObject enemyCont = CreateLayoutContainer(enemyPanel.transform, "EnemyCardContainer");
        SetRT(enemyCont, new Vector2(0.04f, 0.08f), new Vector2(0.96f, 0.66f));
        gameUI.enemyCardContainer = enemyCont.transform;
        enemyPanel.SetActive(false);

        // Your cards: slimmer and translucent.
        GameObject fieldStrip = CreatePanel(panel.transform, "FieldStrip", new Color(0.040f, 0.085f, 0.040f, 0.66f));
        SetRT(fieldStrip, new Vector2(0.025f, 0.185f), new Vector2(0.975f, 0.295f));
        NoRaycast(fieldStrip);

        TextMeshProUGUI fieldLbl = CreateText(fieldStrip.transform, "FieldLabel", "Your cards", 14, GREEN, true);
        SetRT(fieldLbl.gameObject, new Vector2(0.03f, 0.68f), new Vector2(0.97f, 1f));
        fieldLbl.alignment = TextAlignmentOptions.MidlineLeft;

        GameObject fieldCont = CreateLayoutContainer(fieldStrip.transform, "FieldCardContainer");
        SetRT(fieldCont, new Vector2(0.02f, 0.04f), new Vector2(0.98f, 0.70f));
        gameUI.fieldCardContainer = fieldCont.transform;

        GameObject deadPanel = CreatePanel(panel.transform, "DeadCardPanel", RED_PANEL);
        SetRT(deadPanel, new Vector2(0.025f, 0.185f), new Vector2(0.975f, 0.295f));
        gameUI.deadCardPanel = deadPanel;

        TextMeshProUGUI deadLbl = CreateText(deadPanel.transform, "DeadLabel", $"Defeated cards — {PlayerState.ReviveCost} mana to revive", 14, RED, true);
        SetRT(deadLbl.gameObject, new Vector2(0.03f, 0.68f), new Vector2(0.97f, 1f));
        deadLbl.alignment = TextAlignmentOptions.MidlineLeft;

        GameObject deadCont = CreateLayoutContainer(deadPanel.transform, "DeadCardContainer");
        SetRT(deadCont, new Vector2(0.02f, 0.04f), new Vector2(0.98f, 0.70f));
        gameUI.deadCardContainer = deadCont.transform;
        deadPanel.SetActive(false);

        // Bottom dock: smaller buttons, less dead space.
        GameObject bottom = CreatePanel(panel.transform, "BottomDock", CLEAR_PANEL);
        SetRT(bottom, new Vector2(0.025f, 0.045f), new Vector2(0.975f, 0.175f));
        NoRaycast(bottom);

        GameObject hpBox = CreatePanel(bottom.transform, "HPPill", new Color(0.190f, 0.035f, 0.035f, 0.88f));
        SetRT(hpBox, new Vector2(0.02f, 0.52f), new Vector2(0.48f, 0.94f));
        NoRaycast(hpBox);
        TextMeshProUGUI hpTxt = CreateText(hpBox.transform, "CurrentPlayerHP", "HP: 100/100", 16, RED, true);
        SetRT(hpTxt.gameObject, Vector2.zero, Vector2.one);
        hpTxt.alignment = TextAlignmentOptions.Center;
        gameUI.currentPlayerHP = hpTxt;

        GameObject mpBox = CreatePanel(bottom.transform, "ManaPill", new Color(0.035f, 0.095f, 0.180f, 0.88f));
        SetRT(mpBox, new Vector2(0.52f, 0.52f), new Vector2(0.98f, 0.94f));
        NoRaycast(mpBox);
        TextMeshProUGUI mpTxt = CreateText(mpBox.transform, "CurrentPlayerMana", "Mana: 10/10", 16, BLUE, true);
        SetRT(mpTxt.gameObject, Vector2.zero, Vector2.one);
        mpTxt.alignment = TextAlignmentOptions.Center;
        gameUI.currentPlayerMana = mpTxt;

        Button confirm = CreateStyledButton(bottom.transform, "ConfirmAttackButton", "Confirm", GREEN_BTN, GREEN, 17);
        SetRT(confirm.gameObject, new Vector2(0.02f, 0.08f), new Vector2(0.48f, 0.46f));
        gameUI.confirmAttackButton = confirm;
        confirm.gameObject.SetActive(false);

        Button end = CreateStyledButton(bottom.transform, "EndTurnButton", "End Turn", BLUE_BTN, BLUE, 17);
        SetRT(end.gameObject, new Vector2(0.52f, 0.08f), new Vector2(0.98f, 0.46f));
        gameUI.endTurnButton = end;

        panel.SetActive(false);
        return panel;
    }

    private GameObject BuildPassDevicePanel(Transform parent, GameUI gameUI)
    {
        GameObject panel = CreatePanel(parent, "PassDevicePanel", new Color(0.035f, 0.020f, 0.015f, 0.98f));
        SetRT(panel, Vector2.zero, Vector2.one);
        NoRaycast(panel);

        TextMeshProUGUI top = CreateText(panel.transform, "TopLabel", "HAND OVER DEVICE", 13, GOLD, true);
        SetRT(top.gameObject, new Vector2(0.1f, 0.87f), new Vector2(0.9f, 0.93f));
        top.alignment = TextAlignmentOptions.Center;

        TextMeshProUGUI main = CreateText(panel.transform, "PassDeviceText", "Pass to\nPlayer 2", 34, GOLD, true);
        SetRT(main.gameObject, new Vector2(0.08f, 0.54f), new Vector2(0.92f, 0.68f));
        main.alignment = TextAlignmentOptions.Center;
        gameUI.passDeviceText = main;

        TextMeshProUGUI sub = CreateText(panel.transform, "Sub", "Cover the screen, then tap when ready", 15, MUTED, false);
        SetRT(sub.gameObject, new Vector2(0.08f, 0.47f), new Vector2(0.92f, 0.53f));
        sub.alignment = TextAlignmentOptions.Center;

        Button ready = CreateStyledButton(panel.transform, "PassDeviceButton", "I'm Ready", GREEN_BTN, GREEN, 20);
        SetRT(ready.gameObject, new Vector2(0.14f, 0.34f), new Vector2(0.86f, 0.43f));
        gameUI.passDeviceButton = ready;

        panel.SetActive(false);
        return panel;
    }

    private GameObject BuildEndGamePanel(Transform parent, GameUI gameUI)
    {
        GameObject panel = CreatePanel(parent, "EndGamePanel", new Color(0.035f, 0.020f, 0.015f, 0.98f));
        SetRT(panel, Vector2.zero, Vector2.one);
        NoRaycast(panel);

        TextMeshProUGUI label = CreateText(panel.transform, "GameOverLabel", "GAME OVER", 13, GOLD, true);
        SetRT(label.gameObject, new Vector2(0.1f, 0.82f), new Vector2(0.9f, 0.88f));
        label.alignment = TextAlignmentOptions.Center;

        TextMeshProUGUI winner = CreateText(panel.transform, "WinnerText", "Player 1 Wins!", 38, GOLD, true);
        SetRT(winner.gameObject, new Vector2(0.05f, 0.55f), new Vector2(0.95f, 0.67f));
        winner.alignment = TextAlignmentOptions.Center;
        gameUI.winnerText = winner;

        Button restart = CreateStyledButton(panel.transform, "RestartButton", "Play Again", GOLD_DARK, DARK_TEXT, 22);
        SetRT(restart.gameObject, new Vector2(0.15f, 0.36f), new Vector2(0.85f, 0.46f));
        gameUI.restartButton = restart;

        panel.SetActive(false);
        return panel;
    }

    private TextMeshProUGUI BuildMessageToast(Transform parent)
    {
        GameObject toast = CreatePanel(parent, "MessageToast", PANEL_SOLID);
        SetRT(toast, new Vector2(0.06f, 0.78f), new Vector2(0.94f, 0.84f));
        NoRaycast(toast);

        TextMeshProUGUI txt = CreateText(toast.transform, "MessageText", "", 14, TEXT, true);
        SetRT(txt.gameObject, new Vector2(0.04f, 0f), new Vector2(0.96f, 1f));
        txt.alignment = TextAlignmentOptions.MidlineLeft;
        toast.SetActive(false);
        return txt;
    }

    private GameObject BuildCardButtonPrefab()
    {
        GameObject go = new GameObject("CardButtonPrefab");
        RectTransform rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(160f, 62f);

        Image img = go.AddComponent<Image>();
        img.color = CARD_BG;

        Button btn = go.AddComponent<Button>();
        ColorBlock cb = btn.colors;
        cb.normalColor      = CARD_BG;
        cb.highlightedColor = new Color(0.16f, 0.11f, 0.06f, 0.96f);
        cb.pressedColor     = new Color(0.06f, 0.04f, 0.02f, 0.96f);
        cb.disabledColor    = new Color(0.05f, 0.04f, 0.03f, 0.42f);
        btn.colors = cb;

        GameObject stripe = CreatePanel(go.transform, "Stripe", GOLD_DARK);
        SetRT(stripe, Vector2.zero, new Vector2(0.035f, 1f));
        NoRaycast(stripe);

        TextMeshProUGUI title = CreateText(go.transform, "CardName", "Card", 16, TEXT, true);
        SetRT(title.gameObject, new Vector2(0.07f, 0.45f), new Vector2(0.97f, 0.94f));
        title.alignment = TextAlignmentOptions.MidlineLeft;

        TextMeshProUGUI sub = CreateText(go.transform, "ActionLabel", "Action", 12, MUTED, true);
        SetRT(sub.gameObject, new Vector2(0.07f, 0.08f), new Vector2(0.97f, 0.45f));
        sub.alignment = TextAlignmentOptions.MidlineLeft;

        string path = "Assets/Resources/Prefabs/CardButtonPrefab.prefab";
        System.IO.Directory.CreateDirectory("Assets/Resources/Prefabs");
        GameObject pf = PrefabUtility.SaveAsPrefabAsset(go, path);
        DestroyImmediate(go);
        return pf;
    }

    private void ConfigureCanvasForPhoneAR(Canvas canvas)
    {
        CanvasScaler scaler = canvas.GetComponent<CanvasScaler>() ?? canvas.gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1080f, 1920f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;
    }

    private Button CreateStyledButton(Transform parent, string objName, string label, Color bg, Color fg, int fontSize)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        Image img = go.AddComponent<Image>();
        img.color = bg;

        Button btn = go.AddComponent<Button>();
        ColorBlock c = btn.colors;
        c.normalColor      = bg;
        c.highlightedColor = Brighten(bg, 0.08f);
        c.pressedColor     = Darken(bg, 0.20f);
        c.disabledColor    = new Color(bg.r, bg.g, bg.b, 0.42f);
        btn.colors = c;

        if (!string.IsNullOrEmpty(label))
        {
            TextMeshProUGUI t = CreateText(go.transform, "Text", label, fontSize, fg, true);
            SetRT(t.gameObject, Vector2.zero, Vector2.one);
            t.alignment = TextAlignmentOptions.Center;
        }
        return btn;
    }

    private GameObject CreatePanel(Transform parent, string objName, Color color)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        Image img = go.AddComponent<Image>();
        img.color = color;
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
        h.spacing = 6f;
        h.childAlignment = TextAnchor.MiddleCenter;
        h.childForceExpandWidth = false;
        h.childForceExpandHeight = true;
        h.padding = new RectOffset(5, 5, 2, 2);
        return go;
    }

    private TextMeshProUGUI CreateText(Transform parent, string objName, string content, int fontSize, Color color, bool bold)
    {
        GameObject go = new GameObject(objName);
        go.transform.SetParent(parent, false);
        go.AddComponent<RectTransform>();
        TextMeshProUGUI t = go.AddComponent<TextMeshProUGUI>();
        t.text = content;
        t.fontSize = Mathf.RoundToInt(fontSize * FONT_SCALE);
        t.color = color;
        t.fontStyle = bold ? FontStyles.Bold : FontStyles.Normal;
        t.enableWordWrapping = true;
        t.raycastTarget = false;
        return t;
    }

    private void SetRT(GameObject go, Vector2 min, Vector2 max)
    {
        RectTransform rt = go.GetComponent<RectTransform>() ?? go.AddComponent<RectTransform>();
        rt.anchorMin = min;
        rt.anchorMax = max;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private void NoRaycast(GameObject go)
    {
        Image img = go.GetComponent<Image>();
        if (img != null) img.raycastTarget = false;
    }

    private Color Brighten(Color c, float amount) =>
        new Color(Mathf.Min(c.r + amount, 1f), Mathf.Min(c.g + amount, 1f), Mathf.Min(c.b + amount, 1f), c.a);

    private Color Darken(Color c, float amount) =>
        new Color(c.r * (1f - amount), c.g * (1f - amount), c.b * (1f - amount), c.a);
}
#endif
