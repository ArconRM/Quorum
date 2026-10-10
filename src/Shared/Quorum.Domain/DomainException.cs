namespace Quorum.Domain;

/// <summary>Нарушение бизнес-правила (инварианта) доменной модели.</summary>
public class DomainException(string message) : Exception(message);
