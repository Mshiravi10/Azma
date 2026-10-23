namespace Azma.Auth.ProjectAccess;

/// <summary>
/// Minimum capability required for a project operation.
/// Effective role is resolved from a project override, otherwise the workspace role.
/// </summary>
public enum ProjectCapability
{
    View = 1,
    Contribute = 2,
    Manage = 3,
    Own = 4,
}
