/*
 * ChessAI
 *
 *
 *   1. We take a picture of the board as a grid of numbers by using BuildVB function.
 *   2. think ahead 1 move, then 2 moves, then 3... etc up to maxDepth.
 *   3. skip looking at really bad moves to save time (Alpha-Beta).
 *   4. score the board based on what pieces are left and where they sit.
 *   5. play the best move we found before time runs out.
 *
 * HOW WE STORE PIECES:
 *   We use numbers. Positive = White, Negative = Black.
 *   1/-1 = Pawn,   2/-2 = Knight,  3/-3 = Bishop,
 *   4/-4 = Rook,   5/-5 = Queen,   6/-6 = King,   0 = Empty square
 */

using System;
using UnityEngine;

public class ChessAI_Advanced : MonoBehaviour
{
    //  SETTINGS

    [Header("Search Settings")]
    [Range(2, 7)]
    public int maxDepth = 5;       // How many moves ahead the AI thinks (higher = smarter but slower)

    public int timeLimitMs = 3000; // Stop thinking after this many milliseconds (3000 = 3 seconds)


    //  PIECE NUMBER CODES
    //  These are the numbers we use inside our grid to represent pieces.

    private const int WP = 1;    // White Pawn
    private const int WN = 2;    // White Knight
    private const int WB = 3;    // White Bishop
    private const int WR = 4;    // White Rook
    private const int WQ = 5;    // White Queen
    private const int WK = 6;    // White King
    private const int BP = -1;   // Black Pawn
    private const int BN = -2;   // Black Knight
    private const int BB = -3;   // Black Bishop
    private const int BR = -4;   // Black Rook
    private const int BQ = -5;   // Black Queen
    private const int BK = -6;   // Black King
    private const int MT = 0;    // Empty square (MT = "empty")


    //  SEARCH NUMBERS WE USE INTERNALLY

    private const int INF = 9_999_999;  // A number so big it means "the best possible" or "the worst possible"
    private const int MATE = 8_000_000;  // The score we give when a king gets checkmated
    private const int PLY_MAX = 48;       // The deepest we'll ever look ahead (safety limit)
    private const int MAX_MOVES = 128;      // The most moves we'll store for one board position


    //  PIECE VALUES (how many "points" each piece is worth)
    //  A Pawn = 100, Knight = 320, Bishop = 330,
    //  Rook = 500, Queen = 900, King = 20000 (basically priceless)
    //  Index matches the piece code: 1=Pawn, 2=Knight, etc.

    private static readonly int[] MAT = { 0, 100, 320, 330, 500, 900, 20000 };


    //  POSITION BONUS TABLES
    //
    //  These tables give a small bonus or penalty based on WHERE
    //  a piece stands on the board. For example, a knight in the
    //  center gets a bonus, but a knight stuck on the edge gets a penalty.
    //
    //  Each table has 64 numbers — one for each square on the board.
    //  Square 0 = bottom-left corner (a1), Square 63 = top-right corner (h8).


    // we give msot points to respected areas

    // Pawn bonuses: reward pawns that have moved forward
    private static readonly int[] PST_P = {
          0,  0,  0,  0,  0,  0,  0,  0,
         98,134, 61, 95, 68,126, 34,-11,
         -6,  7, 26, 31, 65, 56, 25,-20,
        -14, 13,  6, 21, 23, 12, 17,-23,
        -27, -2, -5, 12, 17,  6, 10,-25,
        -26, -4, -4,-10,  3,  3, 33,-12,
        -35, -1,-20,-23,-15, 24, 38,-22,
          0,  0,  0,  0,  0,  0,  0,  0 };

    // Knight bonuses: reward knights in the middle, penalize them on the edge
    private static readonly int[] PST_N = {
        -50,-40,-30,-30,-30,-30,-40,-50,
        -40,-20,  0,  0,  0,  0,-20,-40,
        -30,  0, 10, 15, 15, 10,  0,-30,
        -30,  5, 15, 20, 20, 15,  5,-30,
        -30,  0, 15, 20, 20, 15,  0,-30,
        -30,  5, 10, 15, 15, 10,  5,-30,
        -40,-20,  0,  5,  5,  0,-20,-40,
        -50,-40,-30,-30,-30,-30,-40,-50 };

    // Bishop bonuses: reward bishops on long diagonals
    private static readonly int[] PST_B = {
        -20,-10,-10,-10,-10,-10,-10,-20,
        -10,  0,  0,  0,  0,  0,  0,-10,
        -10,  0,  5, 10, 10,  5,  0,-10,
        -10,  5,  5, 10, 10,  5,  5,-10,
        -10,  0, 10, 10, 10, 10,  0,-10,
        -10, 10, 10, 10, 10, 10, 10,-10,
        -10,  5,  0,  0,  0,  0,  5,-10,
        -20,-10,-10,-10,-10,-10,-10,-20 };

    // Rook bonuses: reward rooks on open columns and near the opponent's back row
    private static readonly int[] PST_R = {
          0,  0,  0,  0,  0,  0,  0,  0,
          5, 10, 10, 10, 10, 10, 10,  5,
         -5,  0,  0,  0,  0,  0,  0, -5,
         -5,  0,  0,  0,  0,  0,  0, -5,
         -5,  0,  0,  0,  0,  0,  0, -5,
         -5,  0,  0,  0,  0,  0,  0, -5,
         -5,  0,  0,  0,  0,  0,  0, -5,
          0,  0,  0,  5,  5,  0,  0,  0 };

    // Queen bonuses: slight penalty for bringing the queen out too early
    private static readonly int[] PST_Q = {
        -20,-10,-10, -5, -5,-10,-10,-20,
        -10,  0,  0,  0,  0,  0,  0,-10,
        -10,  0,  5,  5,  5,  5,  0,-10,
         -5,  0,  5,  5,  5,  5,  0, -5,
          0,  0,  5,  5,  5,  5,  0, -5,
        -10,  5,  5,  5,  5,  5,  0,-10,
        -10,  0,  5,  0,  0,  0,  0,-10,
        -20,-10,-10, -5, -5,-10,-10,-20 };

    // King bonuses (middlegame): the king should stay safely tucked away behind pawns
    private static readonly int[] PST_KMG = {
        -30,-40,-40,-50,-50,-40,-40,-30,
        -30,-40,-40,-50,-50,-40,-40,-30,
        -30,-40,-40,-50,-50,-40,-40,-30,
        -30,-40,-40,-50,-50,-40,-40,-30,
        -20,-30,-30,-40,-40,-30,-30,-20,
        -10,-20,-20,-20,-20,-20,-20,-10,
         20, 20,  0,  0,  0,  0, 20, 20,
         20, 30, 10,  0,  0, 10, 30, 20 };

    // King bonuses (endgame): once most pieces are gone, the king should move toward the center
    private static readonly int[] PST_KEG = {
        -50,-40,-30,-20,-20,-30,-40,-50,
        -30,-20,-10,  0,  0,-10,-20,-30,
        -30,-10, 20, 30, 30, 20,-10,-30,
        -30,-10, 30, 40, 40, 30,-10,-30,
        -30,-10, 30, 40, 40, 30,-10,-30,
        -30,-10, 20, 30, 30, 20,-10,-30,
        -30,-30,  0,  0,  0,  0,-30,-30,
        -50,-30,-30,-30,-30,-30,-30,-50 };


    //  MEMORY TABLE (called a Transposition Table)
    //
    //  Chess positions can repeat. Instead of thinking about the same
    //  board twice, we write our answer down in this table the first time.
    //  The next time we see the same board, we just look it up!
    //
    //  Like a cheat sheet lol

    private const int TT_SZ = 1 << 16;    // The table holds 65,536 entries
    private const int TT_MASK = TT_SZ - 1;  // A trick to quickly pick the right slot

    // Labels for how reliable a score stored in the table is:
    private const byte TTE = 0;  // Exact  — this score is perfectly accurate
    private const byte TTL = 1;  // At least this good — the real score might be higher
    private const byte TTU = 2;  // At most this good  — the real score might be lower

    // One slot in our memory table — holds everything we learned about one position
    private struct TT
    {
        public ulong h;   // A unique ID number for the board position (like a fingerprint)
        public int s;   // The score we calculated for this position
        public int d;   // How many moves ahead we looked when we calculated it
        public byte f;   // How reliable the score is (TTE, TTL, or TTU)
        public byte fc;  // The "from column" of the best move we found
        public byte fr;  // The "from row" of the best move we found
        public byte tc;  // The "to column" of the best move we found
        public byte tr;  // The "to row" of the best move we found
    }

    private TT[] tt;  // The actual memory table, created when the game starts


    //  BOARD FINGERPRINTING, as we used (Zobrist Hashing)
    //
    //  answers -> "have I seen this board before?"
    //  Do this by giving every piece on every square a random big number.
    //  Then we combine (XOR) all those numbers together.
    //  The result is a unique "fingerprint" for any board position.
    //  When a piece moves, we can update the fingerprint super fast.

    private ulong[,] Z;   // Random numbers: one for each (piece type, square) combo
    private ulong Zb;  // An extra random number we mix in when it's Black's turn


    //  MOVE STORAGE (Move Buffer)
    //
    //  While thinking ahead -> generate lots of possible moves.
    //  We store them in a big pre-made grid so we don't have to create
    //  new memory over and over (which would slow things down).
    //
    //  MB[depth][moveNumber][0..3] stores: from-column, from-row, to-column, to-row
    //  MC[depth] = how many moves we found at that depth level

    private int[,,] MB;  // The move storage grid
    private int[] MC;  // How many moves are stored at each depth level


    //
    //  If we look at good moves first, we can skip a lot of bad ones faster.
    //  We use two tricks to remember which moves tend to be good:
    //
    //  Killer Moves: quiet moves that were surprisingly good at this depth recently.
    //  History: counts how often each move turned out to be great overall.

    private int[,] K1;  // The first "killer move" slot for each depth
    private int[,] K2;  // The second "killer move" slot for each depth
    private int[,,] H;   // The history score for each possible move


    //  PAWN COUNTING HELPERS
    //  We reuse these arrays every time we score the board,
    //  instead of creating new ones each time (saves memory).

    private int[] ewp;  // How many white pawns are on each column (a through h)
    private int[] ebp;  // How many black pawns are on each column


    // Shortcut names for the fingerprint variables (same objects, just shorter names)
    private ulong[,] zt;
    private ulong zb;

    //  COUNTERS (just for showing debug info)

    private int nodes;   // How many board positions we looked at this search
    private int qnodes;  // How many extra "keep capturing" positions we looked at

    //  SETUP — runs once when the game starts
    void Start()
    {
        // Create the memory table (our "cheat sheet")
        tt = new TT[TT_SZ];

        // Create the fingerprint number table: 12 piece types × 64 squares
        Z = new ulong[12, 64];

        // Create the move storage grid big enough for every depth and move
        MB = new int[PLY_MAX, MAX_MOVES, 4];

        // Create the move-count tracker (one slot per depth level)
        MC = new int[PLY_MAX];

        // Create killer move storage (2 slots per depth, 4 numbers each = from/to coordinates)
        K1 = new int[PLY_MAX, 4];
        K2 = new int[PLY_MAX, 4];

        // Create history table (2 sides × 64 from-squares × 64 to-squares)
        H = new int[2, 64, 64];

        // Create pawn counter arrays (8 columns on the board)
        ewp = new int[8];
        ebp = new int[8];

        // Point the shortcut names at the same objects
        zt = Z;
        zb = 0;

        // Fill the fingerprint table with random big numbers.
        // We always use the same starting seed (54321) so the numbers
        // are the same every single time the game runs.
        var rng = new System.Random(54321);
        byte[] buf = new byte[8];

        for (int p = 0; p < 12; p++)         // For each of the 12 piece types
        {
            for (int s = 0; s < 64; s++)      // For each of the 64 squares
            {
                rng.NextBytes(buf);                            // Pick 8 random bytes
                Z[p, s] = BitConverter.ToUInt64(buf, 0);      // Store them as one big number
            }
        }

        // Make a random number to mix in when it's Black's turn to move
        rng.NextBytes(buf);
        Zb = BitConverter.ToUInt64(buf, 0);

        // Start with Black's turn mixed in (matches the default board setup)
        zb = Zb;

        Debug.Log("[AI] Ready.");
    }

    // The GameManager calls this to tell the AI to take its turn.
    public void TakeTurn()
    {
        // Ask the AI to pick the best move
        int[] bestMove = GetBestMoveForHint();

        // If the AI found no moves (checkmate or stalemate), do nothing
        if (bestMove == null)
        {
            return;
        }

        var gm = GameManager.instance;

        // Find the game piece sitting at the "from" square
        var pieceObject = gm.PieceAtGrid(new Vector2Int(bestMove[0], bestMove[1]));

        // Tell the game to move it to the "to" square
        if (pieceObject != null)
        {
            gm.Move(pieceObject, new Vector2Int(bestMove[2], bestMove[3]));
        }
    }

    // Finds and returns the best move as [fromColumn, fromRow, toColumn, toRow].
    // Returns null if there are no legal moves.
    public int[] GetBestMoveForHint()
    {
        // Make sure Start() has already run
        if (tt == null)
        {
            return null;
        }

        // Reset our counters
        nodes = 0;
        qnodes = 0;

        // Wipe the killer move tables clean for this new search
        Array.Clear(K1, 0, K1.Length);
        Array.Clear(K2, 0, K2.Length);

        // Make old history scores smaller so they don't overshadow new information
        for (int c = 0; c < 2; c++)
        {
            for (int f = 0; f < 64; f++)
            {
                for (int t = 0; t < 64; t++)
                {
                    H[c, f, t] = H[c, f, t] / 4;
                }
            }
        }

        // Take a snapshot of the board as a number grid
        int[,] vb = BuildVB();

        // Make a fingerprint for the starting position
        ulong h = Hash(vb, true);

        // Keep track of the best move we've found so far
        int bFc = -1, bFr = -1, bTc = -1, bTr = -1;

        // Note what time it is so we can stop if we run out of time
        long startTime = Now();

        // THINK DEEPER AND DEEPER
        // First think 1 move ahead. Then 2. Then 3. And so on.
        // Each time we finish a depth, we update our "best move so far."
        // If time runs out, we use whatever best move we found last.
        for (int depth = 1; depth <= maxDepth; depth++)
        {
            // Find all the moves we can make right now
            MC[0] = 0;
            Gen(vb, true, 0, false);
            int moveCount = MC[0];

            // If there are no moves, it's checkmate or stalemate — stop
            if (moveCount == 0)
            {
                break;
            }

            // Sort moves so the most promising ones are tried first
            Score(vb, 0, moveCount, 0, true);
            Sort(0, moveCount);

            // If our memory table remembers a good move, try it first
            TTPrime(0, moveCount, h);

            // Start with the worst possible score on both sides
            int alpha = -INF;  // Our best guaranteed score (starts terrible for us)
            int beta = INF;  // Opponent's best guaranteed score (starts terrible for them)

            // Track the best move at this depth
            int cFc = -1, cFr = -1, cTc = -1, cTr = -1;
            int cScore = -INF;
            bool isFirstMove = true;

            // TRY EVERY POSSIBLE MOVE AT THIS DEPTH 
            for (int i = 0; i < moveCount; i++)
            {
                int fc = MB[0, i, 0];  // from column
                int fr = MB[0, i, 1];  // from row
                int tc = MB[0, i, 2];  // to column
                int tr = MB[0, i, 3];  // to row

                // Make the move on our number grid
                int captured = Do(vb, fc, fr, tc, tr);

                // Update the board fingerprint for the new position
                ulong newHash = UHash(h, vb, fc, fr, tc, tr, captured);

                int score;

                if (isFirstMove)
                {
                    // The first move always gets a full search
                    score = -AB(vb, depth - 1, -beta, -alpha, false, newHash, 1);
                    isFirstMove = false;
                }
                else
                {
                    // Later moves: do a quick narrow search first to save time
                    score = -AB(vb, depth - 1, -alpha - 1, -alpha, false, newHash, 1);

                    // If it looks surprisingly good, do a full search to be sure
                    if (score > alpha)
                    {
                        score = -AB(vb, depth - 1, -beta, -alpha, false, newHash, 1);
                    }
                }

                // Undo the move to restore the board
                Undo(vb, fc, fr, tc, tr, captured);

                // Is this the best move we've seen at this depth?
                if (score > cScore)
                {
                    cScore = score;
                    cFc = fc; cFr = fr; cTc = tc; cTr = tr;
                }

                if (score > alpha) alpha = score;

                // If this move is already better than what the opponent would allow,
                // stop looking — they'd never let us reach this position anyway
                if (alpha >= beta) break;
            }

            // Save this depth's best move as our overall best
            if (cFc >= 0)
            {
                bFc = cFc; bFr = cFr; bTc = cTc; bTr = cTr;
            }

            long elapsed = Now() - startTime;
            Debug.Log($"[AI d={depth}] score={cScore} nodes={nodes + qnodes} {elapsed}ms");

            // If we've used more than 2/3 of our time budget, stop early
            if (elapsed > timeLimitMs * 2 / 3)
            {
                break;
            }
        }

        if (bFc >= 0)
            return new int[] { bFc, bFr, bTc, bTr };
        else
            return null;
    }

    // Returns the current time in milliseconds (used to track how long we've been thinking).
    private long Now()
    {
        return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    }


    //  Alpha-Beta Search
    //
    //  it's where the AI actually thinks ahead. It works like this:
    //    "If I make this move, what's the best my opponent can do?"
    //    "And if they do that, what's the best I can do next?"
    //
    //  if a move gives us a score that's TOO good,
    //  the opponent would have blocked us before we could play it.
    //  So we stop searching that path and move on.
    //
    //  "alpha" = the best score WE are sure to get no matter what.
    //  "beta"  = the best score the OPPONENT is sure to get no matter what.
    //
    //  Parameters:
    //    vb    = the board (as a number grid)
    //    depth = how many more moves ahead to look
    //    alpha = best score we're guaranteed so far
    //    beta  = best score the opponent is guaranteed so far
    //    blk   = true if it's Black's turn
    //    h     = board fingerprint
    //    ply   = how deep from the start we currently are
    private int AB(int[,] vb, int depth, int alpha, int beta,
                   bool blk, ulong h, int ply)
    {
        nodes++;

        // If we've gone too deep, just score the board as it is right now
        if (ply >= PLY_MAX)
        {
            return Eval(vb, blk);
        }

        // Remember what alpha was at the start so we can label our memory entry correctly later
        int originalAlpha = alpha;

        // CHECK THE MEMORY TABLE FIRS
        // Have we seen this exact board before? If so, use what we already figured out.
        ref TT entry = ref tt[h & TT_MASK];

        int tfc = -1, tfr = 0, ttc2 = 0, ttr = 0;

        if (entry.h == h && entry.d >= depth)
        {
            int cachedScore = entry.s;

            if (entry.f == TTE)
            {
                // We know the exact answer — return it immediately!
                return cachedScore;
            }
            else if (entry.f == TTL && cachedScore >= beta)
            {
                // It's at least this good, and that already beats beta
                return cachedScore;
            }
            else if (entry.f == TTU && cachedScore <= alpha)
            {
                // It's at most this good, and that's not good enough to beat alpha
                return cachedScore;
            }

            // The table has a hint for a good move — save it to try it first
            tfc = entry.fc; tfr = entry.fr; ttc2 = entry.tc; ttr = entry.tr;
        }

        // STOP LOOKING WHEN DEPTH HITS ZERO 
        if (depth <= 0)
        {
            // Before we stop, keep looking at captures so we don't miss a piece getting taken
            return QS(vb, alpha, beta, blk, ply);
        }
        bool inCheck = Check(vb, blk);

        // "PASS A TURN" TRICK 
        // Idea: what if we just skip our turn? If the opponent STILL can't
        // beat beta even when we do nothing... this position is already so
        // good for us that we can stop searching deeper.
        //
        // We only try this when it's safe:
        //   - We're not in check (you can't skip when in check!)
        //   - We're not too close to the top (would give wrong answers)
        //   - The position doesn't look like a checkmate is close
        //   - We still have big pieces (otherwise skipping can be a trap)
        bool canDoNullMove = depth >= 3
                          && !inCheck
                          && ply > 1
                          && beta < MATE - PLY_MAX
                          && HasMat(vb, blk);

        if (canDoNullMove)
        {
            // Search 2 levels shallower after "passing our turn"
            int nullScore = -AB(vb, depth - 3, -beta, -beta + 1, !blk, h ^ zb, ply + 1);

            if (nullScore >= beta)
            {
                // Even doing nothing beats beta — this branch is great, stop here
                return beta;
            }
        }

        // SKIP MOVES THAT ARE OBVIOUSLY USELESS 
        // At the very last level of search: if the board score + a 200-point
        // safety cushion still can't reach alpha, skip non-capture moves.
        // They just can't help enough to be worth checking.
        bool useFutility = depth <= 1 && !inCheck;
        int futilityBase = 0;

        if (useFutility)
        {
            futilityBase = Eval(vb, blk);
        }

        MC[ply] = 0;
        Gen(vb, blk, ply, false);
        int n = MC[ply];

        // No moves at all?
        if (n == 0)
        {
            if (inCheck)
            {
                // In check with no escape = checkmate!
                // Finding checkmate in fewer moves scores higher
                return -(MATE - ply);
            }
            else
            {
                // Not in check but no moves = stalemate (a draw)
                return 0;
            }
        }

        Score(vb, ply, n, ply, blk);
        Sort(ply, n);

        if (tfc >= 0)
        {
            TTPrime2(ply, n, tfc, tfr, ttc2, ttr);  // Put the memory table's hint move at the front
        }

        int bestFc = -1, bestFr = 0, bestTc = 0, bestTr = 0;
        int best = -INF;
        bool firstChild = true;

        for (int i = 0; i < n; i++)
        {
            int fc = MB[ply, i, 0];
            int fr = MB[ply, i, 1];
            int tc = MB[ply, i, 2];
            int tr = MB[ply, i, 3];

            int cap = vb[tc, tr];

            // Skip moves that definitely can't raise alpha (the "too weak to bother" check)
            bool isQuietMove = cap == MT;
            bool futilityApplies = useFutility && i > 0 && isQuietMove;

            if (futilityApplies && futilityBase + 200 <= alpha)
            {
                continue;
            }

            int captured = Do(vb, fc, fr, tc, tr);
            ulong newHash = UHash(h, vb, fc, fr, tc, tr, captured);

            int score;

            // Moves we try late in the list are usually not that great.
            // So we look at them with one fewer level of depth first.
            // If they turn out surprisingly good, we search them at full depth.
            bool doLMR = !firstChild
                      && depth >= 3
                      && i >= 4
                      && cap == MT
                      && !inCheck;

            if (doLMR)
            {
                // Quick shallow search first
                score = -AB(vb, depth - 2, -alpha - 1, -alpha, !blk, newHash, ply + 1);

                if (score > alpha)
                {
                    // It's better than expected — now do the full search
                    score = -AB(vb, depth - 1, -beta, -alpha, !blk, newHash, ply + 1);
                }
            }
            else if (firstChild)
            {
                // The very first move always gets the full search
                score = -AB(vb, depth - 1, -beta, -alpha, !blk, newHash, ply + 1);
                firstChild = false;
            }
            else
            {
                // Other moves: quick narrow search, then full search only if needed
                score = -AB(vb, depth - 1, -alpha - 1, -alpha, !blk, newHash, ply + 1);

                if (score > alpha)
                {
                    score = -AB(vb, depth - 1, -beta, -alpha, !blk, newHash, ply + 1);
                }
            }

            Undo(vb, fc, fr, tc, tr, captured);

            if (score > best)
            {
                best = score;
                bestFc = fc; bestFr = fr; bestTc = tc; bestTr = tr;
            }

            if (score > alpha) alpha = score;

            // "Too good" means the opponent would never allow us to play this move.
            // So we don't need to look at any more moves from this position.
            if (alpha >= beta)
            {
                // Remember this move as a "killer" — it worked great at this depth
                if (cap == MT && ply < PLY_MAX)
                {
                    // Shift the old killer to slot 2, save the new one in slot 1
                    K2[ply, 0] = K1[ply, 0]; K2[ply, 1] = K1[ply, 1];
                    K2[ply, 2] = K1[ply, 2]; K2[ply, 3] = K1[ply, 3];
                    K1[ply, 0] = fc; K1[ply, 1] = fr;
                    K1[ply, 2] = tc; K1[ply, 3] = tr;

                    // Give this move a bigger history score so we try it early next time
                    int sideIndex = blk ? 1 : 0;
                    int historyBonus = depth * depth;
                    int fromSq = fc * 8 + fr;
                    int toSq = tc * 8 + tr;
                    int current = H[sideIndex, fromSq, toSq];
                    H[sideIndex, fromSq, toSq] = Math.Min(current + historyBonus, 10000);
                }

                break;
            }
        }

        // Label how reliable this score is so we know what to do if we see it again
        byte flag;

        if (best <= originalAlpha)
            flag = TTU;  // Wasn't good enough to beat alpha — at most this good
        else if (best >= beta)
            flag = TTL;  // Better than beta — at least this good
        else
            flag = TTE;  // Right in between — exact answer

        entry.h = h;
        entry.s = best;
        entry.d = (byte)Math.Min(depth, 127);
        entry.f = flag;

        if (bestFc >= 0)
        {
            entry.fc = (byte)bestFc; entry.fr = (byte)bestFr;
            entry.tc = (byte)bestTc; entry.tr = (byte)bestTr;
        }
        else
        {
            entry.fc = 255;  // 255 means "no best move was saved here"
        }

        return best;
    }


    //  KEEP LOOKING AFTER CAPTURES (Quiescence Search)
    //
    //  Imagine the AI thinks 4 moves ahead and stops.
    //  But what if on move 5 your queen gets captured for free?
    //  The AI would miss that and make a terrible mistake.
    //
    //  To fix this, after the main search ends we keep going, but
    //  ONLY look at captures. We stop once nobody is taking anything —
    //  the board is "calm." This way the AI never stops right before
    //  a big trade is about to happen.
    private int QS(int[,] vb, int alpha, int beta, bool blk, int ply)
    {
        qnodes++;

        if (ply >= PLY_MAX)
        {
            return Eval(vb, blk);
        }

        // Score the board without making any capture (the "just stand here" option).
        // The side to move can always choose not to capture, so this is a safe baseline.
        int standPat = Eval(vb, blk);

        // If doing nothing already beats beta, stop
        if (standPat >= beta) return beta;

        // If even capturing the most valuable piece (a queen, worth about 975)
        // wouldn't beat alpha, there's no point searching — just give up early
        if (standPat + 975 < alpha) return alpha;

        if (standPat > alpha) alpha = standPat;

        // Generate only capture moves
        MC[ply] = 0;
        Gen(vb, blk, ply, true);
        int n = MC[ply];

        // Sort captures so we try taking the most valuable piece first
        SortCaps(vb, ply, n);

        for (int i = 0; i < n; i++)
        {
            int fc = MB[ply, i, 0];
            int fr = MB[ply, i, 1];
            int tc = MB[ply, i, 2];
            int tr = MB[ply, i, 3];

            int cap = vb[tc, tr];

            if (cap == MT) continue;  // Safety check: skip non-captures

            int captured = Do(vb, fc, fr, tc, tr);
            int score = -QS(vb, -beta, -alpha, !blk, ply + 1);
            Undo(vb, fc, fr, tc, tr, captured);

            if (score >= beta) return beta;
            if (score > alpha) alpha = score;
        }

        return alpha;
    }


    //  IS THE KING IN CHECK?
    //  Returns true if the given side's king is being attacked right now.

    // Other scripts (like the GameManager) can call this version
    public bool IsInCheck(int[,] b, bool blk)
    {
        return Check(b, blk);
    }

    private bool Check(int[,] b, bool blk)
    {
        int king = blk ? BK : WK;

        // Look through every square to find the king
        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                if (b[c, r] == king)
                {
                    // Found the king — is any opponent piece pointing at this square?
                    return Attacked(b, c, r, !blk);
                }
            }
        }

        return false;  // King not found (shouldn't happen in a real game)
    }

    // Returns true if the square at (col, row) is being attacked by the given side.
    private bool Attacked(int[,] b, int col, int row, bool byBlk)
    {
        // CHECK FOR KNIGHTS
        int knightPiece = byBlk ? BN : WN;

        int[,] knightDeltas = {
            { 2, 1 }, { 2, -1 }, { -2, 1 }, { -2, -1 },
            { 1, 2 }, { 1, -2 }, { -1, 2 }, { -1, -2 }
        };

        for (int i = 0; i < 8; i++)
        {
            int nc = col + knightDeltas[i, 0];
            int nr = row + knightDeltas[i, 1];

            if (IB(nc, nr) && b[nc, nr] == knightPiece)
            {
                return true;  // A knight is attacking this square
            }
        }

        int queenPiece = byBlk ? BQ : WQ;
        int bishopPiece = byBlk ? BB : WB;
        int rookPiece = byBlk ? BR : WR;
        int kingPiece = byBlk ? BK : WK;
        int pawnPiece = byBlk ? BP : WP;

        // CHECK DIAGONALS
        int[,] diagDeltas = { { 1, 1 }, { 1, -1 }, { -1, 1 }, { -1, -1 } };

        for (int d = 0; d < 4; d++)
        {
            int dc = diagDeltas[d, 0];
            int dr = diagDeltas[d, 1];

            for (int s = 1; s < 8; s++)
            {
                int nc = col + dc * s;
                int nr = row + dr * s;

                if (!IB(nc, nr)) break;

                int piece = b[nc, nr];

                if (piece == MT) continue;  // Empty square, keep looking

                // Bishop or queen attacks along diagonals
                if (piece == bishopPiece || piece == queenPiece) return true;

                // King and pawn can only attack one square away
                if (s == 1 && piece == kingPiece) return true;

                if (s == 1 && piece == pawnPiece)
                {
                    // Black pawns attack downward, white pawns attack upward
                    bool blackPawnAttacking = byBlk && dr == -1;
                    bool whitePawnAttacking = !byBlk && dr == 1;

                    if (blackPawnAttacking || whitePawnAttacking) return true;
                }

                break;  // Something is blocking the path — stop looking in this direction
            }
        }

        // CHECK STRAIGHT LINES (Rook, Queen, King can all attack in straight lines)
        int[,] straightDeltas = { { 1, 0 }, { -1, 0 }, { 0, 1 }, { 0, -1 } };

        for (int d = 0; d < 4; d++)
        {
            int dc = straightDeltas[d, 0];
            int dr = straightDeltas[d, 1];

            for (int s = 1; s < 8; s++)
            {
                int nc = col + dc * s;
                int nr = row + dr * s;

                if (!IB(nc, nr)) break;

                int piece = b[nc, nr];

                if (piece == MT) continue;

                if (piece == rookPiece || piece == queenPiece) return true;

                if (s == 1 && piece == kingPiece) return true;

                break;
            }
        }

        return false;  // Nobody is attacking this square
    }

    //  SCORING THE BOARD
    //
    //  This function looks at the board and gives it a number.
    //  Positive = good for the side that's moving. Negative = bad.
    //
    //  look at:
    //    which pieces are alive and how valuable they are
    //    where those pieces are standing (position bonuses from the tables above)
    //    whether pawns are weak or strong
    //    whether one side has both bishops (that's a bonus!)
    //
    //  We also blend between "early game" and "late game" scoring
    //  based on how many big pieces are still on the board.
    private int Eval(int[,] vb, bool blk)
    {
        // figure out how far into the game
        // Count big pieces. Full early game = 24 points. Full late game = 0.
        int phase = 0;

        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                int p = vb[c, r];

                if (p != MT)
                {
                    int pieceType = Math.Abs(p);

                    if (pieceType == 4) phase += 2;  // Rook
                    else if (pieceType == 5) phase += 4;  // Queen
                    else if (pieceType == 2 || pieceType == 3) phase += 1;  // Knight or Bishop
                }
            }
        }

        phase = Math.Min(phase, 24);

        // ADD UP THE SCORES FOR BOTH SIDES
        int mgWhite = 0, egWhite = 0;  // White early/late game scores
        int mgBlack = 0, egBlack = 0;  // Black early/late game scores
        int whiteBishopCount = 0;
        int blackBishopCount = 0;

        // Clear pawn counters
        for (int i = 0; i < 8; i++) { ewp[i] = 0; ebp[i] = 0; }

        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                int p = vb[c, r];

                if (p == MT) continue;

                bool isWhite = p > 0;
                int pieceType = Math.Abs(p);

                // Flip the row for black pieces so the position tables work for both sides
                int tableRow = isWhite ? r : (7 - r);
                int pstIndex = tableRow * 8 + c;

                int mgValue = MAT[pieceType] + PST(pieceType, pstIndex, isMG: true);
                int egValue = MAT[pieceType] + PST(pieceType, pstIndex, isMG: false);

                if (isWhite)
                {
                    mgWhite += mgValue;
                    egWhite += egValue;
                    if (pieceType == 1) ewp[c]++;          // Count white pawns per column
                    if (pieceType == 3) whiteBishopCount++;
                }
                else
                {
                    mgBlack += mgValue;
                    egBlack += egValue;
                    if (pieceType == 1) ebp[c]++;          // Count black pawns per column
                    if (pieceType == 3) blackBishopCount++;
                }
            }
        }

        // BOTH BISHOPS BONUS
        // Having both your bishops is a known advantage in chess — they cover all squares
        if (whiteBishopCount >= 2) { mgWhite += 30; egWhite += 50; }
        if (blackBishopCount >= 2) { mgBlack += 30; egBlack += 50; }

        // PAWN SHAPE SCORING
        for (int f = 0; f < 8; f++)
        {
            // Doubled pawns: two or more pawns stuck on the same column = bad
            if (ewp[f] > 1) { mgWhite -= 10 * (ewp[f] - 1); egWhite -= 20 * (ewp[f] - 1); }
            if (ebp[f] > 1) { mgBlack -= 10 * (ebp[f] - 1); egBlack -= 20 * (ebp[f] - 1); }

            // Isolated pawn: a pawn with no friendly pawns on either neighboring column = weak
            bool whiteHasLeftNeighbour = f > 0 && ewp[f - 1] > 0;
            bool whiteHasRightNeighbour = f < 7 && ewp[f + 1] > 0;
            bool blackHasLeftNeighbour = f > 0 && ebp[f - 1] > 0;
            bool blackHasRightNeighbour = f < 7 && ebp[f + 1] > 0;

            if (ewp[f] > 0 && !whiteHasLeftNeighbour && !whiteHasRightNeighbour)
            {
                mgWhite -= 20; egWhite -= 20;  // Isolated white pawn penalty
            }

            if (ebp[f] > 0 && !blackHasLeftNeighbour && !blackHasRightNeighbour)
            {
                mgBlack -= 20; egBlack -= 20;  // Isolated black pawn penalty
            }

            // Passed pawn: a pawn with no enemy pawns blocking it or beside it.
            // It can march all the way to the end and become a queen!
            bool whitePassedPawn = ewp[f] > 0 && ebp[f] == 0
                                && (f == 0 || ebp[f - 1] == 0)
                                && (f == 7 || ebp[f + 1] == 0);

            bool blackPassedPawn = ebp[f] > 0 && ewp[f] == 0
                                && (f == 0 || ewp[f - 1] == 0)
                                && (f == 7 || ewp[f + 1] == 0);

            if (whitePassedPawn) { mgWhite += 15; egWhite += 30; }  // Bonus for having a free pawn
            if (blackPassedPawn) { mgBlack += 15; egBlack += 30; }
        }
        int mgScore = mgWhite - mgBlack;
        int egScore = egWhite - egBlack;

        // Mix the two scores together based on how many big pieces are left
        int score = (mgScore * phase + egScore * (24 - phase)) / 24;

        // Return from the moving side's point of view
        return blk ? -score : score;
    }

    // Looks up the position bonus for a piece on a particular square.
    private int PST(int t, int idx, bool isMG)
    {
        if (t == 1) return PST_P[idx];
        if (t == 2) return PST_N[idx];
        if (t == 3) return PST_B[idx];
        if (t == 4) return PST_R[idx];
        if (t == 5) return PST_Q[idx];
        if (t == 6) return isMG ? PST_KMG[idx] : PST_KEG[idx];
        return 0;
    }

    // Returns true if the given side has any pieces other than pawns and a king.
    // We check this before using the "skip a turn" trick, because skipping
    // when you only have pawns left can accidentally help the opponent (it's a trap!).
    private bool HasMat(int[,] b, bool blk)
    {
        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                int p = b[c, r];
                if (p == MT) continue;

                bool isMine = blk ? p < 0 : p > 0;
                int pType = Math.Abs(p);

                if (isMine && pType != 1 && pType != 6)
                {
                    return true;  // Found a knight, bishop, rook, or queen
                }
            }
        }

        return false;
    }

    //  MOVE GENERATION
    //
    //  Finds all the moves a side can legally make and stores them.
    //  If capOnly is true, only captures are included
    //  (used in the "keep looking after captures" function above).
    private void Gen(int[,] b, bool blk, int ply, bool capOnly)
    {
        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                int p = b[c, r];
                if (p == MT) continue;

                bool isBlackPiece = p < 0;
                bool isWhitePiece = p > 0;

                // Only look at pieces belonging to the side that's moving
                if (blk && isWhitePiece) continue;
                if (!blk && isBlackPiece) continue;

                int pieceType = Math.Abs(p);

                if (pieceType == 1) GenPawn(b, c, r, blk, ply, capOnly);
                else if (pieceType == 2) GenKnight(b, c, r, blk, ply, capOnly);
                else if (pieceType == 3) GenSlider(b, c, r, blk, ply, capOnly, diag: true, straight: false); // Bishop slides diagonally
                else if (pieceType == 4) GenSlider(b, c, r, blk, ply, capOnly, diag: false, straight: true);  // Rook slides straight
                else if (pieceType == 5) GenSlider(b, c, r, blk, ply, capOnly, diag: true, straight: true);  // Queen slides both ways
                else if (pieceType == 6) GenKing(b, c, r, blk, ply, capOnly);
            }
        }
    }

    // Saves one move into the move storage grid.
    private void AddMove(int ply, int fc, int fr, int tc, int tr)
    {
        if (MC[ply] < MAX_MOVES)
        {
            int slot = MC[ply];
            MB[ply, slot, 0] = fc;
            MB[ply, slot, 1] = fr;
            MB[ply, slot, 2] = tc;
            MB[ply, slot, 3] = tr;
            MC[ply]++;
        }
    }

    private void A(int ply, int fc, int fr, int tc, int tr) => AddMove(ply, fc, fr, tc, tr);

    // Generates all pawn moves: one step forward, two steps from starting row, and diagonal captures.
    private void GenPawn(int[,] b, int c, int r, bool blk, int ply, bool capOnly)
    {
        int direction = blk ? -1 : 1;  // Black pawns move down the board, white moves up
        int startRow = blk ? 6 : 1;  // The row where the pawn can move two squares at once

        if (!capOnly)
        {
            int oneStep = r + direction;

            if (IB(c, oneStep) && b[c, oneStep] == MT)
            {
                AddMove(ply, c, r, c, oneStep);  // One square forward

                int twoStep = r + 2 * direction;

                // From starting row, jump two squares if both squares ahead are empty
                if (r == startRow && IB(c, twoStep) && b[c, twoStep] == MT)
                {
                    AddMove(ply, c, r, c, twoStep);
                }
            }
        }

        // Capture diagonally (one step forward, one step to the left or right)
        int[] captureOffsets = new[] { -1, 1 };

        foreach (int dc in captureOffsets)
        {
            int nc = c + dc;
            int nr = r + direction;

            if (!IB(nc, nr)) continue;

            int targetPiece = b[nc, nr];

            bool whiteCapturingBlack = !blk && targetPiece < 0;
            bool blackCapturingWhite = blk && targetPiece > 0;

            if (whiteCapturingBlack || blackCapturingWhite)
            {
                AddMove(ply, c, r, nc, nr);
            }
        }
    }

    // Generates all 8 possible L-shaped knight hops.
    private void GenKnight(int[,] b, int c, int r, bool blk, int ply, bool capOnly)
    {
        int[,] knightDeltas = {
            { 2, 1 }, { 2, -1 }, { -2, 1 }, { -2, -1 },
            { 1, 2 }, { 1, -2 }, { -1, 2 }, { -1, -2 }
        };

        for (int i = 0; i < 8; i++)
        {
            int nc = c + knightDeltas[i, 0];
            int nr = r + knightDeltas[i, 1];

            if (!IB(nc, nr)) continue;

            int targetPiece = b[nc, nr];

            // Can't land on your own piece
            bool blockingFriendly = blk ? targetPiece < 0 : targetPiece > 0;
            if (blockingFriendly) continue;

            // In captures-only mode, skip empty squares
            if (capOnly && targetPiece == MT) continue;

            AddMove(ply, c, r, nc, nr);
        }
    }

    // Generates moves for sliding pieces (bishop = diagonal only, rook = straight only, queen = both).
    // A sliding piece keeps moving in one direction until it hits a wall or another piece.
    private void GenSlider(int[,] b, int c, int r, bool blk, int ply, bool capOnly,
                           bool diag, bool straight)
    {
        if (diag)
        {
            Slide(b, c, r, blk, ply, capOnly, +1, +1);  // Up-right
            Slide(b, c, r, blk, ply, capOnly, +1, -1);  // Down-right
            Slide(b, c, r, blk, ply, capOnly, -1, +1);  // Up-left
            Slide(b, c, r, blk, ply, capOnly, -1, -1);  // Down-left
        }

        if (straight)
        {
            Slide(b, c, r, blk, ply, capOnly, 0, +1);  // Up
            Slide(b, c, r, blk, ply, capOnly, 0, -1);  // Down
            Slide(b, c, r, blk, ply, capOnly, +1, 0);  // Right
            Slide(b, c, r, blk, ply, capOnly, -1, 0);  // Left
        }
    }

    // Walks step by step in direction (dc, dr), adding moves until something blocks the path.
    private void Slide(int[,] b, int c, int r, bool blk, int ply, bool capOnly, int dc, int dr)
    {
        for (int step = 1; step < 8; step++)
        {
            int nc = c + dc * step;
            int nr = r + dr * step;

            if (!IB(nc, nr)) break;  // Walked off the board, stop

            int targetPiece = b[nc, nr];

            // Your own piece is blocking — can't go here or further
            bool blockedByFriendly = blk ? targetPiece < 0 : targetPiece > 0;
            if (blockedByFriendly) break;

            bool isEmpty = targetPiece == MT;

            if (!capOnly || !isEmpty)
            {
                AddMove(ply, c, r, nc, nr);  // Add this as a valid move
            }

            // If we hit an enemy piece, we can capture it but can't slide further through it
            if (!isEmpty) break;
        }
    }

    // Generates all 8 king moves (one step in any direction — up, down, left, right, or diagonal).
    private void GenKing(int[,] b, int c, int r, bool blk, int ply, bool capOnly)
    {
        int[,] kingDeltas = {
            { 0,  1 }, { 0, -1 }, { 1,  0 }, { -1,  0 },
            { 1,  1 }, { 1, -1 }, { -1, 1 }, { -1, -1 }
        };

        for (int i = 0; i < 8; i++)
        {
            int nc = c + kingDeltas[i, 0];
            int nr = r + kingDeltas[i, 1];

            if (!IB(nc, nr)) continue;

            int targetPiece = b[nc, nr];

            // Can't land on your own piece
            bool blockedByFriendly = blk ? targetPiece < 0 : targetPiece > 0;
            if (blockedByFriendly) continue;

            // In captures-only mode, skip empty squares
            if (capOnly && targetPiece == MT) continue;

            AddMove(ply, c, r, nc, nr);
        }
    }

    // Returns true if column c and row r are inside the 8×8 board.
    private bool IB(int c, int r)
    {
        return c >= 0 && c < 8 && r >= 0 && r < 8;
    }



    //  MAKING AND UNDOING MOVES
    //
    //  Do() moves a piece on the board and returns what it captured.
    //  Undo() puts everything back exactly as it was before.
    //
    //  We need Undo() because the AI tries moves "in its head" without
    //  actually playing them on the real board. After thinking about a
    //  move, we erase it and try a different one.

    private int Do(int[,] b, int fc, int fr, int tc, int tr)
    {
        int captured = b[tc, tr];  // Remember what's at the destination (might be empty)
        b[tc, tr] = b[fc, fr];    // Move our piece to the destination
        b[fc, fr] = MT;           // Leave the starting square empty

        // Pawn promotion: if a pawn reaches the last row, it becomes a queen!
        if (b[tc, tr] == WP && tr == 7) b[tc, tr] = WQ;
        if (b[tc, tr] == BP && tr == 0) b[tc, tr] = BQ;

        return captured;
    }

    private void Undo(int[,] b, int fc, int fr, int tc, int tr, int captured)
    {
        int movedPiece = b[tc, tr];

        // If a promotion happened, turn the queen back into a pawn
        bool wasWhitePromotion = movedPiece == WQ && tr == 7 && fr == 6;
        bool wasBlackPromotion = movedPiece == BQ && tr == 0 && fr == 1;

        if (wasWhitePromotion) b[fc, fr] = WP;
        else if (wasBlackPromotion) b[fc, fr] = BP;
        else b[fc, fr] = movedPiece;

        b[tc, tr] = captured;  // Restore the captured piece (or leave the square empty)
    }


    //  SORTING MOVES (Move Ordering)
    //
    //  We want to try the best moves first. That way the "skip bad moves"
    //  trick cuts off more of the search tree and saves a lot of time.
    //
    //  Priority order:
    //    1. Captures — try taking pieces first (especially big pieces with small ones)
    //    2. Killer moves — quiet moves that worked well at this depth before
    //    3. Everything else, based on how useful they've been historically

    private void Score(int[,] b, int ply, int n, int searchPly, bool blk)
    {
        int colorIndex = blk ? 1 : 0;

        // Insertion sort — simple enough for small lists (up to 128 moves)
        for (int i = 1; i < n; i++)
        {
            int scoreI = MvScore(b, ply, i, searchPly, colorIndex);
            int j = i - 1;

            while (j >= 0 && MvScore(b, ply, j, searchPly, colorIndex) < scoreI)
            {
                SwapMoves(ply, j, j + 1);
                j--;
            }
        }
    }

    // Sorts capture moves so the most valuable captured piece comes first.
    private void SortCaps(int[,] b, int ply, int n)
    {
        for (int i = 1; i < n; i++)
        {
            int toCol_i = MB[ply, i, 2];
            int toRow_i = MB[ply, i, 3];
            int valueI = MAT[Math.Abs(b[toCol_i, toRow_i])];

            int j = i - 1;

            while (j >= 0)
            {
                int toCol_j = MB[ply, j, 2];
                int toRow_j = MB[ply, j, 3];
                int valueJ = MAT[Math.Abs(b[toCol_j, toRow_j])];

                if (valueJ >= valueI) break;

                SwapMoves(ply, j, j + 1);
                j--;
            }
        }
    }

    private void Sort(int ply, int n) { }  // Sorting is already done inside Score()

    // Returns a priority number for a move. Higher = better, try this move sooner.
    private int MvScore(int[,] b, int ply, int i, int searchPly, int colorIndex)
    {
        int fc = MB[ply, i, 0];
        int fr = MB[ply, i, 1];
        int tc = MB[ply, i, 2];
        int tr = MB[ply, i, 3];

        int capturedPiece = b[tc, tr];

        // Captures score highest — bonus for taking a big piece with a small one
        if (capturedPiece != MT)
        {
            int victimValue = MAT[Math.Abs(capturedPiece)];  // Value of the piece being taken
            int attackerValue = MAT[Math.Abs(b[fc, fr])];       // Value of our piece doing the taking
            return 10000 + victimValue * 10 - attackerValue;
        }

        // Killer moves: quiet moves that caused us to stop searching early before
        if (searchPly < PLY_MAX)
        {
            bool isKiller1 = K1[searchPly, 0] == fc && K1[searchPly, 1] == fr
                          && K1[searchPly, 2] == tc && K1[searchPly, 3] == tr;

            bool isKiller2 = K2[searchPly, 0] == fc && K2[searchPly, 1] == fr
                          && K2[searchPly, 2] == tc && K2[searchPly, 3] == tr;

            if (isKiller1) return 9000;
            if (isKiller2) return 8000;
        }

        // History: how useful this move has been in past searches
        int fromSq = fc * 8 + fr;
        int toSq = tc * 8 + tr;
        return H[colorIndex, fromSq, toSq];
    }

    // Swaps two moves in the storage grid.
    private void SwapMoves(int ply, int a, int b2)
    {
        for (int k = 0; k < 4; k++)
        {
            int temp = MB[ply, a, k];
            MB[ply, a, k] = MB[ply, b2, k];
            MB[ply, b2, k] = temp;
        }
    }

    private void Sw(int ply, int a, int b2) => SwapMoves(ply, a, b2);

    // Checks our memory table for a hint move and puts it at the front of the list.
    private void TTPrime(int ply, int n, ulong h)
    {
        ref TT e = ref tt[h & TT_MASK];

        if (e.h != h || e.fc == 255) return;  // No valid hint stored

        TTPrime2(ply, n, e.fc, e.fr, e.tc, e.tr);
    }

    // Finds a specific move in the list and swaps it to position 0 (front of the line).
    private void TTPrime2(int ply, int n, int fc, int fr, int tc, int tr)
    {
        for (int i = 1; i < n; i++)
        {
            bool matchFrom = MB[ply, i, 0] == fc && MB[ply, i, 1] == fr;
            bool matchTo = MB[ply, i, 2] == tc && MB[ply, i, 3] == tr;

            if (matchFrom && matchTo)
            {
                SwapMoves(ply, 0, i);
                return;
            }
        }
    }

    //  BOARD FINGERPRINTING

    // Scans the whole board and builds a fingerprint number from scratch.
    private ulong Hash(int[,] b, bool blk)
    {
        ulong h = 0;

        for (int r = 0; r < 8; r++)
        {
            for (int c = 0; c < 8; c++)
            {
                int p = b[c, r];

                if (p != MT)
                {
                    // XOR mixes in each piece's random number — like stamping it into the fingerprint
                    h ^= Z[PieceIndex(p), r * 8 + c];
                }
            }
        }

        if (blk) h ^= Zb;  // Mix in the "black to move" number

        return h;
    }

    // Updates the fingerprint after one move (much faster than rebuilding from scratch).
    private ulong UHash(ulong h, int[,] b, int fc, int fr, int tc, int tr, int captured)
    {
        int movedPiece = b[tc, tr];

        // Handle pawn promotions (the pawn became a queen during this move)
        bool wasWhitePromotion = movedPiece == WQ && tr == 7 && fr == 6;
        bool wasBlackPromotion = movedPiece == BQ && tr == 0 && fr == 1;

        int originalPiece = wasWhitePromotion ? WP
                          : wasBlackPromotion ? BP
                          : movedPiece;

        // "Un-stamp" the piece from its old square, then stamp it onto its new square
        h ^= Z[PieceIndex(originalPiece), fr * 8 + fc];
        h ^= Z[PieceIndex(movedPiece), tr * 8 + tc];

        // "Un-stamp" the captured piece
        if (captured != MT)
        {
            h ^= Z[PieceIndex(captured), tr * 8 + tc];
        }

        // Flip the "whose turn is it" part of the fingerprint
        h ^= Zb;

        return h;
    }

    // Converts a piece code (like WP=1 or BN=-2) to a table index from 0 to 11.
    // White pieces = 0–5, Black pieces = 6–11
    private int PieceIndex(int p)
    {
        int absType = Math.Abs(p);
        return p > 0 ? absType - 1 : absType - 1 + 6;
    }

    private int PI(int p) => PieceIndex(p);


    //  READING THE BOARD FROM UNITY
    //  The real chess game lives as 3D game objects in Unity.
    //  This function reads those objects and fills in an 8×8 number grid
    //  so all the AI math above can work with simple integers.

    private int[,] BuildVB()
    {
        int[,] vb = new int[8, 8];  // Start with a completely empty board

        var gm = GameManager.instance;
        if (gm == null) return vb;

        for (int c = 0; c < 8; c++)
        {
            for (int r = 0; r < 8; r++)
            {
                // Ask Unity: what game object (chess piece) is on this square?
                var go = gm.PieceAtGrid(new Vector2Int(c, r));
                if (go == null) continue;

                var pc = go.GetComponent<Piece>();
                if (pc == null) continue;

                bool isWhite = gm.white.pieces.Contains(go);

                // Convert the Unity piece type into our number code
                int code;
                if (pc.type == PieceType.Pawn) code = 1;
                else if (pc.type == PieceType.Knight) code = 2;
                else if (pc.type == PieceType.Bishop) code = 3;
                else if (pc.type == PieceType.Rook) code = 4;
                else if (pc.type == PieceType.Queen) code = 5;
                else if (pc.type == PieceType.King) code = 6;
                else code = 0;

                // White pieces get positive numbers, black pieces get negative numbers
                vb[c, r] = isWhite ? code : -code;
            }
        }

        return vb;
    }
}