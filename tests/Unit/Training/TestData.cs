using Training.Domain.Common;
using Training.Domain.Cycles;
using Training.Domain.Jobs;
using Training.Domain.Models;

namespace Training.UnitTests;

internal static class TestData
{
    public static AdaptationCycle CycleIn(CycleStatus status, Guid? cameraId = null)
    {
        var cycle = AdaptationCycle.Start(cameraId ?? Guid.NewGuid(), CycleTrigger.Manual,
            ValidationSettings.Create(ValidationMode.Hybrid, 0.7));
        if (status == CycleStatus.Snapshotting) return cycle;
        cycle.OnSnapshotCreated(Guid.NewGuid(), TaskType.Detection, 80, 20);
        if (status == CycleStatus.Labeling) return cycle;
        cycle.OnLabelingCompleted(Guid.NewGuid(), 90, minTrainingLabels: 10);
        if (status == CycleStatus.Training) return cycle;
        cycle.OnTrainingStarted(Guid.NewGuid());
        cycle.OnTrainingSucceeded(Guid.NewGuid());
        return cycle;
    }

    public static ModelVersion Version(Guid cameraId, int number)
    {
        var job = TrainingJob.Create(Guid.NewGuid(), cameraId, Guid.NewGuid(), TaskType.Detection, 5, null, null);
        job.Start(100);
        job.Succeed($"/artifacts/{job.Id}.json", "mAP50", 0.8);
        return ModelVersion.CreateCandidate(job, number);
    }
}
