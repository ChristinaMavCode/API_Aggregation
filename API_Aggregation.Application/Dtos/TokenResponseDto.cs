namespace API_Aggregation.Application.Dtos;

public sealed record TokenResponseDto(string AccessToken, DateTimeOffset ExpiresAt)
{
    public string TokenType => "Bearer";
}
