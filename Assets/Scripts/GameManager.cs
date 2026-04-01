/*
 * GameManager.cs — patch
 *
 */

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public enum AIMode { OurAI, StockfishAuto, StockfishHint }

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    public Board board;
    public GameObject whiteKing, whiteQueen, whiteBishop, whiteKnight, whiteRook, whitePawn;
    public GameObject blackKing, blackQueen, blackBishop, blackKnight, blackRook, blackPawn;

    private GameObject[,] pieces;
    public Player white, black;
    public Player currentPlayer, otherPlayer;

    private ChessAI_Advanced ourAI;
    private StockfishService stockfishSvc;

    public AIMode currentMode { get; private set; } = AIMode.OurAI;

    // State flags
    // all input checks this
    public bool gameOver { get; private set; } = false;
    private bool aiPending = false;
    private bool aiRunning = false;
    private bool wKingMoved, wRookAMoved, wRookHMoved;
    private bool bKingMoved, bRookAMoved, bRookHMoved;
    private GameObject hintArrow;
    private GameObject goCanvas;
    private Text goText;
    void Awake() { instance = this; }

    void Start()
    {
        pieces = new GameObject[8, 8];
        white = new Player("white", true);
        black = new Player("black", false);
        currentPlayer = white;
        otherPlayer = black;

        ourAI = GetComponent<ChessAI_Advanced>();
        stockfishSvc = GetComponent<StockfishService>();

        InitialSetup();
        BuildGameOverUI();
    }

    //  MODE SWITCHING
    public void SetMode(AIMode mode)
    {
        if (gameOver) return;
        currentMode = mode;
        ClearHintArrow();
        aiPending = false;
        StopAllCoroutines();
        aiRunning = false;
        Debug.Log($"[GM] Mode → {mode}");

        // Re-evaluate current state after mode change
        if (mode == AIMode.StockfishHint)
            RequestHint();
        else if (currentPlayer == black &&
                (mode == AIMode.OurAI || mode == AIMode.StockfishAuto))
            aiPending = true;
    }

    //  BOARD SETUP
    private void InitialSetup()
    {
        AddPiece(whiteRook, white, 0, 0); AddPiece(whiteKnight, white, 1, 0);
        AddPiece(whiteBishop, white, 2, 0); AddPiece(whiteQueen, white, 3, 0);
        AddPiece(whiteKing, white, 4, 0); AddPiece(whiteBishop, white, 5, 0);
        AddPiece(whiteKnight, white, 6, 0); AddPiece(whiteRook, white, 7, 0);
        for (int i = 0; i < 8; i++) AddPiece(whitePawn, white, i, 1);

        AddPiece(blackRook, black, 0, 7); AddPiece(blackKnight, black, 1, 7);
        AddPiece(blackBishop, black, 2, 7); AddPiece(blackQueen, black, 3, 7);
        AddPiece(blackKing, black, 4, 7); AddPiece(blackBishop, black, 5, 7);
        AddPiece(blackKnight, black, 6, 7); AddPiece(blackRook, black, 7, 7);
        for (int i = 0; i < 8; i++) AddPiece(blackPawn, black, i, 6);
    }

    public void AddPiece(GameObject prefab, Player player, int col, int row)
    {
        var go = board.AddPiece(prefab, col, row);
        player.pieces.Add(go);
        pieces[col, row] = go;
    }

    //  PIECE QUERIES
    public void SelectPiece(GameObject p) => board.SelectPiece(p);
    public void DeselectPiece(GameObject p) => board.DeselectPiece(p);

    public GameObject PieceAtGrid(Vector2Int g)
    {
        if (g.x < 0 || g.x > 7 || g.y < 0 || g.y > 7) return null;
        return pieces[g.x, g.y];
    }

    public Vector2Int GridForPiece(GameObject piece)
    {
        for (int i = 0; i < 8; i++)
            for (int j = 0; j < 8; j++)
                if (pieces[i, j] == piece) return new Vector2Int(i, j);
        return new Vector2Int(-1, -1);
    }

    public bool FriendlyPieceAt(Vector2Int g)
    {
        var p = PieceAtGrid(g);
        return p != null && currentPlayer.pieces.Contains(p);
    }

    public bool EnemyPieceAt(Vector2Int g)
    {
        var p = PieceAtGrid(g);
        return p != null && otherPlayer.pieces.Contains(p);
    }

    public int GetForwardForPiece(GameObject p) =>
        white.pieces.Contains(p) ? white.forward : black.forward;


    // ownership check
    public Player GetOwnerOfPiece(GameObject p)
    {
        if (white.pieces.Contains(p)) return white;
        if (black.pieces.Contains(p)) return black;
        return null;
    }

    public bool DoesPieceBelongToCurrentPlayer(GameObject p) =>
        currentPlayer.pieces.Contains(p);

    //  MOVE & CAPTURE
    public void Move(GameObject piece, Vector2Int dest)
    {
        if (gameOver) return;
        ClearHintArrow();

        // Capture anything on destination
        var target = PieceAtGrid(dest);
        if (target != null) CapturePiece(target);
        if (gameOver) return;  // king was captured

        // Track castling rights
        var pc = piece.GetComponent<Piece>();
        var grid = GridForPiece(piece);
        if (pc != null)
        {
            if (pc.type == PieceType.King)
            { if (white.pieces.Contains(piece)) wKingMoved = true; else bKingMoved = true; }
            if (pc.type == PieceType.Rook)
            {
                if (grid == new Vector2Int(0, 0)) wRookAMoved = true;
                if (grid == new Vector2Int(7, 0)) wRookHMoved = true;
                if (grid == new Vector2Int(0, 7)) bRookAMoved = true;
                if (grid == new Vector2Int(7, 7)) bRookHMoved = true;
            }
        }

        pieces[grid.x, grid.y] = null;
        pieces[dest.x, dest.y] = piece;
        board.MovePiece(piece, dest);
    }

    public void CapturePiece(GameObject captured)
    {
        if (captured == null || gameOver) return;

        var pc = captured.GetComponent<Piece>();

        //  long checked/cant move = GAME OVER 
        if (pc != null && pc.type == PieceType.King)
        {
            var loser = GetOwnerOfPiece(captured);
            var winner = loser == white ? black : white;
            board.RemovePiece(captured);
            // Remove from pieces array
            var g = GridForPiece(captured);
            if (g.x >= 0) pieces[g.x, g.y] = null;
            TriggerGameOver(Capitalize(winner.name) + " wins!");
            return;
        }

        var owner = GetOwnerOfPiece(captured);
        if (owner == null) return;
        var captor = owner == white ? black : white;
        owner.pieces.Remove(captured);
        captor.capturedPieces.Add(captured);
        var gp = GridForPiece(captured);
        if (gp.x >= 0) pieces[gp.x, gp.y] = null;
        board.RemovePiece(captured);
    }

    //  LEGAL MOVE FILTERING
    public List<Vector2Int> GetLegalMovesForPiece(GameObject piece)
    {
        if (gameOver) return new List<Vector2Int>();
        var pc = piece.GetComponent<Piece>();
        var grid = GridForPiece(piece);
        var legal = new List<Vector2Int>();

        foreach (var dest in pc.MoveLocations(grid))
        {
            if (FriendlyPieceAt(dest)) continue;
            if (MoveLeavesKingInCheck(piece, dest)) continue;
            legal.Add(dest);
        }
        return legal;
    }

    private bool MoveLeavesKingInCheck(GameObject movingPiece, Vector2Int dest)
    {
        var from = GridForPiece(movingPiece);
        var victim = PieceAtGrid(dest);

        // Simulate
        pieces[from.x, from.y] = null;
        pieces[dest.x, dest.y] = movingPiece;
        var vo = victim != null ? GetOwnerOfPiece(victim) : null;
        if (vo != null) vo.pieces.Remove(victim);

        bool inCheck = IsKingInCheck(GetOwnerOfPiece(movingPiece));

        // Undo
        pieces[from.x, from.y] = movingPiece;
        pieces[dest.x, dest.y] = victim;
        if (vo != null && !vo.pieces.Contains(victim)) vo.pieces.Add(victim);

        return inCheck;
    }

    public bool IsKingInCheck(Player player)
    {
        if (player == null) return false;
        GameObject king = null;
        foreach (var p in player.pieces)
        {
            var pc = p.GetComponent<Piece>();
            if (pc != null && pc.type == PieceType.King) { king = p; break; }
        }
        if (king == null) return false;
        var kpos = GridForPiece(king);
        var enemy = player == white ? black : white;
        foreach (var ep in new List<GameObject>(enemy.pieces))
        {
            var epc = ep.GetComponent<Piece>();
            if (epc == null) continue;
            if (epc.MoveLocations(GridForPiece(ep)).Contains(kpos)) return true;
        }
        return false;
    }

    private bool CurrentPlayerHasNoMoves()
    {
        foreach (var p in new List<GameObject>(currentPlayer.pieces))
            if (GetLegalMovesForPiece(p).Count > 0) return false;
        return true;
    }

    //  TURN MANAGEMENT
    public void NextPlayer()
    {
        if (gameOver) return;

        // Flip turn
        var tmp = currentPlayer;
        currentPlayer = otherPlayer;
        otherPlayer = tmp;

        // Check for game-ending conditions 
        if (CurrentPlayerHasNoMoves())
        {
            if (IsKingInCheck(currentPlayer))
                TriggerGameOver(Capitalize(otherPlayer.name) + " wins by checkmate!");
            else
                TriggerGameOver("Draw by stalemate!");
            return;
        }

        if (IsKingInCheck(currentPlayer))
            Debug.Log($"[GM] {currentPlayer.name} is in CHECK!");

        // Hint mode: show arrow for the new side to move 
        if (currentMode == AIMode.StockfishHint)
        {
            RequestHint();
            return;   // human plays both sides — no auto-move
        }

        // AI turn for Black
        if (currentPlayer == black &&
            (currentMode == AIMode.OurAI || currentMode == AIMode.StockfishAuto))
            aiPending = true;
    }

    void Update()
    {
        if (aiPending && !gameOver && !aiRunning)
        {
            aiPending = false;
            StartCoroutine(HandleAITurn());
        }
    }

    private IEnumerator HandleAITurn()
    {
        aiRunning = true;
        yield return new WaitForSeconds(0.3f);
        if (gameOver) { aiRunning = false; yield break; }

        if (currentMode == AIMode.StockfishAuto && stockfishSvc != null)
            yield return stockfishSvc.AutoPlay();
        else
        {
            ourAI?.TakeTurn();
            NextPlayer();
        }

        aiRunning = false;
    }

    // Stockfish hint request
    public void RequestHint()
    {
        if (stockfishSvc != null)
            StartCoroutine(stockfishSvc.ShowHint());
        else
        {
            // Fallback: use local AI's suggestion
            var m = ourAI?.GetBestMoveForHint();
            if (m != null)
                ShowHintArrow(new Vector2Int(m[0], m[1]), new Vector2Int(m[2], m[3]));
        }
    }

    //  FEN
    public string GetFEN()
    {
        string fen = "";
        for (int row = 7; row >= 0; row--)
        {
            int empty = 0;
            for (int col = 0; col < 8; col++)
            {
                var go = pieces[col, row];
                if (go == null) { empty++; continue; }
                if (empty > 0) { fen += empty; empty = 0; }
                var pc = go.GetComponent<Piece>();
                bool isW = white.pieces.Contains(go);
                char ch = pc.type switch
                {
                    PieceType.Pawn => 'p',
                    PieceType.Knight => 'n',
                    PieceType.Bishop => 'b',
                    PieceType.Rook => 'r',
                    PieceType.Queen => 'q',
                    PieceType.King => 'k',
                    _ => '?'
                };
                fen += isW ? char.ToUpper(ch) : ch;
            }
            if (empty > 0) fen += empty;
            if (row > 0) fen += '/';
        }
        fen += currentPlayer == white ? " w " : " b ";
        string cast = "";
        if (!wKingMoved && !wRookHMoved) cast += "K";
        if (!wKingMoved && !wRookAMoved) cast += "Q";
        if (!bKingMoved && !bRookHMoved) cast += "k";
        if (!bKingMoved && !bRookAMoved) cast += "q";
        fen += (cast.Length > 0 ? cast : "-") + " - 0 1";
        return fen;
    }

    //  HINT ARROW
    public void ShowHintArrow(Vector2Int from, Vector2Int to)
    {
        ClearHintArrow();
        Vector3 s = Geometry.PointFromGrid(from) + Vector3.up * 0.4f;
        Vector3 e = Geometry.PointFromGrid(to) + Vector3.up * 0.4f;

        hintArrow = new GameObject("HintArrow");

        var shaft = new GameObject("Shaft");
        shaft.transform.parent = hintArrow.transform;
        var lr = shaft.AddComponent<LineRenderer>();
        lr.positionCount = 2;
        lr.SetPosition(0, s); lr.SetPosition(1, e);
        lr.startWidth = lr.endWidth = 0.15f;
        lr.material = new Material(Shader.Find("Sprites/Default"));
        lr.startColor = lr.endColor = new Color(0.15f, 0.80f, 0.15f, 0.92f);
        lr.sortingOrder = 10;

        var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.transform.parent = hintArrow.transform;
        head.transform.position = e;
        head.transform.localScale = new Vector3(0.35f, 0.08f, 0.35f);
        head.GetComponent<Renderer>().material.color = new Color(0.15f, 0.80f, 0.15f, 0.92f);
        Destroy(head.GetComponent<Collider>());
    }

    public void ClearHintArrow()
    {
        if (hintArrow != null) { Destroy(hintArrow); hintArrow = null; }
    }

    //  GAME OVER
    private void TriggerGameOver(string msg)
    {
        if (gameOver) return;
        gameOver = true;   // ← this single flag stops ALL clicks
        aiPending = false;
        aiRunning = false;
        StopAllCoroutines();
        ClearHintArrow();
        ShowGameOverScreen(msg);
        Debug.Log("[GM] GAME OVER: " + msg);
    }

    // Game-over UI (runtime, no prefabs)
    private void BuildGameOverUI()
    {
        goCanvas = new GameObject("GOCanvas");
        var cv = goCanvas.AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.sortingOrder = 999;
        goCanvas.AddComponent<GraphicRaycaster>();
        var sc = goCanvas.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1920, 1080);

        var panel = MakePanel(goCanvas, new Vector2(680, 220), Vector2.zero,
                              new Color(0.05f, 0.05f, 0.05f, 0.95f));

        goText = MakeText(panel, "", 50, Color.white, new Vector2(0, 34), new Vector2(660, 100));
        MakeText(panel, "press Play Again to restart", 22,
                 new Color(0.6f, 0.6f, 0.6f), new Vector2(0, -24), new Vector2(660, 36));

        var btn = MakeButton(panel, "Play Again", new Vector2(0, -84), new Vector2(220, 54));
        btn.onClick.AddListener(() =>
            SceneManager.LoadScene(SceneManager.GetActiveScene().name));

        goCanvas.SetActive(false);
    }

    private void ShowGameOverScreen(string msg)
    {
        goCanvas.SetActive(true);
        if (goText) goText.text = msg;
    }

    // UI helpers
    private Font F() =>
        Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf") ??
        Resources.GetBuiltinResource<Font>("Arial.ttf");

    private GameObject MakePanel(GameObject parent, Vector2 sz, Vector2 pos, Color col)
    {
        var go = new GameObject("P"); go.transform.SetParent(parent.transform, false);
        go.AddComponent<Image>().color = col;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = sz; rt.anchoredPosition = pos;
        return go;
    }

    private Text MakeText(GameObject parent, string txt, int sz, Color col, Vector2 pos, Vector2 size)
    {
        var go = new GameObject("T"); go.transform.SetParent(parent.transform, false);
        var t = go.AddComponent<Text>();
        t.text = txt; t.font = F(); t.fontSize = sz; t.color = col;
        t.alignment = TextAnchor.MiddleCenter;
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = size;
        return t;
    }

    private Button MakeButton(GameObject parent, string lbl, Vector2 pos, Vector2 sz)
    {
        var go = new GameObject("Btn"); go.transform.SetParent(parent.transform, false);
        go.AddComponent<Image>().color = new Color(0.18f, 0.48f, 0.18f);
        var btn = go.AddComponent<Button>();
        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = pos; rt.sizeDelta = sz;
        var lg = new GameObject("L"); lg.transform.SetParent(go.transform, false);
        var lt = lg.AddComponent<Text>();
        lt.text = lbl; lt.font = F(); lt.fontSize = 26; lt.color = Color.white;
        lt.alignment = TextAnchor.MiddleCenter;
        var lrt = lg.GetComponent<RectTransform>();
        lrt.anchorMin = Vector2.zero; lrt.anchorMax = Vector2.one; lrt.sizeDelta = Vector2.zero;
        return btn;
    }

    private string Capitalize(string s) =>
        s.Length > 0 ? char.ToUpper(s[0]) + s.Substring(1) : s;
}
