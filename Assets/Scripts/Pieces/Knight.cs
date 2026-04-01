/*
 * Copyright (c) 2018 Razeware LLC
 * (MoveLocations implemented)
 */

using System.Collections.Generic;
using UnityEngine;

public class Knight : Piece
{
    public override List<Vector2Int> MoveLocations(Vector2Int gridPoint)
    {
        List<Vector2Int> locations = new List<Vector2Int>();

        // All 8 possible L-shaped jumps: (±1, ±2) and (±2, ±1)
        Vector2Int[] jumps = {
            new Vector2Int( 1,  2), new Vector2Int( 1, -2),
            new Vector2Int(-1,  2), new Vector2Int(-1, -2),
            new Vector2Int( 2,  1), new Vector2Int( 2, -1),
            new Vector2Int(-2,  1), new Vector2Int(-2, -1)
        };

        foreach (Vector2Int jump in jumps)
        {
            Vector2Int dest = gridPoint + jump;

            // Must be on the board and not occupied by a friendly piece
            if (dest.x < 0 || dest.x > 7 || dest.y < 0 || dest.y > 7) continue;
            if (GameManager.instance.FriendlyPieceAt(dest)) continue;

            locations.Add(dest);
        }

        return locations;
    }
}
