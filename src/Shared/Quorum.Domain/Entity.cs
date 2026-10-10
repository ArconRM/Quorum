namespace Quorum.Domain;

/// <summary>Базовый класс сущности/корня агрегата.</summary>
public abstract class Entity
{
    protected Entity() { }
    protected Entity(Guid id) => Id = id;

    public Guid Id { get; protected set; }
}
