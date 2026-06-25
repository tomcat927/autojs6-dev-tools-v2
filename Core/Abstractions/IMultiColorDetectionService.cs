using Core.Models;

namespace Core.Abstractions;

public interface IMultiColorDetectionService
{
    Task<MultiColorDetectionResult> DetectAsync(
        byte[] imageBytes,
        MultiColorDetectionRequest request,
        CancellationToken cancellationToken = default);
}
