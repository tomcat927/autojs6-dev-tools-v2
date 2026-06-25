namespace Core.Models;

public enum MultiColorSearchMode
{
    DetectAtAnchor,
    FindInRegion
}

public sealed class MultiColorOffset
{
    public int Dx { get; init; }

    public int Dy { get; init; }

    public required string Color { get; init; }
}

public sealed class MultiColorDetectionRequest
{
    public MultiColorSearchMode Mode { get; init; } = MultiColorSearchMode.DetectAtAnchor;

    public int AnchorX { get; init; }

    public int AnchorY { get; init; }

    public required string FirstColor { get; init; }

    public IReadOnlyList<MultiColorOffset> Offsets { get; init; } = [];

    public CropRegion? Region { get; init; }

    public int Threshold { get; init; } = 16;
}

public sealed class MultiColorPointCheck
{
    public required string Label { get; init; }

    public int X { get; init; }

    public int Y { get; init; }

    public int Dx { get; init; }

    public int Dy { get; init; }

    public required string ExpectedColor { get; init; }

    public string? ActualColor { get; init; }

    public int DeltaR { get; init; }

    public int DeltaG { get; init; }

    public int DeltaB { get; init; }

    public int DeltaSum { get; init; }

    public bool IsInBounds { get; init; }

    public bool IsMatch { get; init; }
}

public sealed class MultiColorDetectionResult
{
    public bool IsMatch { get; init; }

    public int? AnchorX { get; init; }

    public int? AnchorY { get; init; }

    public CropRegion? Region { get; init; }

    public int Threshold { get; init; }

    public int CheckedCandidates { get; init; }

    public long ElapsedMilliseconds { get; set; }

    public IReadOnlyList<MultiColorPointCheck> PointChecks { get; init; } = [];
}
