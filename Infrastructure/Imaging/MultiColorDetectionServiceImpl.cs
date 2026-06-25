using System.Diagnostics;
using Core.Abstractions;
using Core.Helpers;
using Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace Infrastructure.Imaging;

public sealed class MultiColorDetectionServiceImpl : IMultiColorDetectionService
{
    public async Task<MultiColorDetectionResult> DetectAsync(
        byte[] imageBytes,
        MultiColorDetectionRequest request,
        CancellationToken cancellationToken = default)
    {
        if (imageBytes.Length == 0)
        {
            throw new ArgumentException("Image bytes cannot be empty.", nameof(imageBytes));
        }

        var firstColor = AutoJsColorMatcher.ParseColor(request.FirstColor);
        var expectedOffsets = request.Offsets
            .Select(offset => (Offset: offset, Color: AutoJsColorMatcher.ParseColor(offset.Color)))
            .ToArray();

        var stopwatch = Stopwatch.StartNew();
        using var image = await Image.LoadAsync<Rgba32>(new MemoryStream(imageBytes), cancellationToken);
        var region = NormalizeRegion(request.Region, image.Width, image.Height);

        var result = request.Mode == MultiColorSearchMode.FindInRegion
            ? FindInRegion(image, request, firstColor, expectedOffsets, region, cancellationToken)
            : DetectAtAnchor(image, request, firstColor, expectedOffsets, region);

        stopwatch.Stop();
        result.ElapsedMilliseconds = stopwatch.ElapsedMilliseconds;
        return result;
    }

    private static MultiColorDetectionResult DetectAtAnchor(
        Image<Rgba32> image,
        MultiColorDetectionRequest request,
        RgbColor firstColor,
        IReadOnlyList<(MultiColorOffset Offset, RgbColor Color)> offsets,
        CropRegion? region)
    {
        var checks = BuildPointChecks(image, request.AnchorX, request.AnchorY, firstColor, offsets, request.Threshold);
        var inRegion = region == null || Contains(region, request.AnchorX, request.AnchorY);
        return new MultiColorDetectionResult
        {
            IsMatch = inRegion && checks.All(point => point.IsMatch),
            AnchorX = request.AnchorX,
            AnchorY = request.AnchorY,
            Region = region,
            Threshold = request.Threshold,
            CheckedCandidates = 1,
            PointChecks = checks
        };
    }

    private static MultiColorDetectionResult FindInRegion(
        Image<Rgba32> image,
        MultiColorDetectionRequest request,
        RgbColor firstColor,
        IReadOnlyList<(MultiColorOffset Offset, RgbColor Color)> offsets,
        CropRegion? region,
        CancellationToken cancellationToken)
    {
        var searchRegion = region ?? new CropRegion
        {
            X = 0,
            Y = 0,
            Width = image.Width,
            Height = image.Height,
            OriginalWidth = image.Width,
            OriginalHeight = image.Height
        };

        var checkedCandidates = 0;
        for (var y = searchRegion.Y; y < searchRegion.Y + searchRegion.Height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            for (var x = searchRegion.X; x < searchRegion.X + searchRegion.Width; x++)
            {
                var firstActual = ReadColor(image, x, y);
                var firstMatch = AutoJsColorMatcher.MatchDiff(firstActual, firstColor, request.Threshold);
                if (!firstMatch.IsMatch)
                {
                    continue;
                }

                checkedCandidates++;
                var checks = BuildPointChecks(image, x, y, firstColor, offsets, request.Threshold);
                if (checks.All(point => point.IsMatch))
                {
                    return new MultiColorDetectionResult
                    {
                        IsMatch = true,
                        AnchorX = x,
                        AnchorY = y,
                        Region = searchRegion,
                        Threshold = request.Threshold,
                        CheckedCandidates = checkedCandidates,
                        PointChecks = checks
                    };
                }
            }
        }

        return new MultiColorDetectionResult
        {
            IsMatch = false,
            Region = searchRegion,
            Threshold = request.Threshold,
            CheckedCandidates = checkedCandidates,
            PointChecks = []
        };
    }

    private static IReadOnlyList<MultiColorPointCheck> BuildPointChecks(
        Image<Rgba32> image,
        int anchorX,
        int anchorY,
        RgbColor firstColor,
        IReadOnlyList<(MultiColorOffset Offset, RgbColor Color)> offsets,
        int threshold)
    {
        var checks = new List<MultiColorPointCheck>
        {
            BuildPointCheck(image, "anchor", anchorX, anchorY, 0, 0, firstColor, threshold)
        };

        for (var i = 0; i < offsets.Count; i++)
        {
            var item = offsets[i];
            checks.Add(BuildPointCheck(
                image,
                $"p{i + 1}",
                anchorX + item.Offset.Dx,
                anchorY + item.Offset.Dy,
                item.Offset.Dx,
                item.Offset.Dy,
                item.Color,
                threshold));
        }

        return checks;
    }

    private static MultiColorPointCheck BuildPointCheck(
        Image<Rgba32> image,
        string label,
        int x,
        int y,
        int dx,
        int dy,
        RgbColor expected,
        int threshold)
    {
        if (x < 0 || y < 0 || x >= image.Width || y >= image.Height)
        {
            return new MultiColorPointCheck
            {
                Label = label,
                X = x,
                Y = y,
                Dx = dx,
                Dy = dy,
                ExpectedColor = expected.ToHex(),
                ActualColor = null,
                IsInBounds = false,
                IsMatch = false
            };
        }

        var actual = ReadColor(image, x, y);
        var match = AutoJsColorMatcher.MatchDiff(actual, expected, threshold);
        return new MultiColorPointCheck
        {
            Label = label,
            X = x,
            Y = y,
            Dx = dx,
            Dy = dy,
            ExpectedColor = expected.ToHex(),
            ActualColor = actual.ToHex(),
            DeltaR = match.DeltaR,
            DeltaG = match.DeltaG,
            DeltaB = match.DeltaB,
            DeltaSum = match.DeltaSum,
            IsInBounds = true,
            IsMatch = match.IsMatch
        };
    }

    private static RgbColor ReadColor(Image<Rgba32> image, int x, int y)
    {
        var pixel = image[x, y];
        return new RgbColor(pixel.R, pixel.G, pixel.B);
    }

    private static CropRegion? NormalizeRegion(CropRegion? region, int imageWidth, int imageHeight)
    {
        if (region == null)
        {
            return null;
        }

        var x = Math.Clamp(region.X, 0, imageWidth);
        var y = Math.Clamp(region.Y, 0, imageHeight);
        var width = Math.Clamp(region.Width, 0, imageWidth - x);
        var height = Math.Clamp(region.Height, 0, imageHeight - y);
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        return new CropRegion
        {
            X = x,
            Y = y,
            Width = width,
            Height = height,
            OriginalWidth = imageWidth,
            OriginalHeight = imageHeight
        };
    }

    private static bool Contains(CropRegion region, int x, int y)
    {
        return x >= region.X &&
               y >= region.Y &&
               x < region.X + region.Width &&
               y < region.Y + region.Height;
    }
}
