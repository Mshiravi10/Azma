using Azma.Identity.Api.Application.Auth;
using Azma.Identity.Api.Domain;
using Azma.Identity.Api.Infrastructure.Persistence;
using Azma.Identity.Api.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace Azma.Identity.Tests;

public sealed class McpTokenHandlersTests
{
    private readonly IPasswordHasher _passwordHasher = new TestPasswordHasher();
    private readonly ITokenHasher _tokenHasher = new Sha256TokenHasher();

    [Fact]
    public async Task CreateMcpToken_WithValidPassword_ReturnsRawTokenAndStoresOnlyHash()
    {
        await using var db = CreateDb();
        var seed = await SeedAsync(db);
        var handler = new CreateMcpTokenHandler(db, _passwordHasher, _tokenHasher);

        var result = await handler.Handle(
            new CreateMcpTokenCommand(seed.UserId, seed.WorkspaceId, "Cursor", "correct-password", 30),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Token.Should().StartWith(McpToken.RawTokenPrefix);

        var stored = await db.McpTokens.SingleAsync();
        stored.TokenHash.Should().Be(_tokenHasher.Hash(result.Value.Token));
        stored.TokenHash.Should().NotBe(result.Value.Token);
        stored.Name.Should().Be("Cursor");
    }

    [Fact]
    public async Task CreateMcpToken_WithWrongPassword_FailsAndDoesNotStoreToken()
    {
        await using var db = CreateDb();
        var seed = await SeedAsync(db);
        var handler = new CreateMcpTokenHandler(db, _passwordHasher, _tokenHasher);

        var result = await handler.Handle(
            new CreateMcpTokenCommand(seed.UserId, seed.WorkspaceId, "Cursor", "wrong-password", 30),
            CancellationToken.None);

        result.IsFailure.Should().BeTrue();
        result.Error.Code.Should().Be("identity.invalid_password");
        (await db.McpTokens.CountAsync()).Should().Be(0);
    }

    [Fact]
    public async Task ValidateMcpToken_WithActiveToken_ReturnsPrincipalAndMarksLastUsed()
    {
        await using var db = CreateDb();
        var seed = await SeedAsync(db);
        var create = new CreateMcpTokenHandler(db, _passwordHasher, _tokenHasher);
        var created = await create.Handle(
            new CreateMcpTokenCommand(seed.UserId, seed.WorkspaceId, "Cursor", "correct-password", 30),
            CancellationToken.None);
        var handler = new ValidateMcpTokenHandler(db, _tokenHasher);

        var result = await handler.Handle(
            new ValidateMcpTokenCommand(created.Value.Token),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.UserId.Should().Be(seed.UserId);
        result.Value.WorkspaceId.Should().Be(seed.WorkspaceId);
        result.Value.Role.Should().Be(WorkspaceRole.Admin.ToString());
        result.Value.AccessScope.Should().Be(MembershipAccessScope.Workspace.ToString());
        (await db.McpTokens.SingleAsync()).LastUsedAtUtc.Should().NotBeNull();
    }

    [Fact]
    public async Task ValidateMcpToken_WithRevokedExpiredOrUnknownToken_Fails()
    {
        await using var db = CreateDb();
        var seed = await SeedAsync(db);
        var revoked = McpToken.Issue(seed.UserId, seed.WorkspaceId, "Revoked", _tokenHasher.Hash("azma_mcp_revoked"), TimeSpan.FromDays(30));
        revoked.Revoke();
        var expired = McpToken.Issue(seed.UserId, seed.WorkspaceId, "Expired", _tokenHasher.Hash("azma_mcp_expired"), TimeSpan.FromDays(-1));
        db.McpTokens.AddRange(revoked, expired);
        await db.SaveChangesAsync();
        var handler = new ValidateMcpTokenHandler(db, _tokenHasher);

        var revokedResult = await handler.Handle(new ValidateMcpTokenCommand("azma_mcp_revoked"), CancellationToken.None);
        var expiredResult = await handler.Handle(new ValidateMcpTokenCommand("azma_mcp_expired"), CancellationToken.None);
        var unknownResult = await handler.Handle(new ValidateMcpTokenCommand("azma_mcp_unknown"), CancellationToken.None);

        revokedResult.IsFailure.Should().BeTrue();
        expiredResult.IsFailure.Should().BeTrue();
        unknownResult.IsFailure.Should().BeTrue();
    }

    [Fact]
    public async Task ListMcpTokens_ReturnsOnlyCurrentWorkspaceTokens()
    {
        await using var db = CreateDb();
        var seed = await SeedAsync(db);
        var otherWorkspace = Workspace.Create("Other", "other", seed.UserId);
        db.Workspaces.Add(otherWorkspace);
        db.Memberships.Add(Membership.Create(seed.UserId, otherWorkspace.Id, WorkspaceRole.Tester));
        db.McpTokens.Add(McpToken.Issue(seed.UserId, seed.WorkspaceId, "Current", _tokenHasher.Hash("azma_mcp_current"), TimeSpan.FromDays(30)));
        db.McpTokens.Add(McpToken.Issue(seed.UserId, otherWorkspace.Id, "Other", _tokenHasher.Hash("azma_mcp_other"), TimeSpan.FromDays(30)));
        await db.SaveChangesAsync();
        var handler = new ListMcpTokensHandler(db);

        var result = await handler.Handle(
            new ListMcpTokensQuery(seed.UserId, seed.WorkspaceId),
            CancellationToken.None);

        result.IsSuccess.Should().BeTrue();
        result.Value.Should().ContainSingle(x => x.Name == "Current");
    }

    private IdentityDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<IdentityDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        return new IdentityDbContext(options);
    }

    private async Task<SeedIds> SeedAsync(IdentityDbContext db)
    {
        var user = User.Create("user@azma.local", _passwordHasher.Hash("correct-password"), "Azma User");
        var workspace = Workspace.Create("Workspace", "workspace", user.Id);
        db.Users.Add(user);
        db.Workspaces.Add(workspace);
        db.Memberships.Add(Membership.Create(user.Id, workspace.Id, WorkspaceRole.Admin));
        await db.SaveChangesAsync();
        return new SeedIds(user.Id, workspace.Id);
    }

    private sealed record SeedIds(Guid UserId, Guid WorkspaceId);

    private sealed class TestPasswordHasher : IPasswordHasher
    {
        public string Hash(string password) => "hash:" + password;

        public bool Verify(string password, string hash) => hash == Hash(password);
    }
}
