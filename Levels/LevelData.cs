namespace AetherShatter.Levels
{
    /// <summary>
    /// Static repository of all hardcoded level layouts.
    ///
    /// <para>Each level is stored as a 2-D integer array where:
    /// <list type="bullet">
    ///   <item><c>0</c> = empty cell (no brick spawned here).</item>
    ///   <item><c>1</c> = Glass brick  — 1 hit, neon blue.</item>
    ///   <item><c>2</c> = Neon brick   — 2 hits, hot pink.</item>
    ///   <item><c>3</c> = Core brick   — 3 hits, bright yellow; destroys trigger screen-shake.</item>
    /// </list>
    /// </para>
    ///
    /// <para>All layouts use 10 columns to match the play-field width of 672 px
    /// (10 × (64 + 4) + 2 × 16 px margin).  Row count may vary between levels.</para>
    ///
    /// <para>To add new levels: define a new <c>int[,]</c> constant and append it to
    /// <see cref="All"/>.  No other code needs to change.</para>
    /// </summary>
    public static class LevelData
    {
        // ── Level 1 ──────────────────────────────────────────────────────────
        /// <summary>
        /// Classic symmetric grid — a gentle introduction.
        /// Mix of Glass (1 HP) and Neon (2 HP) bricks.
        /// No Core bricks; acts as a tutorial for the ball physics and power-ups.
        /// </summary>
        public static readonly int[,] Level1 =
        {
            //  col: 0  1  2  3  4  5  6  7  8  9
            {       0, 1, 1, 1, 1, 1, 1, 1, 1, 0  },  // row 0 — flanked by empty corners
            {       1, 1, 1, 1, 1, 1, 1, 1, 1, 1  },  // row 1 — full row of Glass
            {       1, 2, 1, 2, 1, 1, 2, 1, 2, 1  },  // row 2 — alternating Neon accents
            {       2, 2, 2, 2, 2, 2, 2, 2, 2, 2  },  // row 3 — full Neon wall
            {       0, 1, 2, 2, 2, 2, 2, 2, 1, 0  },  // row 4 — Neon centre, Glass flanks
            {       0, 0, 1, 1, 1, 1, 1, 1, 0, 0  },  // row 5 — compressed Glass bottom
        };

        // ── Level 2 ──────────────────────────────────────────────────────────
        /// <summary>
        /// Diamond of Core bricks surrounded by Neon rings.
        /// Introduces Core bricks (3 HP) for the first time — centre cluster
        /// rewards accurate ball control with the highest point values.
        /// </summary>
        public static readonly int[,] Level2 =
        {
            //  col: 0  1  2  3  4  5  6  7  8  9
            {       0, 0, 0, 1, 1, 1, 1, 0, 0, 0  },  // row 0 — sparse Glass cap
            {       0, 0, 2, 2, 2, 2, 2, 2, 0, 0  },  // row 1 — Neon ring
            {       0, 2, 3, 3, 2, 2, 3, 3, 2, 0  },  // row 2 — Core corners in Neon ring
            {       1, 2, 3, 3, 3, 3, 3, 3, 2, 1  },  // row 3 — full Core interior
            {       0, 2, 3, 3, 2, 2, 3, 3, 2, 0  },  // row 4 — mirror of row 2
            {       0, 0, 2, 2, 2, 2, 2, 2, 0, 0  },  // row 5 — mirror of row 1
            {       0, 0, 0, 1, 1, 1, 1, 0, 0, 0  },  // row 6 — mirror of row 0
        };

        // ── Level 3 ──────────────────────────────────────────────────────────
        /// <summary>
        /// Gauntlet — maximum difficulty.  Heavy Core walls with deliberately narrow
        /// gaps force precise ball angles and reward the Multiball power-up.
        /// Mixed-tier rows keep the player guessing which direction the ball will
        /// deflect after each bounce.
        /// </summary>
        public static readonly int[,] Level3 =
        {
            //  col: 0  1  2  3  4  5  6  7  8  9
            {       3, 3, 3, 3, 3, 3, 3, 3, 3, 3  },  // row 0 — solid Core ceiling
            {       3, 0, 3, 0, 3, 0, 3, 0, 3, 3  },  // row 1 — Core with alternating gaps
            {       2, 2, 2, 2, 2, 2, 2, 2, 2, 2  },  // row 2 — full Neon wall
            {       1, 3, 1, 3, 1, 3, 1, 3, 1, 3  },  // row 3 — alternating Glass / Core
            {       3, 1, 3, 1, 3, 1, 3, 1, 3, 1  },  // row 4 — inverse of row 3
            {       2, 2, 0, 2, 2, 2, 2, 0, 2, 2  },  // row 5 — Neon with two narrow gaps
            {       3, 3, 3, 3, 0, 0, 3, 3, 3, 3  },  // row 6 — Core with small centre gap
        };

        // ── Ordered sequence ──────────────────────────────────────────────────

        /// <summary>
        /// All levels in play order.  <c>GameplayState</c> indexes into this array and
        /// transitions to the Victory screen when it advances past the last entry.
        /// </summary>
        public static readonly int[][,] All = { Level1, Level2, Level3 };
    }
}
