"""
Настоящий InferenceWorker: учителя, обучение и оценка студентов. Контракт тот же, что у заглушки
(worker.proto), .NET-сервисы разницы не видят.

Ошибки → коды gRPC, которые Labeling/Training превращают в ExternalServiceException и запускают компенсации саги:
  нет весов / нет VLM-сервера / неизвестный учитель → FAILED_PRECONDITION
  пустая или битая выборка                          → INVALID_ARGUMENT
  нет артефакта                                     → NOT_FOUND
  всё остальное (OOM и т.п.)                         → INTERNAL
"""
from __future__ import annotations

import logging
import os
import threading
from pathlib import Path

import grpc

from . import worker_pb2 as pb
from . import worker_pb2_grpc as pb_grpc
from .common import Box
from .students import Epoch, Sample, Trained
from .teachers import TeacherError, resolve

log = logging.getLogger("quorum.worker")

ARTIFACTS = Path(os.getenv("ARTIFACTS_DIR", "/artifacts"))

# Одна GPU: обучение и инференс не должны идти одновременно (ConcurrentMessageLimit = 1 у очередей
# в .NET это обеспечивает, но воркер страхуется сам — его могут вызвать и в обход).
_gpu = threading.Lock()


def _samples(frames) -> list[Sample]:
    return [Sample(f.frame_id, f.path, [Box(b.x, b.y, b.w, b.h) for b in f.boxes], f.text) for f in frames]


def _abort_for(context, e: Exception):
    if isinstance(e, TeacherError):
        context.abort(grpc.StatusCode.FAILED_PRECONDITION, str(e))
    if isinstance(e, (ValueError, FileNotFoundError)):
        context.abort(grpc.StatusCode.INVALID_ARGUMENT, str(e))
    log.exception("Worker failure")
    context.abort(grpc.StatusCode.INTERNAL, f"{type(e).__name__}: {e}")


class RealWorker(pb_grpc.InferenceWorkerServicer):
    def RunTeacher(self, request, context):
        log.info("RunTeacher %s (%s, %s), prompt=%r: %d frames",
                 request.teacher, request.kind, request.task, request.prompt, len(request.frames))
        if request.task not in ("Detection", "Ocr"):
            context.abort(grpc.StatusCode.INVALID_ARGUMENT, f"Unknown task {request.task!r}")
        frames = [(f.frame_id, f.path) for f in request.frames]
        broken = 0
        with _gpu:
            try:
                teacher = resolve(request.teacher, request.kind)
                for p in teacher.predict(frames, request.prompt):
                    if not context.is_active():
                        log.info("Client cancelled RunTeacher %s", request.teacher)
                        return
                    broken += p.error is not None
                    yield pb.FramePrediction(
                        frame_id=p.frame_id, text=p.text, confidence=p.confidence,
                        boxes=[pb.Box(x=b.x, y=b.y, w=b.w, h=b.h, score=b.score) for b in p.boxes])
            except Exception as e:  # noqa: BLE001
                _abort_for(context, e)
        if broken:
            log.warning("Teacher %s: %d of %d frames unreadable", request.teacher, broken, len(frames))

    def TrainStudent(self, request, context):
        log.info("TrainStudent job=%s %s/%s: %d frames, %d epochs, base=%s", request.job_id, request.task,
                 request.architecture, len(request.frames), request.epochs, request.base_artifact or "-")
        if not request.frames:
            context.abort(grpc.StatusCode.INVALID_ARGUMENT, "Empty training set")
        if request.task not in ("Detection", "Ocr"):
            context.abort(grpc.StatusCode.INVALID_ARGUMENT, f"Unknown task {request.task!r}")
        samples = _samples(request.frames)
        job_dir = ARTIFACTS / "jobs" / request.job_id
        with _gpu:
            try:
                if request.task == "Detection":
                    from .students import yolo
                    events = yolo.train(job_dir, samples, request.architecture or "yolov8n", request.epochs,
                                        request.base_artifact, ARTIFACTS / f"{request.job_id}.pt")
                else:
                    from .students import crnn
                    events = crnn.train(samples, request.epochs, request.base_artifact, ARTIFACTS / f"{request.job_id}.pt")
                for ev in events:
                    if isinstance(ev, Epoch):
                        yield pb.TrainProgress(epoch=ev.epoch, epochs=ev.epochs, loss=ev.loss)
                    elif isinstance(ev, Trained):
                        log.info("Job %s done: %s=%.4f", request.job_id, ev.metric_name, ev.metric_value)
                        yield pb.TrainProgress(epoch=request.epochs, epochs=request.epochs, done=True,
                                               artifact=ev.artifact, metric_name=ev.metric_name,
                                               metric_value=ev.metric_value)
            except Exception as e:  # noqa: BLE001
                _abort_for(context, e)

    def Evaluate(self, request, context):
        if not Path(request.candidate_artifact).is_file():
            context.abort(grpc.StatusCode.NOT_FOUND, f"Candidate artifact {request.candidate_artifact!r} not found")
        if not request.holdout:
            context.abort(grpc.StatusCode.INVALID_ARGUMENT, "Empty holdout set")
        samples = _samples(request.holdout)
        # Базовая модель могла быть обучена заглушкой (JSON) или удалена — тогда сравнивать не с чем.
        baseline = request.baseline_artifact
        has_baseline = bool(baseline) and baseline.endswith(".pt") and Path(baseline).is_file()

        with _gpu:
            try:
                if request.task == "Detection":
                    from .students import yolo
                    work = ARTIFACTS / "eval" / Path(request.candidate_artifact).stem
                    score = lambda a, tag: yolo.evaluate_artifact(a, samples, work / tag)
                    metric = yolo.METRIC
                else:
                    from .students import crnn
                    score = lambda a, tag: crnn.evaluate_artifact(a, samples)
                    metric = crnn.METRIC
                response = pb.EvaluateResponse(metric_name=metric, candidate=score(request.candidate_artifact, "candidate"))
                if has_baseline:
                    response.baseline = score(baseline, "baseline")
                    response.has_baseline = True
            except Exception as e:  # noqa: BLE001
                _abort_for(context, e)
        log.info("Evaluate %s on %d frames: candidate=%.4f baseline=%s", metric, len(samples), response.candidate,
                 f"{response.baseline:.4f}" if response.has_baseline else "-")
        return response
