# Сквозной прогон Quorum на кадрах камеры
2026-11-24 22:29:26 Gateway http://localhost:8000, кадры datasets/camera, лимит на день: 50 (0 — все)
2026-11-24 22:29:26 == Вход: администратор и разметчик
2026-11-24 22:29:26 == Камера и учителя
2026-11-24 22:29:27    камера ba7fdedd-e84d-4fc0-a5ca-f96aec97f151
2026-11-24 22:29:27    учитель owlv2-large-4646
2026-11-24 22:29:27    учитель llmdet-large-4646
2026-11-24 22:29:27    учитель mm-grounding-dino-large-4646
2026-11-24 22:29:27 == Цикл 1: дни 2025-11-09,2025-11-15, режим Hybrid, порог 0.85
2026-11-24 22:29:28    залито 2025-11-09: 50 кадров
2026-11-24 22:29:30    залито 2025-11-15: 50 кадров
2026-11-24 22:29:30    заливка: 100 кадров за 3 с
2026-11-24 22:29:30    цикл 0e91e9eb-784f-4876-b52b-643a78ca3520
2026-11-24 22:29:30    → Snapshotting
2026-11-24 22:29:35    Snapshotting: 5 с
2026-11-24 22:29:35    → Labeling
2026-11-24 22:31:37    Labeling: 122 с
2026-11-24 22:31:37    → Training
2026-11-24 22:32:07    Training: 30 с
2026-11-24 22:32:07    → Completed
2026-11-24 22:32:07    итого цикл 1: 157 с, разметчик проверил кадров: 47
2026-11-24 22:32:07    {"status":"Completed","outcome":"Promoted","trainFrames":80,"holdoutFrames":20,"acceptedLabels":100,"failureReason":null,"evaluation":{"metricName":"mAP50","candidateScore":0.9469,"baselineScore":null,"holdoutSize":20,"decision":"Promote"}}
2026-11-24 22:32:07    разметка: {"status":"Completed","totalFrames":100,"accepted":100,"rejected":0,"reviewed":47}
2026-11-24 22:32:07 == Цикл 2: дни 2025-11-16, режим Auto, порог 0.85
2026-11-24 22:32:08    залито 2025-11-16: 50 кадров
2026-11-24 22:32:08    заливка: 50 кадров за 1 с
2026-11-24 22:32:08    цикл ed0bffff-c784-403f-9ae8-ccf1463835c1
2026-11-24 22:32:08    → Snapshotting
2026-11-24 22:32:13    Snapshotting: 5 с
2026-11-24 22:32:13    → Labeling
2026-11-24 22:33:13    Labeling: 60 с
2026-11-24 22:33:13    → Training
2026-11-24 22:33:28    Training: 15 с
2026-11-24 22:33:28    → Completed
2026-11-24 22:33:28    итого цикл 2: 80 с, разметчик проверил кадров: 0
2026-11-24 22:33:28    {"status":"Completed","outcome":"Promoted","trainFrames":40,"holdoutFrames":10,"acceptedLabels":29,"failureReason":null,"evaluation":{"metricName":"mAP50","candidateScore":0.9639,"baselineScore":0.9639,"holdoutSize":6,"decision":"Promote"}}
2026-11-24 22:33:28    разметка: {"status":"Completed","totalFrames":50,"accepted":29,"rejected":21,"reviewed":0}
2026-11-24 22:33:28 == Цикл 3: дни 2025-11-29,2025-11-30, режим Auto, порог 0.85
2026-11-24 22:33:30    залито 2025-11-29: 50 кадров
2026-11-24 22:33:31    залито 2025-11-30: 50 кадров
2026-11-24 22:33:31    заливка: 100 кадров за 3 с
2026-11-24 22:33:31    цикл fdce0c4a-fc1e-4f5b-8de1-04d95f615cd0
2026-11-24 22:33:31    → Snapshotting
2026-11-24 22:33:36    Snapshotting: 5 с
2026-11-24 22:33:36    → Labeling
2026-11-24 22:35:16    Labeling: 100 с
2026-11-24 22:35:16    → Training
2026-11-24 22:35:41    Training: 25 с
2026-11-24 22:35:41    → Evaluating
2026-11-24 22:35:46    Evaluating: 5 с
2026-11-24 22:35:46    → Completed
2026-11-24 22:35:46    итого цикл 3: 135 с, разметчик проверил кадров: 0
2026-11-24 22:35:46    {"status":"Completed","outcome":"Promoted","trainFrames":80,"holdoutFrames":20,"acceptedLabels":72,"failureReason":null,"evaluation":{"metricName":"mAP50","candidateScore":0.995,"baselineScore":0.995,"holdoutSize":12,"decision":"Promote"}}
2026-11-24 22:35:46    разметка: {"status":"Completed","totalFrames":100,"accepted":72,"rejected":28,"reviewed":0}
2026-11-24 22:35:46 == Версии модели камеры
{"number":3,"status":"Published","holdoutMetric":0.995}
{"number":2,"status":"Archived","holdoutMetric":0.9639}
{"number":1,"status":"Archived","holdoutMetric":0.9469}
2026-11-24 22:35:46 Отчёт: runs/system_demo/20261124-222926
