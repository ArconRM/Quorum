# GPU-воркер Quorum. Сейчас — заглушка без CUDA; для настоящего инференса базовый образ меняется
# на pytorch/pytorch:*-cuda* и добавляется runtime: nvidia в compose.
# Сборка из корня репозитория: docker build -f src/Worker/Dockerfile .
FROM python:3.12-slim
WORKDIR /app
ENV PYTHONUNBUFFERED=1

COPY src/Worker/requirements.txt .
RUN pip install --no-cache-dir -r requirements.txt

COPY src/Worker/quorum_worker ./quorum_worker
COPY src/Shared/Quorum.Application/Protos/worker.proto ./protos/worker.proto
# Код gRPC генерируется из того же .proto, что и у .NET-клиентов. Импорт делаем относительным (пакет).
RUN python -m grpc_tools.protoc -I protos --python_out=quorum_worker --grpc_python_out=quorum_worker protos/worker.proto \
 && sed -i 's/^import worker_pb2/from . import worker_pb2/' quorum_worker/worker_pb2_grpc.py

EXPOSE 50051
CMD ["python", "-m", "quorum_worker.server"]
