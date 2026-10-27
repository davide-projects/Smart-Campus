using SmartCampus.Identity.Api.Domain;

namespace SmartCampus.Identity.UnitTests;

public class UserTests
{
    [Fact]
    public void Constructor_ValidData_TrimsNames()
    {
        var user = new User(Guid.NewGuid(), "  student1 ", " Mario Rossi ", "hash", UserRole.Student);

        Assert.Equal("student1", user.Username);
        Assert.Equal("Mario Rossi", user.DisplayName);
        Assert.Equal(UserRole.Student, user.Role);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_EmptyUsername_Throws(string username)
    {
        Assert.Throws<ArgumentException>(() =>
            new User(Guid.NewGuid(), username, "Mario Rossi", "hash", UserRole.Student));
    }

    [Fact]
    public void Constructor_EmptyId_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new User(Guid.Empty, "student1", "Mario Rossi", "hash", UserRole.Student));
    }

    [Fact]
    public void Constructor_EmptyPasswordHash_Throws()
    {
        Assert.Throws<ArgumentException>(() =>
            new User(Guid.NewGuid(), "student1", "Mario Rossi", " ", UserRole.Student));
    }

    [Fact]
    public void Constructor_UndefinedRole_Throws()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() =>
            new User(Guid.NewGuid(), "student1", "Mario Rossi", "hash", (UserRole)99));
    }
}
