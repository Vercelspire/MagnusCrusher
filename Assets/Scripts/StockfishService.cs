/*
 * StockfishService.cs
 *  • AutoPlay()  — Stockfish plays Black's move automatically.
 *  • ShowHint()  — Gets best move for WHOEVER is currently to move,
 *                  shows a green arrow. Called after EVERY move in hint mode.
 *  
 * 
 */

using System.Collections;
using UnityEngine;
using UnityEngine.Networking;

public class StockfishService : MonoBehaviour
{
    [Header("Stockfish API")]
    [Range(1, 15)]
    public int depth = 15;
    public float timeout = 8f;

    // stockfish API for EVAL
    private const string URL = "https://stockfish.online/api/s/v2.php";

    private EvalBar evalBar;
    void Start() { evalBar = FindObjectOfType<EvalBar>(); }

    // Auto-play Stockfish moves for Black 
    public IEnumerator AutoPlay()
    {
        string fen = GameManager.instance.GetFEN();
        yield return Fetch(fen, hintOnly: false);
    }

    // Hint: show arrow for current side to move
    public IEnumerator ShowHint()
    {
        string fen = GameManager.instance.GetFEN();
        yield return Fetch(fen, hintOnly: true);
    }

    // fetch stockfish
    private IEnumerator Fetch(string fen, bool hintOnly)
    {
        string url = $"{URL}?fen={UnityWebRequest.EscapeURL(fen)}&depth={depth}";

        using var req = UnityWebRequest.Get(url);
        req.timeout = (int)timeout;
        yield return req.SendWebRequest();

        if (req.result != UnityWebRequest.Result.Success)
        {
            Debug.LogWarning($"[Stockfish] Network error: {req.error}");
            Fallback(hintOnly);
            yield break;
        }

        string json = req.downloadHandler.text;

        if (!TryParse(json, out string bestMove, out float eval))
        {
            Debug.LogWarning("[Stockfish] Parse failed");
            Fallback(hintOnly);
            yield break;
        }

        if (!TryUCI(bestMove, out Vector2Int from, out Vector2Int to))
        {
            Debug.LogWarning($"[Stockfish] Bad move string: '{bestMove}'");
            Fallback(hintOnly);
            yield break;
        }

        evalBar?.Refresh();

        if (hintOnly)
        {
            // Draw arrow, doesn't auto execute
            GameManager.instance.ShowHintArrow(from, to);
            Debug.Log($"[Stockfish Hint] {bestMove}  eval={eval}");
        }
        else
        {
            // Execute Black's move
            var gm = GameManager.instance;
            var piece = gm.PieceAtGrid(from);
            if (piece == null)
            {
                Debug.LogWarning($"[Stockfish] No piece at {from}. FEN was: {fen}");
                Fallback(false);
                yield break;
            }
            gm.Move(piece, to);
            evalBar?.Refresh();
            gm.NextPlayer();
            Debug.Log($"[Stockfish Auto] Played {bestMove}  eval={eval}");
        }
    }

    // Parse JSON
    private bool TryParse(string json, out string bestMove, out float eval)
    {
        bestMove = null; eval = 0f;
        try
        {
            var r = JsonUtility.FromJson<Resp>(json);
            if (r != null && r.success && !string.IsNullOrEmpty(r.bestmove))
            { bestMove = r.bestmove.Split(' ')[0]; eval = r.evaluation; return true; }
        }
        catch { }

        // Manual fallback
        int idx = json.IndexOf("\"bestmove\"");
        if (idx >= 0)
        {
            int q1 = json.IndexOf('"', idx + 10) + 1;
            int q2 = json.IndexOf('"', q1);
            if (q1 > 0 && q2 > q1)
            {
                bestMove = json.Substring(q1, q2 - q1).Split(' ')[0];
                return !string.IsNullOrEmpty(bestMove);
            }
        }
        return false;
    }

    // ── Parse UCI move "e2e4" → grid coords ──────────────────
    private bool TryUCI(string uci, out Vector2Int from, out Vector2Int to)
    {
        from = to = Vector2Int.zero;
        if (uci == null || uci.Length < 4) return false;
        int fc = uci[0] - 'a', fr = uci[1] - '1', tc = uci[2] - 'a', tr = uci[3] - '1';
        if (fc < 0 || fc > 7 || fr < 0 || fr > 7 || tc < 0 || tc > 7 || tr < 0 || tr > 7) return false;
        from = new Vector2Int(fc, fr);
        to = new Vector2Int(tc, tr);
        return true;
    }

    // ── Fallback to local AI ──────────────────────────────────
    private void Fallback(bool hintOnly)
    {
        var ai = GetComponent<ChessAI_Advanced>();
        if (ai == null) return;

        if (hintOnly)
        {
            var m = ai.GetBestMoveForHint();
            if (m != null)
                GameManager.instance.ShowHintArrow(
                    new Vector2Int(m[0], m[1]), new Vector2Int(m[2], m[3]));
        }
        else
        {
            ai.TakeTurn();
            evalBar?.Refresh();
            GameManager.instance.NextPlayer();
        }
    }

    [System.Serializable]
    private class Resp
    {
        public bool success;
        public float evaluation;
        public string bestmove;
    }
}
