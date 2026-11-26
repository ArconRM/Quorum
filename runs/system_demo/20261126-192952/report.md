# Сквозной прогон Quorum на кадрах камеры
2026-11-26 19:29:52 Gateway http://localhost:8000, кадры datasets/camera, лимит на день: 400 (0 — все)
2026-11-26 19:29:52 == Вход: администратор и разметчик
2026-11-26 19:29:52 == Камера и учителя
2026-11-26 19:29:53    камера e9fc1c72-61d1-4e7c-ad33-f74bc416cf98
2026-11-26 19:29:53    учитель owlv2-large-6205
2026-11-26 19:29:53    учитель llmdet-large-6205
2026-11-26 19:29:53    учитель mm-grounding-dino-large-6205
2026-11-26 19:29:53 == Цикл 1: дни 2025-11-09,2025-11-15, режим Hybrid, порог 0.85
2026-11-26 19:29:56    залито 2025-11-09: 101 кадров
2026-11-26 19:30:13    залито 2025-11-15: 400 кадров
2026-11-26 19:30:13    заливка: 501 кадров за 20 с
2026-11-26 19:30:13    цикл 52e68a99-7315-4ff1-9359-1314b2a6ed0b
2026-11-26 19:30:13    → Snapshotting
2026-11-26 19:30:18    Snapshotting: 5 с
2026-11-26 19:30:18    → Labeling
2026-11-26 19:39:35    Labeling: 557 с
2026-11-26 19:39:35    → Training
2026-11-26 19:41:40    Training: 125 с
2026-11-26 19:41:40    → Evaluating
2026-11-26 19:41:45    Evaluating: 5 с
2026-11-26 19:41:45    → Completed
2026-11-26 19:41:45    итого цикл 1: 692 с, разметчик проверил кадров: 192
2026-11-26 19:41:45    {"status":"Completed","outcome":"Promoted","trainFrames":401,"holdoutFrames":100,"acceptedLabels":501,"failureReason":null,"evaluation":{"metricName":"mAP50","candidateScore":0.9917,"baselineScore":null,"holdoutSize":100,"decision":"Promote"}}
2026-11-26 19:41:45    разметка: {"status":"Completed","totalFrames":501,"accepted":501,"rejected":0,"reviewed":192}
2026-11-26 19:41:45 == Цикл 2: дни 2025-11-16, режим Auto, порог 0.85
2026-11-26 19:41:59    залито 2025-11-16: 400 кадров
2026-11-26 19:41:59    заливка: 400 кадров за 14 с
2026-11-26 19:41:59    цикл 67b91b0c-2610-4edc-9598-9a9e23adb790
2026-11-26 19:41:59    → Snapshotting
2026-11-26 19:42:04    Snapshotting: 5 с
2026-11-26 19:42:04    → Labeling
2026-11-26 19:49:11    Labeling: 427 с
2026-11-26 19:49:11    → Training
2026-11-26 19:50:41    Training: 90 с
2026-11-26 19:50:41    → Evaluating
2026-11-26 19:50:46    Evaluating: 5 с
2026-11-26 19:50:46    → Completed
2026-11-26 19:50:46    итого цикл 2: 527 с, разметчик проверил кадров: 0
2026-11-26 19:50:46    {"status":"Completed","outcome":"Promoted","trainFrames":320,"holdoutFrames":80,"acceptedLabels":326,"failureReason":null,"evaluation":{"metricName":"mAP50","candidateScore":0.995,"baselineScore":0.9888,"holdoutSize":65,"decision":"Promote"}}
2026-11-26 19:50:46    разметка: {"status":"Completed","totalFrames":400,"accepted":326,"rejected":74,"reviewed":0}
2026-11-26 19:50:46 == Цикл 3: дни 2025-11-29,2025-11-30, режим Auto, порог 0.85
2026-11-26 19:50:51    залито 2025-11-29: 128 кадров
2026-11-26 19:51:02    залито 2025-11-30: 400 кадров
2026-11-26 19:51:02    заливка: 528 кадров за 16 с
2026-11-26 19:51:02    цикл 90f02ffa-df52-4772-9841-53591d77fe90
2026-11-26 19:51:02    → Snapshotting
2026-11-26 19:51:07    Snapshotting: 5 с
2026-11-26 19:51:07    → Labeling
2026-11-26 19:59:19    Labeling: 492 с
2026-11-26 19:59:19    → Training
2026-11-26 20:00:14    Training: 55 с
2026-11-26 20:00:14    → Evaluating
2026-11-26 20:00:19    Evaluating: 5 с
2026-11-26 20:00:19    → Completed
2026-11-26 20:00:19    итого цикл 3: 557 с, разметчик проверил кадров: 0
2026-11-26 20:00:19    {"status":"Completed","outcome":"Promoted","trainFrames":423,"holdoutFrames":105,"acceptedLabels":373,"failureReason":null,"evaluation":{"metricName":"mAP50","candidateScore":0.995,"baselineScore":0.9744,"holdoutSize":78,"decision":"Promote"}}
2026-11-26 20:00:19    разметка: {"status":"Completed","totalFrames":528,"accepted":373,"rejected":155,"reviewed":0}
2026-11-26 20:00:19 == Версии модели камеры
{"number":3,"status":"Published","holdoutMetric":0.995}
{"number":2,"status":"Archived","holdoutMetric":0.995}
{"number":1,"status":"Archived","holdoutMetric":0.9917}
2026-11-26 20:00:19 Отчёт: runs/system_demo/20261126-192952
