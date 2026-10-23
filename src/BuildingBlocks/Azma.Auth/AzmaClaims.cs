namespace Azma.Auth;

public static class AzmaClaims
{
    public const string UserId = "sub";
    public const string WorkspaceId = "workspace_id";
    public const string Email = "email";
    public const string DisplayName = "name";
    public const string Role = "role";
    public const string AccessScope = "access_scope";
}

public static class AzmaRoles
{
    public const string Owner = "Owner";
    public const string Admin = "Admin";
    public const string Tester = "Tester";
    public const string Viewer = "Viewer";
}

public static class AzmaAccessScopes
{
    public const string Workspace = "Workspace";
    public const string ProjectScoped = "ProjectScoped";
}
