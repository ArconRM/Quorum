namespace Quorum.Core.Auth;

public static class Roles
{
    /// <summary>Всё, включая управление пользователями.</summary>
    public const string Admin = "Admin";

    /// <summary>Камеры, учителя, запуск циклов, публикация и откат моделей.</summary>
    public const string Operator = "Operator";

    /// <summary>Ручная проверка меток.</summary>
    public const string Annotator = "Annotator";
}
