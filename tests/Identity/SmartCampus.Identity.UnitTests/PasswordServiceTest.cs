using SmartCampus.Identity.Api.Auth;

namespace SmartCampus.Identity.UnitTests;

public class PasswordServiceTests
{
    private readonly PasswordService _service = new();

    [Fact]
    public void Hash_DoesNotContainThePlainPassword()
    {
        var hash = _service.Hash("Campus-Test-1!");

        Assert.DoesNotContain("Campus-Test-1!", hash);
    }

    [Fact]
    public void Hash_SamePasswordTwice_ProducesDifferentHashes()
    {
        Assert.NotEqual(_service.Hash("Campus-Test-1!"), _service.Hash("Campus-Test-1!"));
    }

    [Fact]
    public void Verify_CorrectPassword_ReturnsTrue()
    {
        var hash = _service.Hash("Campus-Test-1!");

        Assert.True(_service.Verify(hash, "Campus-Test-1!"));
    }

    [Fact]
    public void Verify_WrongPassword_ReturnsFalse()
    {
        var hash = _service.Hash("Campus-Test-1!");

        Assert.False(_service.Verify(hash, "altra-password"));
    }

    [Fact]
    public void SimulateVerification_DoesNotThrow()
    {
        var exception = Record.Exception(() => _service.SimulateVerification("qualsiasi"));

        Assert.Null(exception);
    }
}
