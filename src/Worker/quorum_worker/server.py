"""
gRPC-сервер InferenceWorker (контракт: src/Shared/Quorum.Application/Protos/worker.proto).

Сейчас это ЗАГЛУШКА: работа имитируется задержками и детерминированными данными (simulation.py).
Настоящий инференс (GroundingDINO / YOLO-World / OWLv2, VLM через llama.cpp, обучение YOLO/CRNN)
подменит только тела методов — контракт и вызывающий .NET-код менять не придётся.

Переменные окружения:
  WORKER_PORT          порт gRPC (50051)
  ARTIFACTS_DIR        куда складывать «обученные модели» (/artifacts)
  STUB_FRAME_DELAY_MS  задержка на кадр при инференсе (15)
  STUB_EPOCH_DELAY_MS  задержка на эпоху при обучении (300)
  STUB_FAIL_TRAINING   1 — обучение падает (проверка компенсаций саги)
"""
import json
import logging
import os
import time
from concurrent import futures
from pathlib import Path

import grpc

from . import simulation as sim
from . import worker_pb2 as pb
from . import worker_pb2_grpc as pb_grpc

log = logging.getLogger("quorum.worker")

ARTIFACTS = Path(os.getenv("ARTIFACTS_DIR", "/artifacts"))
FRAME_DELAY = int(os.getenv("STUB_FRAME_DELAY_MS", "15")) / 1000
EPOCH_DELAY = int(os.getenv("STUB_EPOCH_DELAY_MS", "300")) / 1000


def _read_artifact(path: str) -> dict | None:
    try:
        return json.loads(Path(path).read_text()) if path else None
    except (OSError, ValueError):
        return None


class InferenceWorker(pb_grpc.InferenceWorkerServicer):
    def RunTeacher(self, request, context):
        log.info("RunTeacher %s (%s, %s): %d frames", request.teacher, request.kind, request.task, len(request.frames))
        for frame in request.frames:
            if not context.is_active():
                return
            time.sleep(FRAME_DELAY)
            if request.task == "Detection":
                boxes, confidence = sim.predict_boxes(request.teacher, frame.frame_id)
                yield pb.FramePrediction(
                    frame_id=frame.frame_id,
                    boxes=[pb.Box(x=b.x, y=b.y, w=b.w, h=b.h, score=b.score) for b in boxes],
                    confidence=confidence)
            elif request.task == "Ocr":
                text, confidence = sim.predict_text(request.teacher, request.kind, frame.frame_id)
                yield pb.FramePrediction(frame_id=frame.frame_id, text=text, confidence=confidence)
            else:
                context.abort(grpc.StatusCode.INVALID_ARGUMENT, f"Unknown task {request.task!r}")

    def TrainStudent(self, request, context):
        log.info("TrainStudent job=%s %s/%s: %d frames, %d epochs, base=%s",
                 request.job_id, request.task, request.architecture, len(request.frames), request.epochs,
                 request.base_artifact or "-")
        if not request.frames:
            context.abort(grpc.StatusCode.INVALID_ARGUMENT, "Empty training set")
        if os.getenv("STUB_FAIL_TRAINING") == "1":
            time.sleep(EPOCH_DELAY)
            context.abort(grpc.StatusCode.UNAVAILABLE, "Simulated GPU failure (STUB_FAIL_TRAINING=1)")

        for epoch in range(1, request.epochs + 1):
            if not context.is_active():
                return
            time.sleep(EPOCH_DELAY)
            yield pb.TrainProgress(epoch=epoch, epochs=request.epochs, loss=sim.loss_curve(epoch, request.epochs, request.job_id))

        # Дообучение: студент «помнит» данные предыдущей версии.
        base = _read_artifact(request.base_artifact)
        total = len(request.frames) + (base or {}).get("samples", 0)
        metric_name, metric = sim.trained_metric(request.task, total, request.job_id)

        ARTIFACTS.mkdir(parents=True, exist_ok=True)
        artifact = ARTIFACTS / f"{request.job_id}.json"
        artifact.write_text(json.dumps({
            "job_id": request.job_id, "task": request.task, "architecture": request.architecture,
            "samples": total, "metric_name": metric_name, "metric": metric,
        }))
        yield pb.TrainProgress(epoch=request.epochs, epochs=request.epochs, done=True,
                               artifact=str(artifact), metric_name=metric_name, metric_value=metric)

    def Evaluate(self, request, context):
        candidate = _read_artifact(request.candidate_artifact)
        if candidate is None:
            context.abort(grpc.StatusCode.NOT_FOUND, f"Candidate artifact {request.candidate_artifact!r} not found")
        holdout = [f.frame_id for f in request.holdout]
        metric_name = candidate.get("metric_name", "score")
        response = pb.EvaluateResponse(
            metric_name=metric_name,
            candidate=sim.evaluate(candidate["metric"], holdout, request.candidate_artifact))

        baseline = _read_artifact(request.baseline_artifact)
        if baseline is not None:
            response.baseline = sim.evaluate(baseline["metric"], holdout, request.baseline_artifact)
            response.has_baseline = True
        log.info("Evaluate %s: candidate=%.4f baseline=%s", metric_name, response.candidate,
                 f"{response.baseline:.4f}" if response.has_baseline else "-")
        return response


def serve() -> None:
    logging.basicConfig(level=logging.INFO, format="%(asctime)s %(levelname)s %(name)s: %(message)s")
    port = os.getenv("WORKER_PORT", "50051")
    server = grpc.server(futures.ThreadPoolExecutor(max_workers=8),
                         options=[("grpc.max_receive_message_length", 64 * 1024 * 1024)])
    pb_grpc.add_InferenceWorkerServicer_to_server(InferenceWorker(), server)
    server.add_insecure_port(f"[::]:{port}")
    server.start()
    log.info("Inference worker (stub) listening on :%s", port)
    server.wait_for_termination()


if __name__ == "__main__":
    serve()
