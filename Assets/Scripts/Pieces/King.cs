/*
 * Copyright (c) 2018 Razeware LLC
 * (MoveLocations implemented)
 */

using System.Collections.Generic;
using UnityEngine;

public class King : Piece
{
    public override List<Vector2Int> MoveLocations(Vector2Int gridPoint)
    {
        List<Vector2Int> locations = new List<Vector2Int>();

        // King moves exactly one square in any of the 8 directions
        // RookDirections + BishopDirections from Piece.cs cover all 8
        List<Vector2Int> allDirections = new List<Vector2Int>();
        allDirections.AddRange(RookDirections);
        allDirections.AddRange(BishopDirections);

        foreach (Vector2Int dir in allDirections)
        {
            Vector2Int dest = new Vector2Int(gridPoint.x + dir.x, gridPoint.y + dir.y);

            if (dest.x < 0 || dest.x > 7 || dest.y < 0 || dest.y > 7) continue;
            if (GameManager.instance.FriendlyPieceAt(dest)) continue;

            locations.Add(dest);
        }

        return locations;
    }
}
