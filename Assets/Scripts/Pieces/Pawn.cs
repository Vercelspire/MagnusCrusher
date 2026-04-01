/*
 * Copyright (c) 2018 Razeware LLC
 * (MoveLocations implemented)
 */

using System.Collections.Generic;
using UnityEngine;

public class Pawn : Piece
{
    public override List<Vector2Int> MoveLocations(Vector2Int gridPoint)
    {
        List<Vector2Int> locations = new List<Vector2Int>();

        // Determine direction: white moves toward row 7 (+1), black toward row 0 (-1)
        int forward = GameManager.instance.GetForwardForPiece(gameObject);

        // Single step forward
        Vector2Int oneStep = new Vector2Int(gridPoint.x, gridPoint.y + forward);
        if (IsInBounds(oneStep) && GameManager.instance.PieceAtGrid(oneStep) == null)
        {
            locations.Add(oneStep);

            // Double push from starting rank 
            // White starts on row 1, Black on row 6
            int startRow;
            if (forward == 1)
            {
                startRow = 1;
            }
            else
            {
                startRow = 6;
            }

            if (gridPoint.y == startRow)
            {
                Vector2Int twoStep = new Vector2Int(gridPoint.x, gridPoint.y + 2 * forward);
                if (GameManager.instance.PieceAtGrid(twoStep) == null)
                    locations.Add(twoStep);
            }
        }

        // Diagonal captures 
        // Pawns only capture diagonally — they cannot move diagonally to an empty square
        int[] sideCols = { gridPoint.x - 1, gridPoint.x + 1 };
        foreach (int col in sideCols)
        {
            Vector2Int diag = new Vector2Int(col, gridPoint.y + forward);
            if (IsInBounds(diag) && GameManager.instance.EnemyPieceAt(diag))
                locations.Add(diag);
        }

        return locations;
    }

    private bool IsInBounds(Vector2Int p)
    {
        return p.x >= 0 && p.x < 8 && p.y >= 0 && p.y < 8;
    }
}
