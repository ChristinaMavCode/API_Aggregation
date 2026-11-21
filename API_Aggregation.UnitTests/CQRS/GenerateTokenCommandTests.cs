using API_Aggregation.Application.Abstractions;
using API_Aggregation.Application.CQRS.Commands.GenerateToken;
using API_Aggregation.Application.Dtos;
using Moq;

namespace API_Aggregation.UnitTests.CQRS;

public class GenerateTokenCommandTests
{
    private readonly Mock<IUserCredentialValidator> _credentials = new();
    private readonly Mock<ITokenService> _tokens = new();

    [Fact]
    public async Task Handle_ReturnsToken_ForValidCredentials()
    {
        var token = new TokenResponseDto("jwt", DateTimeOffset.UtcNow);
        _credentials.Setup(c => c.IsValid("demo", "pwd")).Returns(true);
        _tokens.Setup(t => t.CreateToken("demo")).Returns(token);

        var result = await new GenerateTokenCommandHandler(_credentials.Object, _tokens.Object)
            .Handle(new GenerateTokenCommand("demo", "pwd"), CancellationToken.None);

        Assert.Same(token, result);
    }

    [Fact]
    public async Task Handle_ReturnsNull_AndIssuesNoToken_ForInvalidCredentials()
    {
        _credentials.Setup(c => c.IsValid(It.IsAny<string>(), It.IsAny<string>())).Returns(false);

        var result = await new GenerateTokenCommandHandler(_credentials.Object, _tokens.Object)
            .Handle(new GenerateTokenCommand("demo", "wrong"), CancellationToken.None);

        Assert.Null(result);
        _tokens.Verify(t => t.CreateToken(It.IsAny<string>()), Times.Never);
    }

    [Theory]
    [InlineData(null, "pwd")]
    [InlineData("demo", null)]
    [InlineData("", "")]
    public void Validator_Fails_ForMissingCredentials(string? username, string? password) =>
        Assert.False(new GenerateTokenCommandValidator().Validate(new GenerateTokenCommand(username, password)).IsValid);
}
