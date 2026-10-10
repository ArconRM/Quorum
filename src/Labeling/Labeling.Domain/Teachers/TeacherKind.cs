namespace Labeling.Domain.Teachers;

public enum TeacherKind
{
    /// <summary>Детектор с открытым словарём по текстовому запросу (GroundingDINO, YOLO-World, OWLv2).</summary>
    OpenVocabDetector,

    /// <summary>Обычный детектор с фиксированными классами (YOLOv8l, Faster R-CNN).</summary>
    Detector,

    /// <summary>Мультимодальная языковая модель (Qwen-VL, Gemma). Только для текста: боксы у VLM слишком грубые.</summary>
    Vlm,
}
