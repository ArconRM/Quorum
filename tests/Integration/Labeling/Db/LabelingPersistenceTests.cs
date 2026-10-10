using Labeling.Domain.Common;
using Labeling.Domain.Consensus;
using Labeling.Domain.Labels;
using Labeling.Domain.Predictions;
using Labeling.Domain.Runs;
using Labeling.Domain.Teachers;
using Labeling.Infrastructure.Db;
using Labeling.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace Labeling.IntegrationTests.Db;

/// <summary>
/// Маппинг на настоящем Postgres: боксы в jsonb, стратегия как owned-тип, учителя прогона в отдельной таблице,
/// уникальность «один ответ учителя на кадр».
/// </summary>
[Trait("Category", "Integration")]
public sealed class LabelingPersistenceTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres = new PostgreSqlBuilder().WithImage("postgres:17-alpine").Build();

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await using var db = CreateDb();
        await db.Database.EnsureCreatedAsync();
    }

    public Task DisposeAsync() => _postgres.DisposeAsync().AsTask();

    private LabelingDbContext CreateDb() =>
        new(new DbContextOptionsBuilder<LabelingDbContext>().UseNpgsql(_postgres.GetConnectionString()).Options);

    private static LabelingRun NewRun(params TeacherModel[] teachers) =>
        LabelingRun.Start(Guid.NewGuid(), Guid.NewGuid(), TaskType.Detection, "license plate",
            ValidationStrategy.Create(ValidationMode.Hybrid, 0.7), teachers);

    [Fact]
    public async Task Run_predictions_and_labels_round_trip()
    {
        var teacher = TeacherModel.Register("owlv2-large", TeacherKind.OpenVocabDetector, TaskType.Detection, 0.8);
        var run = NewRun(teacher);
        var frameId = Guid.NewGuid();
        var boxes = new[] { new BoundingBox(0.1, 0.2, 0.15, 0.05, 0.93), new BoundingBox(0.6, 0.6, 0.1, 0.04, 0.4) };

        await using (var db = CreateDb())
        {
            db.Teachers.Add(teacher);
            db.Runs.Add(run);
            db.Predictions.Add(Prediction.Create(run.Id, frameId, teacher.Id, boxes, null, 0.66));
            db.Labels.Add(ConsensusLabel.Create(run.Id, frameId, "cam/f.jpg", DataSplit.Holdout, TaskType.Detection,
                new ConsensusResult([boxes[0]], null, 0.75), LabelStatus.Accepted));
            await db.SaveChangesAsync();
        }

        await using (var db = CreateDb())
        {
            var loadedRun = await new RunRepository(db).GetByCycleAsync(run.CycleId, CancellationToken.None);
            Assert.NotNull(loadedRun);
            Assert.Equal(ValidationMode.Hybrid, loadedRun!.Strategy.Mode);
            Assert.Equal(0.8, Assert.Single(loadedRun.Teachers).Weight);

            var prediction = Assert.Single(await new PredictionRepository(db).ListByRunAsync(run.Id, CancellationToken.None));
            Assert.Equal(2, prediction.Boxes.Count);
            Assert.Equal(0.93, prediction.Boxes[0].Score, 6);

            var label = Assert.Single(await new LabelRepository(db).ListAcceptedAsync(run.Id, CancellationToken.None));
            Assert.Equal(DataSplit.Holdout, label.Split);
            Assert.Equal(0.15, Assert.Single(label.Boxes).W, 6);
        }
    }

    [Fact]
    public async Task Teacher_cannot_answer_the_same_frame_twice()
    {
        var teacher = TeacherModel.Register("yolo-world-l", TeacherKind.OpenVocabDetector, TaskType.Detection, 1);
        var run = NewRun(teacher);
        var frameId = Guid.NewGuid();

        await using var db = CreateDb();
        db.Teachers.Add(teacher);
        db.Runs.Add(run);
        db.Predictions.Add(Prediction.Create(run.Id, frameId, teacher.Id, [], null, 0));
        db.Predictions.Add(Prediction.Create(run.Id, frameId, teacher.Id, [], null, 0));

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Concurrent_review_completion_is_detected()
    {
        var teacher = TeacherModel.Register("grounding-dino", TeacherKind.OpenVocabDetector, TaskType.Detection, 1);
        var run = NewRun(teacher);
        run.SetTotalFrames(2);
        run.CompleteTeacher(teacher.Id);
        run.FinishAggregation(0, 0, 2);
        await using (var db = CreateDb())
        {
            db.Teachers.Add(teacher);
            db.Runs.Add(run);
            await db.SaveChangesAsync();
        }

        // Два разметчика одновременно закрывают последние задания.
        await using var first = CreateDb();
        await using var second = CreateDb();
        var a = await first.Runs.SingleAsync(r => r.Id == run.Id);
        var b = await second.Runs.SingleAsync(r => r.Id == run.Id);
        a.RecordReview(LabelStatus.Accepted);
        b.RecordReview(LabelStatus.Accepted);
        await first.SaveChangesAsync();

        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => second.SaveChangesAsync());
    }
}
