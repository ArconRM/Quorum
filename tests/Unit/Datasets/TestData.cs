using Datasets.Domain.Cameras;
using Datasets.Domain.Frames;

namespace Datasets.UnitTests;

internal static class TestData
{
    public static Camera Camera(int minFrames = 5, int intervalHours = 24) =>
        Datasets.Domain.Cameras.Camera.Create("Въезд", "", TaskType.Detection, "license plate", minFrames, intervalHours);

    public static List<Frame> Frames(Camera camera, int count) =>
        Enumerable.Range(0, count)
            .Select(_ =>
            {
                var id = Guid.NewGuid();
                return Frame.Create(id, camera.Id, $"{camera.Id:N}/{id:N}.jpg", "image/jpeg", 1024, null);
            })
            .ToList();
}
