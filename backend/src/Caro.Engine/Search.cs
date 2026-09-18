using Caro.Domain;

namespace Caro.Engine;

public static partial class SearchEngine
{
    public static (int X, int Y, SearchStats Stats) SearchPosition(
        Board b,
        Player player,
        SearchConfig config,
        TranspositionTable tt,
        SearchHeuristics heuristics,
        CancellationToken ctx)
    {
        SearchBoard sb = new(b);
        List<Position> candidates = Candidates.GetCandidates(sb, Constants.Board.MaxSearchRadius);

        if (candidates.Count == 0)
        {
            return (-1, -1, default(SearchStats));
        }
        if (candidates.Count == 1)
        {
            return (candidates[0].X, candidates[0].Y, default(SearchStats));
        }

        int bestX = candidates[0].X;
        int bestY = candidates[0].Y;
        PvTable pv = new(Math.Min(config.MaxDepth, Constants.Search.AbsoluteMaxDepth) + 2);
        using TimeMonitor monitor = new(config.TimeLimitMs, ctx);

        tt.ResetStats();
        int bestScore = -Constants.Score.Infinity;
        int completedDepth = 0;
        Position[] principalVariation = [];
        int fullAlpha = -Constants.Score.Infinity;
        int fullBeta = Constants.Score.Infinity;

        if (config.UseVCF)
        {
            SearchBoard oppSB = new(b);
            if (!Vcf.OpponentHasImmediateWin(oppSB, player.Opponent()))
            {
                long vcfTime = (long)(config.TimeLimitMs * Constants.Vcf.TimeFraction);
                VcfSearchResult vcf = Vcf.SolveVCFWithDepth(b, player, config.VCFMaxDepth, vcfTime, ctx);
                if (vcf.Result == VCFResult.Win)
                {
                    return (vcf.X, vcf.Y, new SearchStats
                    {
                        DepthAchieved = 0,
                        SearchScore = Constants.Score.WinScore,
                        AllocatedTimeMs = config.TimeLimitMs,
                        MoveType = MoveTypes.Vcf,
                        VcfDepth = vcf.ChainDepth,
                        VcfNodes = vcf.NodesSearched,
                        PrincipalVariation = [new Position(vcf.X, vcf.Y)],
                    });
                }
            }
        }

        Position? vcfPreferred = null;
        if (config.UseVCF)
        {
            long oppVcfTime = (long)(config.TimeLimitMs * Constants.Vcf.BlockFraction);
            VcfSearchResult opp = Vcf.SolveVCFWithDepth(b, player.Opponent(), config.VCFMaxDepth, oppVcfTime, ctx);
            if (opp.Result == VCFResult.Win)
            {
                Board blocked = b.PlaceStone(opp.X, opp.Y, player);
                long blockCheckTime = (long)(config.TimeLimitMs * Constants.Vcf.BlockCheckFraction);
                VcfSearchResult check = Vcf.SolveVCFWithDepth(blocked, player.Opponent(), config.VCFMaxDepth, blockCheckTime, ctx);
                if (check.Result != VCFResult.Win)
                {
                    vcfPreferred = new Position(opp.X, opp.Y);
                }
            }
        }

        long lastIterMs = 0;
        long prevIterMs = 0;
        for (int depth = 1; depth <= config.MaxDepth; depth++)
        {
            if (monitor.ShouldStop())
            {
                break;
            }
            // Do not start an iteration that cannot finish inside the soft
            // budget; the hard bound stays as the emergency stop.
            if (depth > 1 && config.SoftLimitMs > 0 && monitor.ElapsedMs() >= config.SoftLimitMs)
            {
                break;
            }
            if (depth > 1 && !IterationBudget.NextIterationFits(monitor.ElapsedMs(), lastIterMs, prevIterMs, config.SoftLimitMs))
            {
                break;
            }
            long iterStart = monitor.ElapsedMs();

            int delta = Constants.Search.AspirationWindowSize;
            int a = fullAlpha;
            int betaBound = fullBeta;
            if (depth > 1)
            {
                a = Math.Max(bestScore - delta, fullAlpha);
                betaBound = Math.Min(bestScore + delta, fullBeta);
            }

            int x = 0;
            int y = 0;
            int score = 0;
            bool found = false;
            for (int attempt = 0; attempt < Constants.Search.MaxAspirationAttempts; attempt++)
            {
                pv.ResetAll();
                (x, y, score) = SearchRoot(sb, player, depth, a, betaBound, tt, heuristics, candidates, monitor, vcfPreferred, pv);
                if (x < 0 || monitor.ShouldStop())
                {
                    break;
                }
                if (score <= a && a > fullAlpha)
                {
                    a = Math.Max(a - delta, fullAlpha);
                    delta *= Constants.Search.AspirationWidenFactor;
                    continue;
                }
                if (score >= betaBound && betaBound < fullBeta)
                {
                    betaBound = Math.Min(betaBound + delta, fullBeta);
                    delta *= Constants.Search.AspirationWidenFactor;
                    continue;
                }
                found = true;
                break;
            }

            if (!found && !monitor.ShouldStop())
            {
                pv.ResetAll();
                (x, y, score) = SearchRoot(sb, player, depth, fullAlpha, fullBeta, tt, heuristics, candidates, monitor, vcfPreferred, pv);
                if (x >= 0)
                {
                    found = true;
                }
            }

            if (found)
            {
                bestX = x;
                bestY = y;
                bestScore = score;
                completedDepth = depth;
                // Captured only alongside a completed iteration: a later
                // iteration that runs out of time resets the table, and the
                // reported line must never describe a search that did not
                // finish.
                principalVariation = pv.Line(0);
                prevIterMs = lastIterMs;
                lastIterMs = monitor.ElapsedMs() - iterStart;
                if (MateScore.IsForcedWinScore(score))
                {
                    break;
                }
            }
        }

        if (completedDepth == 0)
        {
            // No depth finished in time. Fall back to the best-ordered move,
            // never the raw scan-order candidate list head.
            List<Position> ordered = MoveOrdering.OrderMoves(candidates, sb, player, 0, null, heuristics);
            if (ordered.Count > 0)
            {
                bestX = ordered[0].X;
                bestY = ordered[0].Y;
            }
            bestScore = 0;
        }

        long elapsed = monitor.ElapsedMs();
        (long probes, long hits) = tt.Stats();
        long nodes = monitor.NodesCount;
        double hitRate = 0;
        if (probes > 0)
        {
            hitRate = (double)hits / probes;
        }
        double nps = 0;
        if (elapsed > 0)
        {
            nps = (double)nodes / elapsed * Constants.Time.MsPerSecond;
        }

        string moveType = "";
        if (completedDepth == 0)
        {
            moveType = MoveTypes.TimeoutFallback;
        }
        return (bestX, bestY, new SearchStats
        {
            DepthAchieved = completedDepth,
            NodesSearched = nodes,
            NodesPerSecond = nps,
            SearchScore = bestScore,
            TableHitRate = hitRate,
            AllocatedTimeMs = config.TimeLimitMs,
            ThreadCount = 1,
            MoveType = moveType,
            PrincipalVariation = principalVariation,
        });
    }
}
