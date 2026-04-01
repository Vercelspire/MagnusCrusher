/*
 * ChessUI.cs
 *
 */

using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public class ChessUI : MonoBehaviour
{
    public static ChessUI instance;

    [Header("Font")]
    [Tooltip("Filename of .ttf inside Assets/Resources/Fonts/ (no extension). " +
             "Leave blank to use Unity built-in. Recommended: Orbitron-Regular")]
    public string fontName = "Orbitron-Regular";

    // Colors (chess.com dark theme) 
    private static readonly Color BG_PANEL = new Color(0.13f, 0.15f, 0.17f, 1f);
    private static readonly Color BG_DARK = new Color(0.08f, 0.09f, 0.10f, 1f);
    private static readonly Color BTN_ACTIVE = new Color(0.35f, 0.70f, 0.30f, 1f);
    private static readonly Color BTN_IDLE = new Color(0.20f, 0.23f, 0.26f, 1f);
    private static readonly Color BTN_HOVER = new Color(0.28f, 0.32f, 0.36f, 1f);
    private static readonly Color TXT_MAIN = new Color(0.92f, 0.92f, 0.90f, 1f);
    private static readonly Color TXT_DIM = new Color(0.50f, 0.54f, 0.58f, 1f);
    private static readonly Color CHECK_RED = new Color(0.92f, 0.18f, 0.18f, 1f);
    private static readonly Color ACCENT = new Color(0.35f, 0.70f, 0.30f, 1f);

    // UI refs
    private Text statusText, turnLabel, apiStatusText;
    private Button btnOurAI, btnStockfishAuto, btnStockfishHint;
    private RectTransform moveListContent;
    private int fullMoveNumber = 1;
    private List<Text> moveEntries = new List<Text>();
    private EvalBar evalBar;

    // Font cache
    private Font cachedFont;

    private Font GetFont()
    {
        if (cachedFont != null) return cachedFont;

        // Try custom font from Resources/Fonts/
        if (!string.IsNullOrEmpty(fontName))
            cachedFont = Resources.Load<Font>($"Fonts/{fontName}");

        // Unity built-in fallbacks
        if (cachedFont == null)
            cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        if (cachedFont == null)
            cachedFont = Resources.GetBuiltinResource<Font>("Arial.ttf");

        // Whatever Unity has loaded
        if (cachedFont == null)
        {
            var all = Resources.FindObjectsOfTypeAll<Font>();
            if (all.Length > 0) cachedFont = all[0];
        }

        return cachedFont;
    }

    void Awake() { instance = this; }

    void Start()
    {
        // Unity UI buttons REQUIRE an EventSystem in the scene.
        // The Razeware project never creates one, so buttons silently
        // do nothing. We create it here if it's missing.
        EnsureEventSystem();

        evalBar = FindObjectOfType<EvalBar>();
        BuildUI();
        SetModeButton(AIMode.OurAI);
    }

    // EventSystem creation 
    private void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;   // already exists

        var esGO = new GameObject("EventSystem");
        esGO.AddComponent<EventSystem>();
        esGO.AddComponent<StandaloneInputModule>();
        Debug.Log("[ChessUI] Created missing EventSystem — UI buttons now work.");
    }

    //  Public API
    public void OnMoveMade(string moveNotation, bool isWhiteMove, bool isCheck)
    {
        RecordMove(moveNotation, isWhiteMove);
        evalBar?.Refresh();

        bool humanTurn = GameManager.instance.currentPlayer == GameManager.instance.white;
        string who = isCheck ? "CHECK! " + (humanTurn ? "Your turn" : "AI thinking…")
                             : (humanTurn ? "Your turn" : "AI thinking…");

        if (statusText) { statusText.text = who; statusText.color = isCheck ? CHECK_RED : TXT_MAIN; }
        if (turnLabel)
            turnLabel.text = GameManager.instance.currentPlayer == GameManager.instance.white
                ? "WHITE TO MOVE" : "BLACK TO MOVE";
    }

    public void SetAPIStatus(string msg)
    {
        if (apiStatusText) apiStatusText.text = msg;
    }

    //  Move List
    private void RecordMove(string notation, bool isWhiteMove)
    {
        if (moveListContent == null) return;

        if (isWhiteMove)
        {
            MakeMoveRow(fullMoveNumber, notation);
        }
        else
        {
            if (moveEntries.Count > 0 && moveEntries[moveEntries.Count - 1] != null)
                moveEntries[moveEntries.Count - 1].text += "   " + notation;
            fullMoveNumber++;
        }

        Canvas.ForceUpdateCanvases();
        var sr = moveListContent.GetComponentInParent<ScrollRect>();
        if (sr != null) sr.verticalNormalizedPosition = 0f;
    }

    private Text MakeMoveRow(int num, string whiteMove)
    {
        // Row GO — Image background only
        var rowGO = new GameObject($"Move{num}");
        rowGO.transform.SetParent(moveListContent, false);
        var img = rowGO.AddComponent<Image>();
        img.color = num % 2 == 0
            ? new Color(0.12f, 0.14f, 0.16f)
            : new Color(0.16f, 0.18f, 0.20f);
        var rowRt = rowGO.GetComponent<RectTransform>();
        rowRt.sizeDelta = new Vector2(0f, 28f);

        // Label GO — Text only (must be a CHILD, not same GO as Image)
        var lblGO = new GameObject("Label");
        lblGO.transform.SetParent(rowGO.transform, false);
        var t = lblGO.AddComponent<Text>();
        t.text = $"  {num}.   {whiteMove}";
        t.font = GetFont();
        t.fontSize = 15;
        t.color = TXT_MAIN;
        t.alignment = TextAnchor.MiddleLeft;
        var lblRt = lblGO.GetComponent<RectTransform>();
        lblRt.anchorMin = Vector2.zero;
        lblRt.anchorMax = Vector2.one;
        lblRt.sizeDelta = Vector2.zero;
        lblRt.offsetMin = new Vector2(8f, 0f);
        lblRt.offsetMax = Vector2.zero;

        moveEntries.Add(t);
        return t;
    }
    //  Mode Buttons
    private void SetModeButton(AIMode mode)
    {
        GameManager.instance?.SetMode(mode);

        if (btnOurAI) SetBtnActive(btnOurAI, mode == AIMode.OurAI);
        if (btnStockfishAuto) SetBtnActive(btnStockfishAuto, mode == AIMode.StockfishAuto);
        if (btnStockfishHint) SetBtnActive(btnStockfishHint, mode == AIMode.StockfishHint);

        if (apiStatusText)
            apiStatusText.text = mode switch
            {
                AIMode.OurAI => "Our AI is playing Black",
                AIMode.StockfishAuto => "Stockfish (online) is playing Black",
                AIMode.StockfishHint => "Stockfish hint arrow after your move",
                _ => ""
            };
    }

    private void SetBtnActive(Button btn, bool active)
    {
        var img = btn.GetComponent<Image>();
        if (img) img.color = active ? BTN_ACTIVE : BTN_IDLE;
        var t = btn.GetComponentInChildren<Text>();
        if (t) t.color = active ? Color.white : TXT_DIM;
    }

    //  Build UI
    private void BuildUI()
    {
        // Root canvas
        var canvasGO = new GameObject("ChessUICanvas");
        canvasGO.transform.SetParent(transform, false);
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        canvasGO.AddComponent<GraphicRaycaster>();
        var sc = canvasGO.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);

        // ── Right panel ──────────────────────────────────────
        var panel = NewGO("SidePanel", canvasGO);
        panel.AddComponent<Image>().color = BG_PANEL;
        var panelRt = panel.GetComponent<RectTransform>();
        panelRt.anchorMin = new Vector2(1f, 0f);
        panelRt.anchorMax = new Vector2(1f, 1f);
        panelRt.pivot = new Vector2(1f, 0.5f);
        panelRt.anchoredPosition = Vector2.zero;
        panelRt.sizeDelta = new Vector2(290f, 0f);

        float y = -16f;

        // Black player
        AddPlayerLabel(panel, "BLACK", "2900+ ELO", y, ACCENT); y -= 56f;

        // Thin accent line
        AddColorBar(panel, ACCENT, y, 2f); y -= 10f;

        // AI MODE header
        AddSectionHeader(panel, "AI MODE", y); y -= 28f;

        // Mode buttons — each wired up here
        btnOurAI = AddModeBtn(panel, "OUR AI", y); y -= 42f;
        btnStockfishAuto = AddModeBtn(panel, "STOCKFISH AUTO", y); y -= 42f;
        btnStockfishHint = AddModeBtn(panel, "STOCKFISH HINT ◆", y); y -= 42f;

        // Wire up onClick AFTER creating buttons
        btnOurAI.onClick.AddListener(() => SetModeButton(AIMode.OurAI));
        btnStockfishAuto.onClick.AddListener(() => SetModeButton(AIMode.StockfishAuto));
        btnStockfishHint.onClick.AddListener(() => SetModeButton(AIMode.StockfishHint));

        apiStatusText = AddSmallText(panel, "Our AI is playing Black", y, TXT_DIM); y -= 26f;

        AddColorBar(panel, new Color(1f, 1f, 1f, 0.06f), y, 1f); y -= 14f;

        // Move list
        AddSectionHeader(panel, "MOVES", y); y -= 28f;
        BuildMoveList(panel, y); y -= 350f;

        AddColorBar(panel, new Color(1f, 1f, 1f, 0.06f), y, 1f); y -= 14f;

        // Status
        statusText = AddLargeText(panel, "YOUR TURN", y, ACCENT); y -= 38f;
        turnLabel = AddSmallText(panel, "WHITE TO MOVE", y, TXT_DIM); y -= 32f;

        AddColorBar(panel, ACCENT, y, 2f); y -= 10f;

        // White player
        AddPlayerLabel(panel, "YOU (WHITE)", "HUMAN", y, TXT_DIM);

        // Top bar
        var topBar = NewGO("TopBar", canvasGO);
        topBar.AddComponent<Image>().color = BG_DARK;
        var topBarRt = topBar.GetComponent<RectTransform>();
        topBarRt.anchorMin = new Vector2(0f, 1f);
        topBarRt.anchorMax = new Vector2(1f, 1f);
        topBarRt.pivot = new Vector2(0.5f, 1f);
        topBarRt.anchoredPosition = Vector2.zero;
        topBarRt.sizeDelta = new Vector2(0f, 46f);

        var titleGO = NewGO("Title", topBar);
        var titleTxt = titleGO.AddComponent<Text>();
        titleTxt.text = "CHESS  ·  HUMAN VS AI";
        titleTxt.font = GetFont();
        titleTxt.fontSize = 18;
        titleTxt.color = TXT_MAIN;
        titleTxt.alignment = TextAnchor.MiddleCenter;
        var trt = titleGO.GetComponent<RectTransform>();
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.sizeDelta = Vector2.zero;

        // Thin green line under top bar
        var barLine = NewGO("AccentLine", topBar);
        barLine.AddComponent<Image>().color = ACCENT;
        var blrt = barLine.GetComponent<RectTransform>();
        blrt.anchorMin = new Vector2(0, 0); blrt.anchorMax = new Vector2(1, 0);
        blrt.pivot = new Vector2(0.5f, 1f); blrt.sizeDelta = new Vector2(0, 2f);
        blrt.anchoredPosition = Vector2.zero;
    }

    // UI 

    private void AddPlayerLabel(GameObject parent, string name, string rating, float y, Color nameCol)
    {
        var go = NewGO("PlayerLabel", parent);
        SetRT(go.GetComponent<RectTransform>(), y, 52f);

        var nameGO = NewGO("Name", go);
        var nameT = nameGO.AddComponent<Text>();
        nameT.text = name; nameT.font = GetFont(); nameT.fontSize = 17;
        nameT.color = nameCol; nameT.fontStyle = FontStyle.Bold;
        nameT.alignment = TextAnchor.MiddleLeft;
        var nrt = nameGO.GetComponent<RectTransform>();
        nrt.anchorMin = new Vector2(0, 0.5f); nrt.anchorMax = new Vector2(1, 1f);
        nrt.offsetMin = new Vector2(16, 0); nrt.offsetMax = Vector2.zero;

        var rateGO = NewGO("Rating", go);
        var rateT = rateGO.AddComponent<Text>();
        rateT.text = rating; rateT.font = GetFont(); rateT.fontSize = 13;
        rateT.color = TXT_DIM; rateT.alignment = TextAnchor.MiddleLeft;
        var rrt = rateGO.GetComponent<RectTransform>();
        rrt.anchorMin = new Vector2(0, 0); rrt.anchorMax = new Vector2(1, 0.5f);
        rrt.offsetMin = new Vector2(16, 0); rrt.offsetMax = Vector2.zero;
    }

    private Button AddModeBtn(GameObject parent, string label, float y)
    {
        var go = NewGO("Btn_" + label, parent);
        var img = go.AddComponent<Image>();
        img.color = BTN_IDLE;

        // Button colour transitions
        // gives visual (feedback on hover/click)
        var btn = go.AddComponent<Button>();
        var cb = btn.colors;
        cb.normalColor = BTN_IDLE;
        cb.highlightedColor = BTN_HOVER;
        cb.pressedColor = BTN_ACTIVE;
        cb.selectedColor = BTN_IDLE;
        btn.colors = cb;

        var rt = go.GetComponent<RectTransform>();
        SetRT(rt, y, 36f);
        rt.offsetMin = new Vector2(14f, rt.offsetMin.y);
        rt.offsetMax = new Vector2(-14f, rt.offsetMax.y);

        // Label child
        var lblGO = NewGO("Label", go);
        var t = lblGO.AddComponent<Text>();
        t.text = label; t.font = GetFont(); t.fontSize = 14;
        t.color = TXT_DIM; t.alignment = TextAnchor.MiddleCenter;
        var lrt = lblGO.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.sizeDelta = Vector2.zero;

        return btn;
    }

    private void AddSectionHeader(GameObject parent, string title, float y)
    {
        var go = NewGO("Header_" + title, parent);
        var t = go.AddComponent<Text>();
        t.text = title; t.font = GetFont(); t.fontSize = 11;
        t.color = TXT_DIM; t.fontStyle = FontStyle.Bold;
        t.alignment = TextAnchor.MiddleLeft;
        var rt = go.GetComponent<RectTransform>();
        SetRT(rt, y, 22f);
        rt.offsetMin = new Vector2(16, rt.offsetMin.y);
    }

    private Text AddSmallText(GameObject parent, string text, float y, Color col)
    {
        var go = NewGO("SmallTxt", parent);
        var t = go.AddComponent<Text>();
        t.text = text; t.font = GetFont(); t.fontSize = 13;
        t.color = col; t.alignment = TextAnchor.MiddleLeft;
        var rt = go.GetComponent<RectTransform>();
        SetRT(rt, y, 22f);
        rt.offsetMin = new Vector2(16, rt.offsetMin.y);
        return t;
    }

    private Text AddLargeText(GameObject parent, string text, float y, Color col)
    {
        var go = NewGO("LargeTxt", parent);
        var t = go.AddComponent<Text>();
        t.text = text; t.font = GetFont(); t.fontSize = 18;
        t.color = col; t.fontStyle = FontStyle.Bold; t.alignment = TextAnchor.MiddleLeft;
        var rt = go.GetComponent<RectTransform>();
        SetRT(rt, y, 32f);
        rt.offsetMin = new Vector2(16, rt.offsetMin.y);
        return t;
    }

    // color bar for the eval
    private void AddColorBar(GameObject parent, Color col, float y, float h)
    {
        var go = NewGO("ColorBar", parent);
        go.AddComponent<Image>().color = col;
        SetRT(go.GetComponent<RectTransform>(), y, h);
    }


    // build move list
    private void BuildMoveList(GameObject parent, float y)
    {
        var sv = NewGO("MoveScroll", parent);
        var svRt = sv.GetComponent<RectTransform>();
        SetRT(svRt, y, 340f);
        sv.AddComponent<Image>().color = BG_DARK;

        var scroll = sv.AddComponent<ScrollRect>();
        scroll.horizontal = false;

        var vp = NewGO("Viewport", sv);
        var vpRt = vp.GetComponent<RectTransform>();
        vpRt.anchorMin = Vector2.zero; vpRt.anchorMax = Vector2.one;
        vpRt.sizeDelta = Vector2.zero; vpRt.offsetMin = Vector2.zero;
        vp.AddComponent<Image>().color = Color.clear;
        vp.AddComponent<Mask>().showMaskGraphic = false;
        scroll.viewport = vpRt;

        var content = NewGO("Content", vp);
        moveListContent = content.GetComponent<RectTransform>();
        moveListContent.anchorMin = new Vector2(0f, 1f);
        moveListContent.anchorMax = new Vector2(1f, 1f);
        moveListContent.pivot = new Vector2(0.5f, 1f);
        moveListContent.anchoredPosition = Vector2.zero;
        moveListContent.sizeDelta = Vector2.zero;

        var vlg = content.AddComponent<VerticalLayoutGroup>();
        vlg.childControlHeight = false;
        vlg.childControlWidth = true;
        vlg.childForceExpandWidth = true;
        vlg.spacing = 0f;
        content.AddComponent<ContentSizeFitter>().verticalFit =
            ContentSizeFitter.FitMode.PreferredSize;

        scroll.content = moveListContent;
    }

    // Rect helpers 
    private void SetRT(RectTransform rt, float topY, float height)
    {
        rt.anchorMin = new Vector2(0f, 1f);
        rt.anchorMax = new Vector2(1f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, topY);
        rt.sizeDelta = new Vector2(0f, height);
    }

    private GameObject NewGO(string name, GameObject parent)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.AddComponent<RectTransform>();
        return go;
    }
}