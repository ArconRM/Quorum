namespace Identity.Domain.Users;

public static class Role
{
    public const string Admin = "Admin";
    public const string Operator = "Operator";
    public const string Annotator = "Annotator";

    public static readonly IReadOnlySet<string> All = new HashSet<string> { Admin, Operator, Annotator };
}
