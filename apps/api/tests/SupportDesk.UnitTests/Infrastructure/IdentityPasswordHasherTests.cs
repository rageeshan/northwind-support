using SupportDesk.Infrastructure.Services;

namespace SupportDesk.UnitTests.Infrastructure;

public class IdentityPasswordHasherTests
{
    private readonly IdentityPasswordHasher _sut = new();

    [Fact]
    public void Verify_passes_for_the_original_password_and_fails_for_any_other()
    {
        var hash = _sut.Hash("Local_Dev_Password1!");

        Assert.True(_sut.Verify("Local_Dev_Password1!", hash));
        Assert.False(_sut.Verify("wrong-password", hash));
        Assert.False(_sut.Verify("Local_Dev_Password1!", "not-a-hash"));
        Assert.False(_sut.Verify("Local_Dev_Password1!", null));
    }
}
