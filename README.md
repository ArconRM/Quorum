# Quorum — микросервисная система непрерывной адаптации моделей компьютерного зрения

Учебный проект по микросервисной архитектуре на домене дипломной работы
«Разработка системы непрерывной адаптации моделей компьютерного зрения к целевому домену».

**Идея.** Тяжёлые модели (open-vocab детекторы, мультимодальные LLM) точны, но медленные; лёгкие модели реального
времени теряют качество на данных конкретной камеры. Quorum замыкает цикл: камера копит кадры → ансамбль
моделей-учителей размечает их без человека → метка принимается, если за неё набран **кворум** учителей →
спорные кадры (по выбранной стратегии) проверяет разметчик → на псевдоразметке дообучается лёгкий студент →
он публикуется на камеру, только если на новых кадрах лучше текущей модели. И так периодически.

---

## 1. Покрытие требований

| # | Требование | Где реализовано |
|---|---|---|
| 1 | Предметная область, 10–15 агрегатов | 4 контекста, 13 агрегатов (§2) |
| 2 | DDD: агрегаты и контексты | §2, проекты `*.Domain` |
| 3 | Микросервис на контекст, REST, чистая архитектура | `src/<Сервис>/{Domain,Application,Infrastructure,Service}`, фичи через MediatR (§8) |
| 4 | Docker, docker compose | `Dockerfile`, `src/Worker/Dockerfile`, `docker-compose.yml` |
| 5 | Кеш Redis | Training: опубликованная модель камеры (HybridCache, L2 = Redis) |
| 6 | PostgreSQL | База на сервис: `identity`, `datasets`, `labeling`, `training` (Postgres в Docker, `localhost:5432`) |
| 7 | Распределённая транзакция | Сага «цикл адаптации»: Datasets → Labeling → Training (+ GPU-воркер) с компенсациями (§3) |
| 8 | Transactional outbox / inbox | MassTransit EF Core Outbox в БД каждого сервиса (§4) |
| 9 | Брокер сообщений | RabbitMQ (+ management UI на :15672) |
| 10 | gRPC | Labeling → Datasets, Training → Labeling, Labeling/Training → воркер (стримы) (§5) |
| 11 | Единый сервис авторизации, JWT | Identity выпускает JWT, остальные проверяют подпись локально (§7) |
| 12 | Unit + интеграционные тесты | `tests/Unit/<Сервис>`, `tests/Integration/<Сервис>` (Testcontainers) (§11) |

---

## 2. Предметная область и DDD-модель

### 2.1 Контексты = микросервисы

```
             ┌──────────────┐  JWT (подпись проверяется локально в каждом сервисе)
             │   Identity   │───────────────────────────────────────────────────┐
             │  БД identity │                                                   │
             └──────────────┘                                                   ▼
┌──────────────────┐ CreateSnapshot / ReleaseSnapshot  ┌──────────────────────────────────┐
│     Datasets     │◄──────────────────────────────────│             Training             │
│ камеры, кадры,   │──SnapshotCreated / Rejected──────►│ сага «цикл адаптации», обучение, │
│ снимки,          │──AdaptationRequested─────────────►│ версии моделей, оценка  (+Redis) │
│ планировщик      │                                   │          БД training             │
│  БД datasets     │                                   └───┬───────────────▲──────────┬───┘
└────────▲─────────┘                  StartLabeling /      │               │          │ gRPC TrainStudent,
         │ gRPC SnapshotQuery         DiscardLabeling      ▼   LabelingCompleted/Failed │ Evaluate (стрим)
┌────────┴─────────────────────────────────────────────────────────┐       │          ▼
│                             Labeling                              │───────┘   ┌──────────────┐
│ учителя, прогоны, предсказания (кэш), консенсус, проверка людьми  │──gRPC────►│  GPU-воркер  │
│                           БД labeling                             │ RunTeacher│  (Python,    │
└──────────────────────────────────▲───────────────────────────────┘  (стрим)  │   заглушка)  │
                                   └── gRPC TrainingSetQuery ◄── Training       └──────────────┘
```

| Сервис | Ответственность | Агрегаты | БД |
|---|---|---|---|
| **Identity** | Пользователи, роли, выпуск JWT | `User` | `identity` |
| **Datasets** | Камеры и их расписание, приём кадров, замороженные снимки, планировщик циклов | `Camera`, `Frame`, `DatasetSnapshot` | `datasets` |
| **Labeling** | Реестр учителей, прогоны ансамбля, кэш предсказаний, консенсус, проверка людьми | `TeacherModel`, `LabelingRun`, `Prediction`, `ConsensusLabel`, `ReviewTask` | `labeling` |
| **Training** | Оркестрация цикла, обучение студента, версии моделей, оценка и публикация | `AdaptationCycle`, `TrainingJob`, `ModelVersion`, `Evaluation` | `training` |
| GPU-воркер | Исполнитель: инференс учителей, обучение, оценка | — (без состояния) | — |

**Роли:** `Admin` — всё; `Operator` — камеры, учителя, запуск циклов, откат моделей; `Annotator` — проверка меток.
Саморегистрация даёт роль `Annotator`, остальные роли выдаёт администратор.

Задачи: **детекция** (`Detection`, например номерные пластины) и **распознавание текста** (`Ocr`).

### 2.2 Агрегаты и инварианты

**Identity**
- `User` — email (value object, нормализован, уникален), ФИО, хеш пароля (PBKDF2), роли. У пользователя всегда хотя бы одна роль.

**Datasets**
- `Camera` — название, задача, текстовый запрос для учителей (`license plate`), расписание: «не чаще раза в N часов и не меньше M новых кадров». Неактивная камера не принимает кадры и не запрашивает циклы.
- `Frame` — кадр: путь в хранилище, тип (только изображения), размер, время съёмки. `SnapshotId` — в какой снимок забран; пока `null`, кадр «новый». Конкурентность через `xmin`: два снимка не заберут один кадр.
- `DatasetSnapshot` — замороженный набор кадров под один цикл: `Locked → Used | Discarded`. На камере одновременно не больше одного `Locked`. Детерминированное разбиение: каждый 5-й кадр — в отложенную выборку (`Holdout`), остальные — в обучение.

**Labeling**
- `TeacherModel` — учитель: вид (`OpenVocabDetector` / `Detector` / `Vlm`), задача, вес в голосовании. VLM — только для OCR (их боксы слишком грубые), детекторы — только для детекции.
- `LabelingRun` — прогон ансамбля по снимку: стратегия валидации, снимок весов учителей на момент старта, прогресс каждого учителя, счётчики. `Running → Reviewing → Completed`, `Failed`, `Discarded` (компенсация). Каждый кадр получает ровно одну метку; последняя проверка закрывает прогон ровно один раз (счётчик + `xmin`).
- `Prediction` — ответ одного учителя на один кадр (боксы в jsonb или текст). Хранится всегда — это **кэш**, на котором агрегацию можно переиграть с другими порогами и весами без повторного инференса. Уникальность «учитель × кадр × прогон».
- `ConsensusLabel` — итоговая метка кадра: значение, согласие ансамбля, статус `Accepted / Rejected / NeedsReview`, источник (`Consensus` / `Human`).
- `ReviewTask` — задание разметчику: `Open → Claimed → Done`, `Cancelled`. Чужое взятое задание не отнять, пока оно не брошено дольше 30 минут.
- `ValidationStrategy` (value object) — **главная развилка исследования**:
  - `Manual` — каждую метку смотрит человек (консенсус — подсказка);
  - `Auto` — согласие ≥ порога → принять, иначе отбросить;
  - `Hybrid` — согласие ≥ порога → принять, иначе человеку.

**Training**
- `AdaptationCycle` — агрегат и одновременно оркестратор саги (§3). Один активный цикл на камеру (инвариант + частичный уникальный индекс в БД).
- `TrainingJob` — обучение студента: архитектура по задаче (`yolov8n` / `crnn`), эпохи, прогресс, дообучение от текущей опубликованной версии.
- `ModelVersion` — версия модели камеры: `Candidate → Published | Rejected`, `Published → Archived`, откат `Archived → Published`. Опубликована ровно одна версия на камеру (доменный сервис `ModelPublication` + частичный уникальный индекс).
- `Evaluation` — сравнение кандидата с опубликованной моделью на отложенной выборке цикла: публикуем, если опубликованной нет или кандидат лучше не меньше чем на `MinImprovement`.

### 2.3 Консенсус (Labeling.Domain/Consensus)

Чистые функции без зависимостей, покрыты unit-тестами.

**Детекция** — упрощённый Weighted Box Fusion с кворумом:
1. боксы всех учителей сортируются по `score × вес учителя`;
2. жадная кластеризация по IoU ≥ 0.5, один учитель — один голос за объект;
3. поддержка кластера = вес учителей в нём / вес всех учителей; объект принимается, если поддержка **строго больше половины**;
4. координаты — среднее, взвешенное по `score × вес`;
5. **согласие кадра** = среднее по кластерам `max(поддержка, 1 − поддержка)`: насколько решительно ансамбль ответил на вопрос «есть ли тут объект». Все молчат — согласие 1.

**Текст** — взвешенное голосование после нормализации (верхний регистр, без пробелов и разделителей, кириллица-двойник → латиница: VLM часто путают «А» и «A»). Пустой ответ — тоже голос («не читается»). Согласие = вес победителя / общий вес.

### 2.4 Связи между контекстами

- **Published Language** — `src/Shared/Quorum.Application/Contracts` (события и команды) и `.../Protos` (gRPC).
- **Anti-Corruption Layer** — каждый сервис видит соседей через свои порты (`IFrameSource`, `ITeacherRunner`, `ILabeledDataSource`, `ITrainingWorker`) со своими моделями; gRPC-клиенты спрятаны в Infrastructure.
- **Снимки данных** — Labeling копирует путь и split кадра в метку; прогон фиксирует веса учителей на старте.
- Чужие агрегаты — только по идентификатору (`CameraId`, `SnapshotId`, `RunId`, `FrameId`).

---

## 3. Распределённая транзакция: сага «цикл адаптации»

Оркестратор — агрегат `AdaptationCycle` в Training (переходы и решение о компенсациях — в домене, отправка команд — в хендлерах).
Запуск — планировщиком Datasets (`AdaptationRequested`, хореография) или вручную (`POST /cycles`).

```
            ┌──────────────┐ CreateSnapshot        ┌──────────┐
 Start ───► │ Snapshotting │──────────────────────►│ Datasets │  забрать новые кадры в снимок
            └──────┬───────┘◄── SnapshotCreated ───┘
                   │        ◄── SnapshotRejected ──► Skipped (мало кадров / камера занята)
                   ▼
            ┌──────────────┐ StartLabeling         ┌──────────┐  учителя (gRPC-стрим к воркеру) → консенсус
            │   Labeling   │──────────────────────►│ Labeling │  → по стратегии: принять / отбросить / человеку
            └──────┬───────┘◄── LabelingCompleted ─┘           (ждём, пока разметчики разберут спорные)
                   │        ◄── LabelingFailed ──► ReleaseSnapshot(discard) → Failed
                   ▼
            ┌──────────────┐ RunTrainingJob (своя очередь) → обучающая выборка из Labeling (gRPC)
            │   Training   │ → обучение на воркере (gRPC-стрим эпох) → ModelVersion(Candidate)
            └──────┬───────┘   мало меток / воркер упал ──► DiscardLabeling + ReleaseSnapshot(discard) → Failed
                   ▼
            ┌──────────────┐ EvaluateCandidate → кандидат и текущая модель на отложенной выборке
            │  Evaluating  │   лучше → Published (старая → Archived), хуже → Rejected
            └──────┬───────┘
                   ▼
            ReleaseSnapshot(used) → Completed (Promoted | KeptCurrent)
```

| Шаг | Сервис (БД) | Локальная транзакция | Отказ | Компенсация |
|---|---|---|---|---|
| 1 | Datasets (`datasets`) | Снимок + пометка кадров | Мало кадров, камера занята | — (цикл `Skipped`) |
| 2 | Labeling (`labeling`) | Прогон, предсказания, метки, задания | Нет учителей, воркер / Datasets недоступны | Отбросить снимок: кадры снова «новые» |
| 3 | Training (`training`) | Задача обучения, версия-кандидат | Мало принятых меток, воркер упал | Пометить разметку неиспользованной + отбросить снимок |
| 4 | Training (`training`) | Оценка, публикация / отклонение | Воркер недоступен | Как в шаге 3 |
| ✔ | Datasets (`datasets`) | Снимок `Used` | — | — |

Главная компенсация — **кадры не теряются**: если цикл провалился, снимок отбрасывается, кадры
снова считаются новыми и попадут в следующий цикл. Это видно в демо (`DEMO_FAILURE=1`).

Все шаги **идемпотентны** (RabbitMQ — at-least-once): повторная команда по тому же циклу повторяет ответ,
событие не того шага игнорируется, учитель при перезапуске пропускает уже размеченные кадры.

---

## 4. Transactional Outbox / Inbox

MassTransit EF Core Outbox (`Quorum.Core/Messaging`), таблицы `OutboxMessage`, `OutboxState`, `InboxState` в БД каждого сервиса.

- **Outbox.** `StartCycleHandler` создаёт цикл и публикует `CreateSnapshot` — сообщение пишется в outbox в той же транзакции и уходит в брокер только после коммита. Не бывает «цикл есть, команды нет» и наоборот.
- **Inbox.** Каждый консьюмер обёрнут в `UseEntityFrameworkOutbox`: дубли отсекаются по `MessageId`, а исходящие сообщения консьюмера отправляются только при успешном коммите его транзакции.
- Долгие шаги (`ProcessLabelingRun`, `RunTrainingJob`) идут отдельными внутренними сообщениями через свою очередь с `ConcurrentMessageLimit = 1` (одна GPU — одна задача).

## 5. gRPC

Контракты в `src/Shared/Quorum.Application/Protos`. Серверы — в `*.Service/Grpc`, клиенты — в `*.Infrastructure/Grpc` за портами Application-слоя.

| Клиент → Сервер | Метод | Зачем синхронно |
|---|---|---|
| Labeling → Datasets | `SnapshotQuery.GetSnapshotFrames` | Состав снимка (пути и split) перед прогоном учителей |
| Labeling → воркер | `InferenceWorker.RunTeacher` (server stream) | Предсказания по кадрам по мере готовности |
| Training → Labeling | `TrainingSetQuery.GetTrainingSet` | Принятые метки для обучения и оценки |
| Training → воркер | `InferenceWorker.TrainStudent` (server stream) | Прогресс по эпохам, в конце — артефакт и метрика |
| Training → воркер | `InferenceWorker.Evaluate` | Метрики кандидата и текущей модели на отложенной выборке |

Datasets и Labeling слушают `8080` (REST, HTTP/1.1) и `8081` (gRPC, HTTP/2), воркер — `50051`.
Ошибки gRPC превращаются в `ExternalServiceException`, хендлеры саги ловят её и запускают компенсации.

## 6. Кеширование (Redis)

Training: `GET /cameras/{id}/model` — какую модель запускать на камере. Его часто опрашивают устройства,
поэтому ответ кешируется в `HybridCache` (L1 — память процесса, L2 — Redis, защита от cache stampede).
Инвалидация — **после** коммита публикации или отката.

## 7. Авторизация (JWT)

- **Identity** выдаёт JWT (HMAC-SHA256) с клеймами `sub`, `email`, `name`, `role`.
- Остальные сервисы проверяют подпись локально общим ключом, без запросов к Identity.
- Администратор создаётся при старте из `Admin:Email` / `Admin:Password`.

---

## 8. Архитектура и структура репозитория

Верхний уровень `src` — сервисы; внутри каждого четыре проекта, зависимости строго внутрь:

```
<Сервис>.Service         Endpoints/, Grpc/ (серверы), Program.cs — composition root
      ▼
<Сервис>.Infrastructure  Db/ (DbContext, Configurations/, Migrations/), Repositories/, Messaging/, Grpc/ (клиенты), …
      ▼
<Сервис>.Application     Features/<Агрегат>/<Фича>/ (Command/Query + Handler + Validator), Abstractions/ (порты), Models/
      ▼
<Сервис>.Domain          <Агрегат>/ — сущности, value objects, инварианты, доменные сервисы; без зависимостей
```

```
src/
  Identity/      Domain/Users  ·  Application/Features/{Auth,Users}  ·  Infrastructure/{Db,Security,Seeding}
  Datasets/      Domain/{Cameras,Frames,Snapshots}  ·  Application/Features/{Cameras,Frames,Snapshots}
                 Infrastructure/{Db,Storage,Scheduling,Messaging}  ·  Service/Grpc/SnapshotQueryGrpcService
  Labeling/      Domain/{Consensus,Teachers,Runs,Predictions,Labels,Reviews}
                 Application/Features/{Teachers,Runs,Reviews}  ·  Infrastructure/{Db,Grpc,Messaging}
                 Service/Grpc/TrainingSetQueryGrpcService
  Training/      Domain/{Cycles,Jobs,Models,Evaluations}  ·  Application/{Features/{Cycles,Jobs,Models},Sagas}
                 Infrastructure/{Db,Grpc,Caching,Settings,Messaging}
  Shared/
    Quorum.Domain/        Entity, DomainException
    Quorum.Application/   IUnitOfWork, IIntegrationEventPublisher, исключения, ValidationBehavior, Contracts/, Protos/
    Quorum.Core/          JWT, Swagger/OpenAPI, ProblemDetails, Postgres + миграции, MassTransit + outbox/inbox
  Gateway/Quorum.Gateway/ YARP + общий Swagger UI
  Worker/                 Python gRPC-воркер (заглушка)
tests/
  Unit/         Identity/ Datasets/ Labeling/ Training/   — внутри Domain/, Features/
  Integration/  Datasets/ (API) · Labeling/ (Db)            — Testcontainers
scripts/        demo.sh, add-migration.sh
```

Правила зависимостей (проверены скриптом при генерации):
- `Domain` → только `Quorum.Domain`; никаких EF, MassTransit, MediatR, ASP.NET, gRPC.
- `Application` → свой `Domain` + `Quorum.Application`; не знает про EF, MassTransit, gRPC, `Quorum.Core`.
- `Infrastructure` → свой `Application` + `Quorum.Core`; `Service` → свой `Infrastructure` + `Quorum.Core`.
- Сервисы не ссылаются друг на друга.

## 9. Стек и лицензии

| Назначение | Библиотека | Лицензия |
|---|---|---|
| Платформа | .NET 10, ASP.NET Core Minimal API | MIT |
| ORM | EF Core 10 + Npgsql | MIT / PostgreSQL |
| Swagger UI | Swashbuckle.AspNetCore.SwaggerUI (документ — Microsoft.AspNetCore.OpenApi) | MIT |
| Шина, outbox/inbox | MassTransit **8.x** | Apache 2.0 (v9 — коммерческая) |
| CQRS-диспетчер | MediatR **12.x** | Apache 2.0 |
| Валидация | FluentValidation | Apache 2.0 |
| gRPC | Grpc.AspNetCore / Grpc.Net.Client; grpcio (Python) | Apache 2.0 |
| Кеш | Microsoft.Extensions.Caching.Hybrid + StackExchangeRedis | MIT |
| Gateway | YARP | MIT |
| Тесты | xUnit, NSubstitute, Testcontainers, WebApplicationFactory | Apache 2.0 / BSD / MIT |

---

## 10. Запуск

```bash
docker compose up -d --build
# gateway:      http://localhost:8000
# Swagger UI:   http://localhost:8000/swagger    (выпадающий список — 4 сервиса)
# RabbitMQ UI:  http://localhost:15672           (quorum / quorum)
# PostgreSQL:   localhost:5432                   (postgres / postgres)
./scripts/demo.sh                 # нужен jq
DEMO_FAILURE=1 ./scripts/demo.sh  # + сценарий с компенсацией (останавливает воркер через docker compose)
```

**Базы.** Один контейнер Postgres, у каждого сервиса своя база и свой `DbContext` (`<Сервис>.Infrastructure/Db`); базы создаются при первом старте.

| БД | Таблицы |
|---|---|
| `identity` | `users` |
| `datasets` | `cameras`, `frames`, `snapshots`, `snapshot_frames` |
| `labeling` | `teachers`, `runs`, `run_teachers`, `predictions`, `labels`, `review_tasks` |
| `training` | `cycles`, `training_jobs`, `model_versions`, `evaluations` |

**Кадры** хранятся в docker-томе `frames` (`Storage:FramesRoot`); воркер монтирует тот же том только на чтение.

**Swagger.** Кнопка **Authorize** принимает JWT из `POST /identity/auth/login`.

Маршруты gateway: `/{identity|datasets|labeling|training}/...` → сервис (префикс срезается).

### Основные эндпоинты

| Сервис | Метод и путь (без префикса gateway) | Роль |
|---|---|---|
| Identity | `POST /auth/register`, `POST /auth/login`, `GET /auth/me` | — / — / любая |
| Identity | `GET /users`, `POST /users/{id}/roles`, `DELETE /users/{id}/roles/{role}` | Admin |
| Datasets | `GET /cameras`, `GET /cameras/{id}` (с числом новых кадров) | любая |
| Datasets | `POST /cameras`, `PATCH /cameras/{id}` (запрос, расписание, активность) | Operator |
| Datasets | `POST /cameras/{id}/frames` (multipart, поле `file`) | Operator |
| Datasets | `GET /cameras/{id}/frames`, `GET /frames/{id}/content`, `GET /snapshots/{id}[/frames]` | любая |
| Labeling | `GET /teachers`, `POST /teachers`, `PATCH /teachers/{id}` | любая / Operator / Operator |
| Labeling | `GET /runs/{id}`, `GET /runs?cycleId=`, `GET /runs/{id}/labels?status=`, `GET /runs/{id}/frames/{frameId}/predictions` | любая |
| Labeling | `POST /reviews/next` (200 / 204), `GET /reviews/{id}`, `POST /reviews/{id}` (`Approve` / `Correct` / `Reject`) | Annotator |
| Training | `POST /cycles` (202), `GET /cycles?cameraId=`, `GET /cycles/{id}` (с оценкой) | Operator / любая |
| Training | `GET /cameras/{id}/model` (Redis), `GET /cameras/{id}/models`, `POST /models/{id}/rollback` | любая / любая / Operator |
| Training | `GET /jobs/{id}` (прогресс обучения) | любая |

### Демо-сценарий (`scripts/demo.sh`)
1. Администратор создаёт камеру (`Detection`, «license plate», от 30 кадров), регистрирует трёх учителей (GroundingDINO, YOLO-World, OWLv2), загружает 40 кадров; разметчик регистрируется сам.
2. Цикл 1, `Hybrid` 0.7: учителя размечают, примерно четверть кадров уходит разметчику, он проверяет → обучение → v1 публикуется.
3. Ещё 40 кадров, цикл 2, `Auto`: без человека; кандидат v2 сравнивается с v1 на новых кадрах.
4. Новых кадров нет → цикл `Skipped`.
5. `DEMO_FAILURE=1`: воркер остановлен → разметка падает → цикл `Failed`, снимок отброшен, у камеры снова 35 новых кадров.

Поведение заглушки настраивается переменными: `STUB_FRAME_DELAY_MS`, `STUB_EPOCH_DELAY_MS`, `STUB_FAIL_TRAINING=1` (падает обучение → компенсация обоих шагов). См. `src/Worker/README.md`.

## 11. Тесты

```bash
dotnet test Quorum.slnx --filter "Category!=Integration"   # unit, без Docker
dotnet test Quorum.slnx --filter "Category=Integration"    # нужен Docker
```

**Unit:**
- `Identity` — email, роли, регистрация.
- `Datasets` — расписание камеры, разбиение снимка, возврат кадров при отбрасывании, отказ при нехватке кадров и занятой камере.
- `Labeling` — консенсус боксов и текста (кворум, веса, двойной голос, нормализация кириллицы), стратегии валидации, жизненный цикл прогона и заданий, обработка прогона с фейковым воркером (принятие, отправка спорных на проверку, падение воркера).
- `Training` — переходы цикла и набор компенсаций, правило публикации, публикация с архивированием и откат, хендлеры саги (мало меток → компенсации, падение обучения → компенсации).

**Интеграционные** (Testcontainers):
- `Integration/Datasets` — настоящий Datasets.Service поверх Postgres + RabbitMQ: 401/403 по ролям, валидация, загрузка кадров и скачивание, неактивная камера, OpenAPI.
- `Integration/Labeling` — маппинг на настоящем Postgres: боксы в jsonb, owned-стратегия, учителя прогона, уникальность «учитель × кадр», конфликт конкурентных проверок (`xmin`).

