using Caro.Domain;
using Xunit;

namespace Caro.Engine.Tests;

/// <summary>
/// Covers the principal-variation output: the triangular table itself, and
/// the line the search publishes through <see cref="SearchStats"/>.
/// </summary>
public class PvTableTests
{
    [Fact]
    public void RecordSplicesTheDeeperLine()
    {
        PvTable pv = new(4);

        pv.Record(1, new Position(2, 2));
        pv.Record(0, new Position(1, 1));

        Position[] line = pv.Line(0);

        Assert.Equal(2, line.Length);
        Assert.Equal(new Position(1, 1), line[0]);
        Assert.Equal(new Position(2, 2), line[1]);
    }

    [Fact]
    public void ResetAllEmptiesEveryLine()
    {
        PvTable pv = new(4);
        pv.Record(1, new Position(2, 2));
        pv.Record(0, new Position(1, 1));

        pv.ResetAll();

        Assert.Empty(pv.Line(0));
        Assert.Empty(pv.Line(1));
    }

    [Fact]
    public void RecordOutsideTheTableIsIgnored()
    {
        PvTable pv = new(2);

        pv.Record(-1, new Position(1, 1));
        pv.Record(9, new Position(1, 1));

        Assert.Empty(pv.Line(0));
        Assert.Empty(pv.Line(1));
    }

    [Fact]
    public void SearchPublishesLineStartingAtTheChosenMove()
    {
        Board b = Board.NewBoard()
            .PlaceStone(7, 7, Player.Red)
            .PlaceStone(8, 8, Player.Blue)
            .PlaceStone(7, 8, Player.Red)
            .PlaceStone(6, 6, Player.Blue);

        using TranspositionTable tt = new(1);
        SearchConfig opts = new() { MaxDepth = 4, TimeLimitMs = 10_000, Threads = 1 };
        (int x, int y, SearchStats stats) = SearchEngine.SearchPosition(
            b, Player.Red, opts, tt, new SearchHeuristics(), CancellationToken.None);

        Assert.True(stats.DepthAchieved > 0, "the search must complete at least one ply");
        Assert.NotEmpty(stats.PrincipalVariation);
        Assert.Equal(x, stats.PrincipalVariation[0].X);
        Assert.Equal(y, stats.PrincipalVariation[0].Y);
    }

    [Fact]
    public void SearchLineNeverPlaysTheSameCellTwice()
    {
        Board b = Board.NewBoard()
            .PlaceStone(7, 7, Player.Red)
            .PlaceStone(8, 8, Player.Blue)
            .PlaceStone(7, 8, Player.Red)
            .PlaceStone(6, 6, Player.Blue);

        using TranspositionTable tt = new(1);
        SearchConfig opts = new() { MaxDepth = 5, TimeLimitMs = 10_000, Threads = 1 };
        (_, _, SearchStats stats) = SearchEngine.SearchPosition(
            b, Player.Red, opts, tt, new SearchHeuristics(), CancellationToken.None);

        // A spliced line that reuses a cell would mean the table copied a
        // stale continuation from an earlier iteration.
        Assert.Equal(stats.PrincipalVariation.Length, stats.PrincipalVariation.Distinct().Count());
    }

    [Fact]
    public void VcfWinPublishesItsWinningMove()
    {
        Board b = Board.NewBoard();
        for (int col = 3; col < 7; col++)
        {
            b = b.PlaceStone(col, 5, Player.Red);
        }
        b = b.PlaceStone(10, 10, Player.Blue);

        using TranspositionTable tt = new(1);
        SearchConfig opts = new() { MaxDepth = 4, TimeLimitMs = 5000, Threads = 1, UseVCF = true, VCFMaxDepth = 4 };
        (int x, int y, SearchStats stats) = SearchEngine.SearchPosition(
            b, Player.Red, opts, tt, new SearchHeuristics(), CancellationToken.None);

        Assert.Equal(MoveTypes.Vcf, stats.MoveType);
        Assert.Equal(SearchVerdict.ForcedWin, stats.Verdict);
        Assert.Single(stats.PrincipalVariation);
        Assert.Equal(x, stats.PrincipalVariation[0].X);
        Assert.Equal(y, stats.PrincipalVariation[0].Y);
    }
}
