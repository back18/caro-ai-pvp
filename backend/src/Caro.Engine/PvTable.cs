using Caro.Domain;

namespace Caro.Engine;

/// <summary>
/// Triangular principal-variation table: <c>_lines[ply]</c> holds the best
/// play found from that ply downwards, so the line at ply 0 is the engine's
/// expected continuation from the root.
///
/// Nothing in the search reads this back — the chosen move and score come
/// from the ordinary alpha/beta bookkeeping — so a stale or truncated entry
/// can never change what the engine plays. It is evidence for callers only.
/// </summary>
internal sealed class PvTable
{
    private readonly Position[][] _lines;
    private readonly int[] _lengths;

    public PvTable(int maxPlies)
    {
        if (maxPlies < 1)
        {
            maxPlies = 1;
        }
        _lines = new Position[maxPlies][];
        _lengths = new int[maxPlies];
        for (int ply = 0; ply < maxPlies; ply++)
        {
            _lines[ply] = new Position[maxPlies];
        }
    }

    /// <summary>Empties every line. Call before each root search.</summary>
    public void ResetAll() => Array.Clear(_lengths);

    /// <summary>
    /// Records <paramref name="move"/> as the head of the line at
    /// <paramref name="ply"/>, splicing in the line already found one ply
    /// deeper. Out-of-range plies are ignored.
    /// </summary>
    public void Record(int ply, Position move)
    {
        if (ply < 0 || ply >= _lengths.Length)
        {
            return;
        }

        _lines[ply][0] = move;
        int childLength = 0;
        if (ply + 1 < _lengths.Length)
        {
            childLength = Math.Min(_lengths[ply + 1], _lines[ply].Length - 1);
            Array.Copy(_lines[ply + 1], 0, _lines[ply], 1, childLength);
        }
        _lengths[ply] = childLength + 1;
    }

    /// <summary>Returns a fresh copy of the line at <paramref name="ply"/>.</summary>
    public Position[] Line(int ply)
    {
        if (ply < 0 || ply >= _lengths.Length)
        {
            return [];
        }
        return _lines[ply][.._lengths[ply]];
    }
}
