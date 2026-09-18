using Caro.Domain;

namespace Caro.Engine;

public static class Candidates
{
    public static List<Position> GetCandidates(SearchBoard sb, int radius)
    {
        BitBoard occupied = sb.Occupied();
        if (occupied.IsZero())
        {
            int center = Constants.Board.Size / 2;
            int halfSpan = Constants.Capacity.EmptyBoardSeedSpan / 2;
            List<Position> seed = new(Constants.Capacity.EmptyBoardSeedSpan * Constants.Capacity.EmptyBoardSeedSpan);
            for (int dx = 0; dx < Constants.Capacity.EmptyBoardSeedSpan; dx++)
            {
                for (int dy = 0; dy < Constants.Capacity.EmptyBoardSeedSpan; dy++)
                {
                    seed.Add(new Position(center + dx - halfSpan, center + dy - halfSpan));
                }
            }
            return seed;
        }

        // Stack-allocated dedup: a heap map per node dominated the profile.
        Span<bool> seen = stackalloc bool[Constants.Board.Size * Constants.Board.Size];
        seen.Clear();
        List<Position> result = new(Constants.Capacity.DefaultCandidateCapacity);

        for (int x = 0; x < Constants.Board.Size; x++)
        {
            for (int y = 0; y < Constants.Board.Size; y++)
            {
                if (!occupied.Get(x, y))
                {
                    continue;
                }
                for (int dx = -radius; dx <= radius; dx++)
                {
                    for (int dy = -radius; dy <= radius; dy++)
                    {
                        int nx = x + dx;
                        int ny = y + dy;
                        if (nx < 0 || nx >= Constants.Board.Size || ny < 0 || ny >= Constants.Board.Size)
                        {
                            continue;
                        }
                        int idx = ny * Constants.Board.Size + nx;
                        if (seen[idx] || !sb.IsEmpty(nx, ny))
                        {
                            continue;
                        }
                        seen[idx] = true;
                        result.Add(new Position(nx, ny));
                    }
                }
            }
        }

        return result;
    }

    public static List<Position> GetTacticalCandidates(SearchBoard sb, Player player)
    {
        List<Position> allCandidates = GetCandidates(sb, Constants.Board.MaxSearchRadius);
        if (allCandidates.Count == 0)
        {
            return [];
        }

        Player opponent = player.Opponent();
        List<Position> tactical = new(allCandidates.Count);

        foreach (Position c in allCandidates)
        {
            if (IsTacticalMove(sb, c.X, c.Y, player, opponent))
            {
                tactical.Add(c);
            }
        }

        return tactical;
    }

    internal static bool IsTacticalMove(SearchBoard sb, int x, int y, Player player, Player opponent)
    {
        // Win: creates exactly-5 (Caro-valid)
        sb.MakeMove(x, y, player);
        if (MoveOrdering.WouldWin(sb, x, y, player))
        {
            sb.UnmakeMove();
            return true;
        }
        sb.UnmakeMove();

        // Block: opponent would win here
        sb.MakeMove(x, y, opponent);
        if (MoveOrdering.WouldWin(sb, x, y, opponent))
        {
            sb.UnmakeMove();
            return true;
        }
        sb.UnmakeMove();

        // Four for either side (creating or blocking), plus double threats: a
        // move creating a four-or-open-three shape in two directions at once is
        // forcing, because the opponent cannot answer both lines with one stone.
        // A lone open three stays non-forcing (the opponent may convert or
        // ignore it), so it stays visible to eval and move ordering only.
        Span<sbyte> line = stackalloc sbyte[Constants.Board.LineLength];
        Span<sbyte> oppLine = stackalloc sbyte[Constants.Board.LineLength];
        int ownThreatDirs = 0;
        int oppThreatDirs = 0;
        foreach ((int dx, int dy) in Constants.Directions)
        {
            PatternWindow.ExtractLine(sb, x, y, player, dx, dy, line);
            PatternWindow.NegateLine(line, oppLine);
            if (PatternWindow.LineCompletions(line) >= Constants.Pattern.TacticalMinCompletions
                || PatternWindow.LineCompletions(oppLine) >= Constants.Pattern.TacticalMinCompletions)
            {
                return true;
            }
            if (PatternWindow.MaxCompsAfterFill(line) >= Constants.Pattern.TacticalDoubleThreatDirs)
            {
                ownThreatDirs++;
            }
            if (PatternWindow.MaxCompsAfterFill(oppLine) >= Constants.Pattern.TacticalDoubleThreatDirs)
            {
                oppThreatDirs++;
            }
        }
        return ownThreatDirs >= Constants.Pattern.TacticalDoubleThreatDirs
            || oppThreatDirs >= Constants.Pattern.TacticalDoubleThreatDirs;
    }
}
