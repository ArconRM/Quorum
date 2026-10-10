using Quorum.Domain;

namespace Training.Domain.Models;

/// <summary>
/// Доменный сервис: инвариант «на камере опубликована ровно одна версия».
/// Публикация новой версии (или откат к архивной) переводит текущую в архив.
/// </summary>
public static class ModelPublication
{
    public static void Publish(ModelVersion next, ModelVersion? current)
    {
        if (current is not null)
        {
            if (current.Id == next.Id) throw new DomainException("Version is already published");
            if (current.CameraId != next.CameraId) throw new DomainException("Versions belong to different cameras");
            current.Archive();
        }
        next.Publish();
    }

    /// <summary>Откат: вернуть на камеру ранее опубликованную (архивную) версию.</summary>
    public static void Restore(ModelVersion archived, ModelVersion? current)
    {
        if (archived.Status != ModelVersionStatus.Archived)
            throw new DomainException($"Only archived versions can be restored, this one is {archived.Status}");
        Publish(archived, current);
    }
}
