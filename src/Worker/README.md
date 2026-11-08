# Inference worker

gRPC-сервер `InferenceWorker` по контракту `src/Shared/Quorum.Application/Protos/worker.proto`. Два режима (`WORKER_MODE`):

| Режим | Что делает | Где |
|---|---|---|
| `stub` (по умолчанию) | Имитирует работу без GPU: детерминированные предсказания из id кадра, падающий loss. Нужен демо-скрипту и проверке саги. | `quorum_worker/server.py`, `simulation.py` |
| `real` | Настоящие учителя, обучение и оценка студентов. | `quorum_worker/real.py` |

.NET-сервисы разницы не видят: контракт один.

## Настоящий режим

```
quorum_worker/
  common.py          бокс в долях кадра, нормализация текста (как TextConsensus.cs), чистка ответов VLM
  consensus.py       порт BoxConsensus / TextConsensus из C# + каппа Фляйса (для экспериментов)
  vram.py            одна тяжёлая модель в памяти за раз (16 ГБ)
  teachers/          учителя по имени из реестра Labeling
    detectors.py     GroundingDINO, OWLv2, YOLO-World (open-vocab) и YOLO с весами из MODELS_DIR
    vlm.py           VLM через OpenAI-совместимый сервер (llama-swap)
  students/
    yolo.py          YOLOv8n: датасет из нормированных боксов, стрим эпох, mAP50
    crnn.py          CRNN + CTC для номеров: accuracy (полное совпадение строки)
  real.py            сервис gRPC: ошибки → коды, которые .NET превращает в компенсации саги
experiments/         офлайн-эксперименты для практики (см. ниже)
tests/               pytest: консенсус сверен с C#-тестами, чистка ответов VLM, выбор учителя
```

### Какой учитель как называется

Имя учителя задаётся в `POST /labeling/teachers`. Воркер подбирает реализацию по префиксу, хвост вроде `-1234` игнорирует:

| kind | Имя | Модель |
|---|---|---|
| `OpenVocabDetector` | `grounding-dino`, `grounding-dino-tiny` | `IDEA-Research/grounding-dino-base` / `-tiny` |
| | `yolo-world-s/m/l/x` | `yolov8{s,m,l,x}-worldv2.pt` |
| | `owlv2`, `owlv2-large` | `google/owlv2-base-patch16-ensemble` / `-large-patch14-ensemble` |
| `Detector` | любое, например `yolov8l-plates` | файл `MODELS_DIR/yolov8l-plates.pt` |
| `Vlm` | `qwen3-vl-8b`, `gemma-3-4b`, … | модель с этим именем на `VLM_BASE_URL` (список — `llama-swap.yaml`) |

### Переменные

| Переменная | По умолчанию | Смысл |
|---|---|---|
| `WORKER_MODE` | `stub` | `stub` / `real` |
| `FRAMES_ROOT` | `/data/frames` | том с кадрами (пути в запросах — относительные) |
| `ARTIFACTS_DIR` | `/artifacts` | веса студентов, рабочие каталоги задач |
| `MODELS_DIR` | `/models` | свои веса для учителей `Detector` |
| `VLM_BASE_URL` | `http://llm:8080/v1` | OpenAI-совместимый сервер VLM |
| `VLM_PARALLEL` | `2` | одновременных запросов к VLM |
| `TEACHER_MAP_FILE` | — | JSON `{"имя учителя": "id модели"}`, если имена не совпадают |
| `GDINO_THRESHOLD`, `OWLV2_THRESHOLD`, `YOLO_WORLD_THRESHOLD`, `YOLO_THRESHOLD` | 0.3 / 0.15 / 0.1 / 0.25 | порог уверенности учителя — **параметр эксперимента** |
| `CRNN_PRETRAINED` | — | стартовые веса CRNN, если у камеры ещё нет опубликованной модели |
| `OCR_REQUIRE_DIGITS` | `3` | ответ VLM без стольких цифр считается «не читается» |
| `YOLO_IMGSZ`, `YOLO_BATCH`, `CRNN_BATCH`, `CRNN_LR` | 640, 16, 64, 1e-3 | обучение студентов |

Заглушка понимает прежние `STUB_FRAME_DELAY_MS`, `STUB_EPOCH_DELAY_MS`, `STUB_FAIL_TRAINING=1`.

## Эксперименты

Те же учителя и тот же консенсус, что в платформе, но без .NET: набор с эталонной разметкой → кэш предсказаний → метрики.

```bash
# 1. учителя по очереди, кэш в runs/<имя>/predictions/*.jsonl (повторный запуск продолжает с места остановки)
python -m experiments.run --task detection --data /datasets/detection --format coco \
    --teachers grounding-dino,yolo-world-l,owlv2 --prompt "license plate" \
    --limit 600 --per-domain --out runs/detect_v1

# 2. метрики и графики по кэшу — секунды, без GPU; веса и пороги можно менять сколько угодно
python -m experiments.evaluate --out runs/detect_v1
python -m experiments.evaluate --out runs/detect_v1 --weights grounding-dino=1.5,owlv2=0.7
```

Отчёт — `runs/<имя>/report/`: `summary.md`, `teachers.csv`, `strategies.csv`, `domains.csv`, `tradeoff.png`, `agreement.png`.

Форматы наборов: `coco` — каталоги стран из курсовой 2 курса (`<страна>/images`, `<страна>/labels/output_coco.json`);
`ocr` — кропы, текст номера в имени файла (`A123BC77___120___40.png`), домен — имя подкаталога.

## Предобучение CRNN

С нуля CTC-модели первые ~150–200 шагов оптимизатора выдают одни пустые символы. Одному циклу с парой сотен
принятых меток это почти всё обучение, поэтому CRNN один раз предобучается на большом наборе, а циклы дообучают его:

```bash
python -m quorum_worker.students.crnn --data /datasets/ocr_ru --out /models/crnn-ru.pt --epochs 30
```

Дальше `CRNN_PRETRAINED=/models/crnn-ru.pt` (в `docker-compose.gpu.yml` уже прописано). Со второго цикла стартовой
точкой становится опубликованная модель камеры.

## Тесты

```bash
cd src/Worker
python -m grpc_tools.protoc -I ../Shared/Quorum.Application/Protos --python_out=quorum_worker --grpc_python_out=quorum_worker worker.proto
sed -i 's/^import worker_pb2/from . import worker_pb2/' quorum_worker/worker_pb2_grpc.py
python -m pytest -q tests
```
