using Datasets.Domain.Cameras;
using Quorum.Domain;

namespace Datasets.UnitTests.Domain;

public class CameraTests
{
    [Fact]
    public void New_camera_is_due_and_becomes_not_due_after_request()
    {
        var camera = TestData.Camera(intervalHours: 24);
        var now = DateTime.UtcNow;
        Assert.True(camera.IsCycleDue(now));

        camera.MarkCycleRequested(now);

        Assert.False(camera.IsCycleDue(now.AddHours(23)));
        Assert.True(camera.IsCycleDue(now.AddHours(24)));
    }

    [Fact]
    public void Inactive_camera_is_never_due_and_does_not_accept_frames()
    {
        var camera = TestData.Camera();
        camera.Deactivate();
        Assert.False(camera.IsCycleDue(DateTime.UtcNow));
        Assert.Throws<DomainException>(camera.EnsureCanAcceptFrames);
    }

    [Theory]
    [InlineData(0, 24)]
    [InlineData(10, 0)]
    public void Schedule_is_validated(int minFrames, int hours) =>
        Assert.Throws<DomainException>(() => Camera.Create("C", "", TaskType.Ocr, "read plate", minFrames, hours));

    [Fact]
    public void Prompt_is_required() =>
        Assert.Throws<DomainException>(() => Camera.Create("C", "", TaskType.Detection, " ", 10, 24));
}
