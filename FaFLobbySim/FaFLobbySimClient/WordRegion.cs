namespace FaFLobbySimClient;

internal class WordRegion
{
    public FafRectangle Bounds { get; init; }

    public FafPoint[] Points { get; init; }
}

internal readonly record struct FafPoint(short X, short Y);

internal record FafRectangle(FafPoint TopLeft, FafPoint BottomRight)
{
    public int Height => BottomRight.Y - TopLeft.Y;

    public int Width = BottomRight.X - TopLeft.X;
}