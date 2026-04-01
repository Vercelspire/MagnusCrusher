/*
 * Copyright (c) 2018 Razeware LLC
 * (MoveLocations implemented)
 */

using System.Collections.Generic;
using UnityEngine;

public class Queen : Piece
{
    public override List<Vector2Int> MoveLocations(Vector2Int gridPoint)
    {
        List<Vector2Int> locations = new List<Vector2Int>();

        // Queen = Rook + Bishop: slide in all 8 directions
        // RookDirections (4 straight) + BishopDirections (4 diagonals) defined in Piece.cs
        List<Vector2Int> allDirections = new List<Vector2Int>();
        allDirections.AddRange(RookDirections);
        allDirections.AddRange(BishopDirections);

        foreach (Vector2Int dir in allDirections)
        {
            for (int step = 1; step < 8; step++)
            {
                Vector2Int dest = new Vector2Int(
                    gridPoint.x + dir.x * step,
                    gridPoint.y + dir.y * step);

                if (dest.x < 0 || dest.x > 7 || dest.y < 0 || dest.y > 7) break;

                if (GameManager.instance.FriendlyPieceAt(dest)) break;

                locations.Add(dest);

                if (GameManager.instance.EnemyPieceAt(dest)) break;
            }
        }

        return locations;
    }
}
