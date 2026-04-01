/*
 * EvalBar.cs
 * Chess.com-style vertical evaluation bar.
 *
 * SETUP:
 *   Add this script to any GameObject (e.g. an empty called "EvalBar").
 *   It builds all UI at runtime — no prefabs needed.
 *
 * How it works:
 *   - Calls EvaluatePosition() every time a move is made using the
 *     same material + PST scoring as ChessAI_Advanced.
 *   - Smoothly animates the white/black split using Lerp.
 *   - Shows "+3.5" style text (or "M4" for forced mate).
 *   - Positive = White is winning, Negative = Black is winning.
 */

using UnityEngine;
using UnityEngine.UI;

public class EvalBar : MonoBehaviour
{
    // ── Settings ──────────────────────────────────────────────
    [Header("Position (anchored to left edge)")]
    public float barWidth = 28f;
    public float barHeight = 560f;

    // ── Colors ────────────────────────────────────────────────
    private static readonly Color WHITE_COL = new Color(0.95f, 0.95f, 0.92f);
    private static readonly Color BLACK_COL = new Color(0.12f, 0.12f, 0.12f);
    private static readonly Color BG_COL = new Color(0.06f, 0.06f, 0.06f);

    // ── UI references ─────────────────────────────────────────
    private RectTransform whiteSection;   // grows from bottom
    private Text evalText;
    private Canvas canvas;

    // ── State ─────────────────────────────────────────────────
    private float targetFill = 0.5f;   // 0 = all black, 1 = all white
    private float currentFill = 0.5f;
    private float rawEval = 0f;     // centipawn eval
    private const float SMOOTH = 6f;    // lerp speed

    // ── Piece values for inline evaluation ───────────────────
    private static readonly int[] PVAL = { 0, 100, 320, 330, 500, 900, 20000 };

    // ── Piece-Square Table (pawn, used for eval richness) ─────
    private static readonly int[] PST_PAWN = {
         0,  0,  0,  0,  0,  0,  0,  0,
        50, 50, 50, 50, 50, 50, 50, 50,
        10, 10, 20, 30, 30, 20, 10, 10,
         5,  5, 10, 25, 25, 10,  5,  5,
         0,  0,  0, 20, 20,  0,  0,  0,
         5, -5,-10,  0,  0,-10, -5,  5,
         5, 10, 10,-20,-20, 10, 10,  5,
         0,  0,  0,  0,  0,  0,  0,  0
    };

    void Awake()
    {
        BuildUI();
    }

    void Update()
    {
        // Smooth animation
        currentFill = Mathf.Lerp(currentFill, targetFill, Time.deltaTime * SMOOTH);

        if (whiteSection != null)
            whiteSection.sizeDelta = new Vector2(barWidth, currentFill * barHeight);

        // Update eval text
        UpdateEvalText();
    }

    // ══════════════════════════════════════════════════════════
    //  Public API — call this after every move
    // ══════════════════════════════════════════════════════════
    public void Refresh()
    {
        rawEval = ComputeEval();
        targetFill = EvalToFill(rawEval);
    }

    // ══════════════════════════════════════════════════════════
    //  Evaluation  (centipawns, positive = White winning)
    // ══════════════════════════════════════════════════════════
    private float ComputeEval()
    {
        var gm = GameManager.instance;
        if (gm == null) return 0f;

        int score = 0;
        int[] wPawns = new int[8], bPawns = new int[8];

        for (int col = 0; col < 8; col++)
            for (int row = 0; row < 8; row++)
            {
                var go = gm.PieceAtGrid(new Vector2Int(col, row));
                if (go == null) continue;
                var pc = go.GetComponent<Piece>();
                if (pc == null) continue;

                bool isWhite = gm.white.pieces.Contains(go);
                int sign = isWhite ? 1 : -1;

                // Material
                int ps = PVAL[(int)pc.type + 1 > 6 ? 6 : (int)pc.type + 1];

                // Light PST bonus for pawns only (fast)
                if (pc.type == PieceType.Pawn)
                {
                    int tableRow = isWhite ? row : (7 - row);
                    ps += PST_PAWN[tableRow * 8 + col];
                    if (isWhite) wPawns[col]++; else bPawns[col]++;
                }

                score += sign * ps;
            }

        // Doubled pawn penalty
        for (int f = 0; f < 8; f++)
        {
            if (wPawns[f] > 1) score -= 15 * (wPawns[f] - 1);
            if (bPawns[f] > 1) score += 15 * (bPawns[f] - 1);
        }

        return score / 100f;   // convert to pawns
    }

    // Maps eval (in pawns) to a 0-1 fill fraction.
    // Uses a sigmoid-like curve so ±5 pawns looks nearly decisive.
    private float EvalToFill(float evalPawns)
    {
        // Sigmoid: fill = 1 / (1 + e^(-k * eval))
        float k = 0.4f;
        return 1f / (1f + Mathf.Exp(-k * evalPawns));
    }

    private void UpdateEvalText()
    {
        if (evalText == null) return;

        float abs = Mathf.Abs(rawEval);
        string prefix = rawEval >= 0 ? "+" : "-";
        if (abs > 900) evalText.text = rawEval > 0 ? "WIN" : "WIN";
        else evalText.text = prefix + abs.ToString("0.0");

        // Position text above the white section if black is winning, below if white is winning
        var rt = evalText.GetComponent<RectTransform>();
        bool whiteWinning = rawEval >= 0;
        rt.anchorMin = rt.anchorMax = whiteWinning ? new Vector2(0.5f, 0f) : new Vector2(0.5f, 1f);
        rt.anchoredPosition = whiteWinning ? new Vector2(0, currentFill * barHeight + 6f)
                                           : new Vector2(0, -(1f - currentFill) * barHeight - 6f);
        evalText.color = whiteWinning ? BLACK_COL : WHITE_COL;
    }

    // ══════════════════════════════════════════════════════════
    //  Build UI
    // ══════════════════════════════════════════════════════════
    private void BuildUI()
    {
        // Canvas
        var canvasGO = new GameObject("EvalBarCanvas");
        canvasGO.transform.SetParent(transform, false);
        canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;
        canvasGO.AddComponent<CanvasScaler>().uiScaleMode =
            CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasGO.GetComponent<CanvasScaler>().referenceResolution = new Vector2(1920, 1080);

        // Outer container — left edge, vertically centered
        var container = new GameObject("Container");
        container.transform.SetParent(canvasGO.transform, false);
        var crt = container.AddComponent<RectTransform>();
        crt.anchorMin = new Vector2(0f, 0.5f);
        crt.anchorMax = new Vector2(0f, 0.5f);
        crt.pivot = new Vector2(0f, 0.5f);
        crt.anchoredPosition = new Vector2(18f, 0f);
        crt.sizeDelta = new Vector2(barWidth, barHeight);

        // Background (full black bar)
        var bgGO = new GameObject("BG"); bgGO.transform.SetParent(container.transform, false);
        var bgImg = bgGO.AddComponent<Image>(); bgImg.color = BLACK_COL;
        var bgRt = bgGO.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero; bgRt.anchorMax = Vector2.one; bgRt.sizeDelta = Vector2.zero;

        // White section (grows from bottom)
        var wsGO = new GameObject("WhiteSection"); wsGO.transform.SetParent(container.transform, false);
        var wsImg = wsGO.AddComponent<Image>(); wsImg.color = WHITE_COL;
        var wsRt = wsGO.GetComponent<RectTransform>();
        wsRt.anchorMin = new Vector2(0f, 0f);
        wsRt.anchorMax = new Vector2(1f, 0f);
        wsRt.pivot = new Vector2(0.5f, 0f);
        wsRt.anchoredPosition = Vector2.zero;
        wsRt.sizeDelta = new Vector2(0f, barHeight * 0.5f);
        whiteSection = wsRt;

        // Centre line
        var lineGO = new GameObject("CentreLine"); lineGO.transform.SetParent(container.transform, false);
        var lineImg = lineGO.AddComponent<Image>(); lineImg.color = new Color(0.4f, 0.4f, 0.4f, 0.5f);
        var lrt = lineGO.GetComponent<RectTransform>();
        lrt.anchorMin = new Vector2(0f, 0.5f); lrt.anchorMax = new Vector2(1f, 0.5f);
        lrt.pivot = new Vector2(0.5f, 0.5f);
        lrt.anchoredPosition = Vector2.zero; lrt.sizeDelta = new Vector2(0f, 1f);

        // Eval text (floats just outside the white/black boundary)
        var textGO = new GameObject("EvalText"); textGO.transform.SetParent(container.transform, false);
        evalText = textGO.AddComponent<Text>();
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf")
                  ?? Resources.GetBuiltinResource<Font>("Arial.ttf");
        evalText.font = font;
        evalText.fontSize = 13;
        evalText.fontStyle = FontStyle.Bold;
        evalText.alignment = TextAnchor.MiddleCenter;
        evalText.color = BLACK_COL;
        var ert = evalText.GetComponent<RectTransform>();
        ert.sizeDelta = new Vector2(barWidth + 10f, 22f);
        ert.pivot = new Vector2(0.5f, 0f);
    }
}
