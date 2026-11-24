# Сквозной прогон Quorum на кадрах камеры
2026-11-24 20:41:22 Gateway http://localhost:8000, кадры datasets/camera, лимит на день: 50 (0 — все)
2026-11-24 20:41:22 == Вход: администратор и разметчик
2026-11-24 20:41:22 == Камера и учителя
2026-11-24 20:41:22    камера 5a216680-1fa5-4038-878e-600e19df2719
2026-11-24 20:41:22    учитель owlv2-large-17667
2026-11-24 20:41:22    учитель llmdet-large-17667
2026-11-24 20:41:22    учитель mm-grounding-dino-large-17667
2026-11-24 20:41:22 == Цикл 1: дни 2025-11-09,2025-11-15, режим Hybrid, порог 0.85
2026-11-24 20:41:24    залито 2025-11-09: 50 кадров
2026-11-24 20:41:25    залито 2025-11-15: 50 кадров
2026-11-24 20:41:25    заливка: 100 кадров за 3 с
2026-11-24 20:41:25    цикл 034de2b2-7b5a-480c-9eef-05252859ca2e
2026-11-24 20:41:25    → Snapshotting
2026-11-24 20:41:30    Snapshotting: 5 с
2026-11-24 20:41:30    → Labeling
2026-11-24 20:50:44    Labeling: 554 с
2026-11-24 20:50:44    → Training
2026-11-24 20:51:10    Training: 26 с
2026-11-24 20:51:10    → Evaluating
2026-11-24 20:51:15    Evaluating: 5 с
2026-11-24 20:51:15    → Completed
2026-11-24 20:51:15    итого цикл 1: 590 с, разметчик проверил кадров: 87
2026-11-24 20:51:15    {"status":"Completed","outcome":"Promoted","trainFrames":80,"holdoutFrames":20,"acceptedLabels":100,"failureReason":null,"evaluation":{"metricName":"mAP50","candidateScore":0.8352,"baselineScore":null,"holdoutSize":20,"decision":"Promote"}}
2026-11-24 20:51:15    разметка: {"status":"Completed","totalFrames":100,"accepted":100,"rejected":0,"reviewed":87}
2026-11-24 20:51:15 == Цикл 2: дни 2025-11-16, режим Auto, порог 0.85
2026-11-24 20:51:16    залито 2025-11-16: 50 кадров
2026-11-24 20:51:16    заливка: 50 кадров за 1 с
2026-11-24 20:51:16    цикл 510978bb-0023-42a6-9eea-2063c9ad2680
2026-11-24 20:51:16    → Snapshotting
2026-11-24 20:51:21    Snapshotting: 5 с
2026-11-24 20:51:21    → Labeling
2026-11-24 20:55:57    Labeling: 276 с
2026-11-24 20:55:57    → Training
2026-11-24 20:56:07    Training: 10 с
2026-11-24 20:56:07    → Completed
2026-11-24 20:56:07    итого цикл 2: 291 с, разметчик проверил кадров: 0
2026-11-24 20:56:07    {"status":"Completed","outcome":"Promoted","trainFrames":40,"holdoutFrames":10,"acceptedLabels":12,"failureReason":null,"evaluation":{"metricName":"mAP50","candidateScore":0,"baselineScore":0,"holdoutSize":1,"decision":"Promote"}}
2026-11-24 20:56:07    разметка: {"status":"Completed","totalFrames":50,"accepted":12,"rejected":38,"reviewed":0}
2026-11-24 20:56:07 == Цикл 3: дни 2025-11-29,2025-11-30, режим Auto, порог 0.85
2026-11-24 20:56:09    залито 2025-11-29: 50 кадров
2026-11-24 20:56:09    залито 2025-11-30: 50 кадров
2026-11-24 20:56:09    заливка: 100 кадров за 2 с
2026-11-24 20:56:09    цикл 77916c65-54ea-4a8f-bfbf-b6e69fb860fd
2026-11-24 20:56:09    → Snapshotting
2026-11-24 20:56:14    Snapshotting: 5 с
2026-11-24 20:56:14    → Labeling
2026-11-24 21:03:31    Labeling: 437 с
2026-11-24 21:03:31    → Failed
2026-11-24 21:03:31    итого цикл 3: 442 с, разметчик проверил кадров: 0
2026-11-24 21:03:31    {"status":"Failed","outcome":null,"trainFrames":80,"holdoutFrames":20,"acceptedLabels":2,"failureReason":"Too few accepted labels for training: 2 < 10","evaluation":null}
2026-11-24 21:03:31    разметка: {"status":"Discarded","totalFrames":100,"accepted":2,"rejected":98,"reviewed":0}
2026-11-24 21:03:31 == Версии модели камеры
{"number":2,"status":"Published","holdoutMetric":0}
{"number":1,"status":"Archived","holdoutMetric":0.8352}
2026-11-24 21:03:31 Отчёт: runs/system_demo/20261124-204122
