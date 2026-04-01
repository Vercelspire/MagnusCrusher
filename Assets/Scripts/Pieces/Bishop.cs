/*
 * Copyright (c) 2018 Razeware LLC
 * (MoveLocations implemented)
 */

using System.Collections.Generic;
using UnityEngine;

public class Bishop : Piece
{
    public override List<Vector2Int> MoveLocations(Vector2Int gridPoint)
    {
        List<Vector2Int> locations = new List<Vector2Int>();

        // BishopDirections is defined in Piece.cs: the 4 diagonals
        foreach (Vector2Int dir in BishopDirections)
        {
            // Slide along this diagonal until we hit a wall or a piece
            for (int step = 1; step < 8; step++)
            {
                Vector2Int dest = new Vector2Int(
                    gridPoint.x + dir.x * step,
                    gridPoint.y + dir.y * step);

                if (dest.x < 0 || dest.x > 7 || dest.y < 0 || dest.y > 7) break;

                if (GameManager.instance.FriendlyPieceAt(dest)) break;  // blocked by own piece

                locations.Add(dest);   // empty square or enemy capture

                if (GameManager.instance.EnemyPieceAt(dest)) break;     // stop after capture
            }
        }

        return locations;
    }
}
