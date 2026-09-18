using Caro.Domain;

namespace Caro.Engine;

/// <summary>
/// What a search score proves about the side to move.
/// </summary>
public enum SearchVerdict
{
    /// <summary>No forced result: the score is a heuristic judgement.</summary>
    None = 0,

    /// <summary>The side to move is proven to win.</summary>
    ForcedWin = 1,

    /// <summary>The side to move is proven to lose.</summary>
    ForcedLoss = 2,
}

public struct SearchConfig
{
    public int MaxDepth { get; set; }
    public long TimeLimitMs { get; set; }
    /// <summary>Stop starting new depths once elapsed passes this; 0 disables.</summary>
    public long SoftLimitMs { get; set; }
    public int Threads { get; set; }
    public bool UseVCF { get; set; }
    /// <summary>Attacker moves the VCF solver may chain; 0 = engine default.</summary>
    public int VCFMaxDepth { get; set; }
    public double TimeFraction { get; set; }
}

public struct SearchStats
{
    public SearchStats()
    {
    }

    public int DepthAchieved { get; set; }
    public long NodesSearched { get; set; }
    public double NodesPerSecond { get; set; }
    public int SearchScore { get; set; }

    /// <summary>
    /// Whether <see cref="SearchScore"/> proves a forced result — for either
    /// side — rather than being a heuristic judgement. Derived rather than
    /// stored so every construction site agrees by construction, and so
    /// callers outside this assembly need no knowledge of the score encoding.
    /// </summary>
    public SearchVerdict Verdict => MateScore.VerdictOf(SearchScore);

    public string MoveType { get; set; } = "";
    public double TableHitRate { get; set; }
    public long AllocatedTimeMs { get; set; }
    public int ThreadCount { get; set; }
    public int? VcfDepth { get; set; }
    public long? VcfNodes { get; set; }

    /// <summary>
    /// Best play found from the root, as the engine expects it to continue.
    /// Empty when no depth finished in time or nothing was searched.
    ///
    /// Backed by a field rather than an initialised auto-property so that
    /// <c>default(SearchStats)</c> — which the early-exit paths return —
    /// still yields an empty array instead of null.
    /// </summary>
    public Position[] PrincipalVariation
    {
        get => _principalVariation ?? [];
        set => _principalVariation = value;
    }

    private Position[]? _principalVariation;
}

public enum VCFResult
{
    NoWin = 0,
    Win = 1,
    Timeout = 2,
}

/// <summary>
/// Solver outcome with observability counters: one node per attacker
/// placement tried, ChainDepth = attacker moves in the winning forced
/// chain (0 when no win).
/// </summary>
public readonly record struct VcfSearchResult(
    int X,
    int Y,
    VCFResult Result,
    long NodesSearched,
    int ChainDepth);
