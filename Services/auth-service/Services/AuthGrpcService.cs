using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Grpc.Core;
using Danetka.Contracts.Auth;
using Danetka.AuthService.Data;
using Danetka.AuthService.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
namespace Danetka.AuthService.Services;

public class AuthGrpcService(
    AuthDbContext dbContext,
    IConfiguration configuration,
    ILogger<AuthGrpcService> logger)
    : Danetka.Contracts.Auth.AuthService.AuthServiceBase
{
    public override async Task<AuthResponse> Register(RegisterRequest request, ServerCallContext context)
    {
        logger.LogInformation("Register attempt for email: {Email}", request.Email);

        if (string.IsNullOrWhiteSpace(request.Email) || 
            string.IsNullOrWhiteSpace(request.Username) || 
            string.IsNullOrWhiteSpace(request.Password))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Email, username, and password are required."));
        }

        if (request.Password.Length < 8)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Password must be at least 8 characters long."));
        }

        var normalizedEmail = request.Email.ToLowerInvariant().Trim();

        var emailExists = await dbContext.Users
            .AnyAsync(u => u.Email == normalizedEmail, context.CancellationToken);

        if (emailExists)
        {
            logger.LogWarning("Registration failed: Email {Email} is already taken.", normalizedEmail);
            throw new RpcException(new Status(StatusCode.AlreadyExists, "Email is already registered."));
        }

        var passwordHash = BCrypt.Net.BCrypt.HashPassword(request.Password);
        
        var user = new User
        {
            Email = normalizedEmail,
            Username = request.Username.Trim(),
            PasswordHash = passwordHash,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        dbContext.Users.Add(user);
        await dbContext.SaveChangesAsync(context.CancellationToken);

        logger.LogInformation("Successfully registered user: {UserId}", user.Id);

        var (token, expiresAt) = GenerateJwtToken(user);

        return new AuthResponse
        {
            UserId = user.Id.ToString(),
            Token = token,
            ExpiresAt = expiresAt
        };
    }

    public override async Task<AuthResponse> Login(LoginRequest request, ServerCallContext context)
    {
        logger.LogInformation("Login attempt for email: {Email}", request.Email);

        if (string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Email and password are required."));
        }

        var normalizedEmail = request.Email.ToLowerInvariant().Trim();

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Email == normalizedEmail, context.CancellationToken);

        if (user == null)
        {
            logger.LogWarning("Login failed: User not found for email {Email}", normalizedEmail);
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid email or password."));
        }

        var isPasswordValid = BCrypt.Net.BCrypt.Verify(request.Password, user.PasswordHash);
        
        if (!isPasswordValid)
        {
            logger.LogWarning("Login failed: Invalid password for email {Email}", normalizedEmail);
            throw new RpcException(new Status(StatusCode.Unauthenticated, "Invalid email or password."));
        }

        logger.LogInformation("Successfully logged in user: {UserId}", user.Id);

        var (token, expiresAt) = GenerateJwtToken(user);

        return new AuthResponse
        {
            UserId = user.Id.ToString(),
            Token = token,
            ExpiresAt = expiresAt
        };
    }

    public override Task<ValidateTokenResponse> ValidateToken(ValidateTokenRequest request, ServerCallContext context)
    {
        logger.LogInformation("ValidateToken method called");

        var response = new ValidateTokenResponse
        {
            Valid = false,
            UserId = string.Empty,
            Email = string.Empty,
            Role = string.Empty
        };

        if (string.IsNullOrWhiteSpace(request.Token))
        {
            return Task.FromResult(response);
        }

        var token = request.Token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            ? request.Token["Bearer ".Length..].Trim()
            : request.Token.Trim();

        var tokenHandler = new JwtSecurityTokenHandler();
        var secretKey = configuration["Jwt:Secret"] 
            ?? throw new InvalidOperationException("JWT Secret is not configured.");
        
        var validationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey)),
            
            ValidateIssuer = true,
            ValidIssuer = configuration["Jwt:Issuer"],
            
            ValidateAudience = true,
            ValidAudience = configuration["Jwt:Audience"],
            
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };

        try
        {
            var principal = tokenHandler.ValidateToken(token, validationParameters, out SecurityToken validatedToken);

            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value 
                         ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                         
            var email = principal.FindFirst(ClaimTypes.Email)?.Value 
                        ?? principal.FindFirst(JwtRegisteredClaimNames.Email)?.Value;

            var role = principal.FindFirst(ClaimTypes.Role)?.Value;

            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(email))
            {
                logger.LogWarning("Token is valid structurally, but missing required claims (sub or email).");
                return Task.FromResult(response);
            }

            response.Valid = true;
            response.UserId = userId;
            response.Email = email;
            response.Role = role ?? "User";
        }
        catch (SecurityTokenExpiredException)
        {
            logger.LogInformation("Token validation failed: Token has expired.");
        }
        catch (Exception ex)
        {
            logger.LogWarning("Token validation failed: {Message}", ex.Message);
        }

        return Task.FromResult(response);
    }

    public override async Task<UserResponse> GetUser(GetUserRequest request, ServerCallContext context)
    {
        logger.LogInformation("GetUser attempt for user_id: {UserId}", request.UserId);

        if (!Guid.TryParse(request.UserId, out var userGuid))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid user_id format. Must be UUID."));
        }

        var user = await dbContext.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userGuid, context.CancellationToken);

        if (user == null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "User not found."));
        }

        return MapToUserResponse(user);
    }

    public override async Task<ListUsersResponse> ListUsers(ListUsersRequest request, ServerCallContext context)
    {
        var users = await dbContext.Users
            .AsNoTracking()
            .OrderBy(u => u.CreatedAt)
            .ToListAsync(context.CancellationToken);

        var response = new ListUsersResponse();
        response.Users.AddRange(users.Select(MapToUserResponse));
        return response;
    }

    public override async Task<UserResponse> AddTokens(AddTokensRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userGuid))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid user_id format. Must be UUID."));
        }

        if (request.Amount <= 0)
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Amount must be greater than zero."));
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userGuid, context.CancellationToken);

        if (user == null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "User not found."));
        }

        user.Tokens += request.Amount;
        user.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(context.CancellationToken);

        return MapToUserResponse(user);
    }

    public override async Task<ConsumeTokenResponse> ConsumeToken(ConsumeTokenRequest request, ServerCallContext context)
    {
        if (!Guid.TryParse(request.UserId, out var userGuid))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "Invalid user_id format. Must be UUID."));
        }

        var user = await dbContext.Users
            .FirstOrDefaultAsync(u => u.Id == userGuid, context.CancellationToken);

        if (user == null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "User not found."));
        }

        if (user.Tokens <= 0)
        {
            return new ConsumeTokenResponse
            {
                Success = false,
                RemainingTokens = user.Tokens
            };
        }

        user.Tokens -= 1;
        user.UpdatedAt = DateTime.UtcNow;
        await dbContext.SaveChangesAsync(context.CancellationToken);

        return new ConsumeTokenResponse
        {
            Success = true,
            RemainingTokens = user.Tokens
        };
    }
    
    private static UserResponse MapToUserResponse(User user)
    {
        return new UserResponse
        {
            UserId = user.Id.ToString(),
            Email = user.Email,
            Username = user.Username,
            CreatedAt = ((DateTimeOffset)user.CreatedAt).ToUnixTimeSeconds(),
            Role = MapToProtoRole(user.Role),
            Tokens = user.Tokens,
            SubscriptionPlan = user.SubscriptionPlan
        };
    }
    
    private (string Token, long ExpiresAtUnix) GenerateJwtToken(User user)
    {
        var secretKey = configuration["Jwt:Secret"] 
                        ?? throw new InvalidOperationException("JWT Secret is not configured.");
        
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secretKey));
        var signingCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        
        var expirationHours = configuration.GetValue<int>("Jwt:ExpirationHours", 24);
        var expiryDate = DateTime.UtcNow.AddHours(expirationHours);

        var claims = new[]
        {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: configuration["Jwt:Issuer"],
            audience: configuration["Jwt:Audience"],
            claims: claims,
            expires: expiryDate,
            signingCredentials: signingCredentials
        );

        var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
        var expiresAtUnix = ((DateTimeOffset)expiryDate).ToUnixTimeSeconds();

        return (tokenString, expiresAtUnix);
    }

    private static Role MapToProtoRole(string role)
    {
        if (string.Equals(role, "Admin", StringComparison.OrdinalIgnoreCase))
        {
            return Role.Admin;
        }

        return Role.User;
    }
}