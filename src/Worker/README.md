# Inference worker (заглушка)

gRPC-сервер `InferenceWorker` по контракту `src/Shared/Quorum.Application/Protos/worker.proto`.

Сейчас он ничего не считает на GPU, а **имитирует** работу (`quorum_worker/simulation.py`):
- у каждого кадра есть «истинная» разметка, выведенная из его id;
- у каждого учителя — своё «качество» (выведено из имени): пропуски, сдвиги рамок, ложные срабатывания, ошибки в символах, кириллица у VLM;
- каждый 7-й кадр «сложный» — там учителя расходятся сильнее, и в гибридном режиме он уходит человеку;
- обучение — эпохи с падающим loss, итоговая метрика растёт с объёмом накопленных данных (дообучение от предыдущей версии);
- результат детерминирован: тот же вход — тот же ответ.

Настоящая реализация (open-vocab детекторы, VLM через llama.cpp, обучение YOLO/CRNN) заменит тела методов в `server.py`.

Запуск локально:

```bash
pip install -r src/Worker/requirements.txt
cd src/Worker
python -m grpc_tools.protoc -I ../Shared/Quorum.Application/Protos --python_out=quorum_worker --grpc_python_out=quorum_worker worker.proto
sed -i 's/^import worker_pb2/from . import worker_pb2/' quorum_worker/worker_pb2_grpc.py
ARTIFACTS_DIR=/tmp/artifacts python -m quorum_worker.server
```

Переменные: `WORKER_PORT`, `ARTIFACTS_DIR`, `STUB_FRAME_DELAY_MS`, `STUB_EPOCH_DELAY_MS`, `STUB_FAIL_TRAINING=1` (обучение падает — проверка компенсаций саги).
