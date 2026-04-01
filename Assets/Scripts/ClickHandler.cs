/*
 * ClickHandler.cs
 *
 */

using System.Collections.Generic;
using UnityEngine;

public class ClickHandler : MonoBehaviour
{
    public GameObject highlightPrefab;

    private GameObject selectedPiece;
    private List<Vector2Int> legalMoves = new List<Vector2Int>();
    private List<GameObject> highlights = new List<GameObject>();
    private Material hlMat;
    private EvalBar evalBar;

    void Start()
    {
        hlMat = new Material(Shader.Find("Standard"));
        hlMat.color = new Color(0.18f, 0.85f, 0.18f, 0.75f);
        evalBar = FindObjectOfType<EvalBar>();
    }

    void Update()
    {
        var gm = GameManager.instance;
        if (gm == null) return;

        // ── HARD STOP: no input once game is over ─────────────
        if (gm.gameOver) return;

        // Determine if human should be able to click right now
        bool humanTurn;
        if (gm.currentMode == AIMode.StockfishHint)
            humanTurn = true;   // both sides are human in hint mode
        else
            humanTurn = gm.currentPlayer == gm.white;

        if (!humanTurn) return;
        if (Input.GetMouseButtonDown(0)) HandleClick();
    }

    private void HandleClick()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (!new UnityEngine.Plane(Vector3.up, Vector3.zero).Raycast(ray, out float dist)) return;

        Vector2Int grid = Geometry.GridFromPoint(ray.GetPoint(dist));
        if (grid.x < 0 || grid.x > 7 || grid.y < 0 || grid.y > 7) { Deselect(); return; }

        var gm = GameManager.instance;

        if (selectedPiece != null)
        {
            // Try to execute move
            if (legalMoves.Contains(grid)) { ExecuteMove(grid); return; }

            // Try to switch selection to another friendly piece
            var clicked = gm.PieceAtGrid(grid);
            if (clicked != null && gm.currentPlayer.pieces.Contains(clicked))
            { Deselect(); TrySelect(clicked, grid); return; }

            Deselect();
            return;
        }

        // Try to select a friendly piece
        var piece = gm.PieceAtGrid(grid);
        if (piece != null && gm.currentPlayer.pieces.Contains(piece))
            TrySelect(piece, grid);
    }

    private void TrySelect(GameObject piece, Vector2Int grid)
    {
        legalMoves = GameManager.instance.GetLegalMovesForPiece(piece);
        if (legalMoves.Count == 0) return;
        selectedPiece = piece;
        GameManager.instance.SelectPiece(piece);
        foreach (var d in legalMoves) SpawnDot(d);
    }

    private void Deselect()
    {
        if (selectedPiece != null) GameManager.instance.DeselectPiece(selectedPiece);
        selectedPiece = null;
        legalMoves.Clear();
        ClearDots();
    }

    private void ExecuteMove(Vector2Int dest)
    {
        var gm = GameManager.instance;
        var piece = selectedPiece;
        var fromGrid = gm.GridForPiece(piece);
        var pc = piece.GetComponent<Piece>();
        Deselect();

        gm.Move(piece, dest);
        if (gm.gameOver) return;   // king was captured mid-move

        evalBar?.Refresh();

        string note = BuildNote(pc, fromGrid, dest);
        bool inCheck = gm.IsKingInCheck(gm.currentPlayer == gm.white ? gm.black : gm.white);
        if (inCheck) note += "+";
        ChessUI.instance?.OnMoveMade(note, gm.currentPlayer == gm.black, inCheck);

        // NextPlayer handles: turn flip, checkmate check, hint request, AI trigger
        gm.NextPlayer();
    }

    private string BuildNote(Piece pc, Vector2Int from, Vector2Int to)
    {
        string prefix = pc.type switch
        {
            PieceType.King => "K",
            PieceType.Queen => "Q",
            PieceType.Rook => "R",
            PieceType.Bishop => "B",
            PieceType.Knight => "N",
            _ => ""
        };
        return prefix + (char)('a' + to.x) + (char)('1' + to.y);
    }

    private void SpawnDot(Vector2Int grid)
    {
        Vector3 pos = Geometry.PointFromGrid(grid); pos.y = 0.05f;
        GameObject dot;
        if (highlightPrefab != null)
            dot = Instantiate(highlightPrefab, pos, Quaternion.identity);
        else
        {
            dot = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            dot.transform.position = pos;
            dot.transform.localScale = new Vector3(0.45f, 0.02f, 0.45f);
            dot.GetComponent<Renderer>().material = hlMat;
            Destroy(dot.GetComponent<Collider>());
        }
        dot.name = "Highlight";
        highlights.Add(dot);
    }

    private void ClearDots()
    {
        foreach (var h in highlights) if (h) Destroy(h);
        highlights.Clear();
    }
}
