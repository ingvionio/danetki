using Danetka.Contracts.Puzzle;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Distributed;
using PuzzleService.Data;
using PuzzleService.Models;
using StackExchange.Redis;

namespace PuzzleService.Services;

public class PuzzleGrpcService : Danetka.Contracts.Puzzle.PuzzleService.PuzzleServiceBase
{
    private const int PuzzleByIdCacheMinutes = 10;
    private const int ListPuzzlesCacheMinutes = 5;
    private const string ListCacheKeyPattern = "Puzzle_puzzles_page_*";

    private readonly PuzzleDbContext _dbContext;
    private readonly IDistributedCache _cache;
    private readonly IConnectionMultiplexer _redis;
    private readonly ILogger<PuzzleGrpcService> _logger;

    public PuzzleGrpcService(
        PuzzleDbContext dbContext,
        IDistributedCache cache,
        IConnectionMultiplexer redis,
        ILogger<PuzzleGrpcService> logger)
    {
        _dbContext = dbContext;
        _cache = cache;
        _redis = redis;
        _logger = logger;
    }

    public override async Task<SavePuzzleResponse> SavePuzzle(SavePuzzleRequest request, ServerCallContext context)
    {
        var entity = new PuzzleEntity
        {
            OpenPart = request.OpenPart,
            HiddenPart = request.HiddenPart,
            SourceUrl = request.SourceUrl,
            StoryId = request.StoryId,
            JobId = request.JobId,
        };

        _dbContext.Puzzles.Add(entity);
        await _dbContext.SaveChangesAsync(context.CancellationToken);

        await InvalidateListCachesAsync(context.CancellationToken);

        return new SavePuzzleResponse
        {
            PuzzleId = entity.Id,
            Success = true,
        };
    }

    public override async Task<CountPuzzlesByJobResponse> CountPuzzlesByJob(
        CountPuzzlesByJobRequest request,
        ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.JobId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "job_id is required"));
        }

        var count = await _dbContext.Puzzles
            .AsNoTracking()
            .CountAsync(p => p.JobId == request.JobId, context.CancellationToken);

        return new CountPuzzlesByJobResponse { Count = count };
    }

    public override async Task<PuzzleResponse> GetPuzzleById(GetPuzzleByIdRequest request, ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.PuzzleId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "puzzle_id is required"));
        }

        var cacheKey = BuildPuzzleCacheKey(request.PuzzleId);
        var cachedResponse = await TryGetPuzzleFromCacheAsync(cacheKey, context.CancellationToken);
        if (cachedResponse is not null)
        {
            _logger.LogInformation("Cache hit for puzzle {PuzzleId}", request.PuzzleId);
            return cachedResponse;
        }

        _logger.LogInformation("Cache miss for puzzle {PuzzleId}", request.PuzzleId);

        var puzzle = await _dbContext.Puzzles.FindAsync([request.PuzzleId], context.CancellationToken);
        if (puzzle is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Данетка с ID {request.PuzzleId} не найдена"));
        }

        var response = MapToPuzzleResponse(puzzle);
        await SetPuzzleCacheAsync(cacheKey, response, context.CancellationToken);

        return response;
    }

    public override async Task<RevealAnswerResponse> RevealPuzzleAnswer(
        GetPuzzleByIdRequest request,
        ServerCallContext context)
    {
        if (string.IsNullOrWhiteSpace(request.PuzzleId))
        {
            throw new RpcException(new Status(StatusCode.InvalidArgument, "puzzle_id is required"));
        }

        var puzzle = await _dbContext.Puzzles
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == request.PuzzleId, context.CancellationToken);

        if (puzzle is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, $"Данетка с ID {request.PuzzleId} не найдена"));
        }

        return new RevealAnswerResponse
        {
            PuzzleId = puzzle.Id,
            HiddenPart = puzzle.HiddenPart,
        };
    }

    public override async Task<PuzzleResponse> GetRandomPuzzle(GetRandomPuzzleRequest request, ServerCallContext context)
    {
        var query = _dbContext.Puzzles.AsQueryable();

        if (request.ExcludeIds.Count > 0)
        {
            query = query.Where(p => !request.ExcludeIds.Contains(p.Id));
        }

        var puzzle = await query
            .OrderBy(_ => EF.Functions.Random())
            .FirstOrDefaultAsync(context.CancellationToken);

        if (puzzle is null)
        {
            throw new RpcException(new Status(StatusCode.NotFound, "Доступные данетки не найдены."));
        }

        return MapToPuzzleResponse(puzzle);
    }

    public override async Task<ListPuzzlesResponse> ListPuzzles(ListPuzzlesRequest request, ServerCallContext context)
    {
        var pageSize = Math.Clamp(request.PageSize, 1, 50);
        var page = Math.Max(request.Page, 1);

        var cacheKey = BuildListCacheKey(page, pageSize);
        var cachedResponse = await TryGetListFromCacheAsync(cacheKey, context.CancellationToken);
        if (cachedResponse is not null)
        {
            _logger.LogInformation("Cache hit for puzzles list page={Page} size={PageSize}", page, pageSize);
            return cachedResponse;
        }

        _logger.LogInformation("Cache miss for puzzles list page={Page} size={PageSize}", page, pageSize);

        var totalItems = await _dbContext.Puzzles.CountAsync(context.CancellationToken);

        var puzzles = await _dbContext.Puzzles
            .AsNoTracking()
            .OrderBy(p => p.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(context.CancellationToken);

        var response = new ListPuzzlesResponse
        {
            Total = totalItems,
            Page = page,
        };
        response.Puzzles.AddRange(puzzles.Select(MapToPuzzleResponse));

        await SetListCacheAsync(cacheKey, response, context.CancellationToken);

        return response;
    }

    private async Task<PuzzleResponse?> TryGetPuzzleFromCacheAsync(string cacheKey, CancellationToken cancellationToken)
    {
        var cachedBytes = await _cache.GetAsync(cacheKey, cancellationToken);
        if (cachedBytes is null || cachedBytes.Length == 0)
        {
            return null;
        }

        return PuzzleResponse.Parser.ParseFrom(cachedBytes);
    }

    private async Task SetPuzzleCacheAsync(
        string cacheKey,
        PuzzleResponse response,
        CancellationToken cancellationToken)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(PuzzleByIdCacheMinutes),
        };

        await _cache.SetAsync(cacheKey, response.ToByteArray(), options, cancellationToken);
    }

    private async Task<ListPuzzlesResponse?> TryGetListFromCacheAsync(string cacheKey, CancellationToken cancellationToken)
    {
        var cachedBytes = await _cache.GetAsync(cacheKey, cancellationToken);
        if (cachedBytes is null || cachedBytes.Length == 0)
        {
            return null;
        }

        return ListPuzzlesResponse.Parser.ParseFrom(cachedBytes);
    }

    private async Task SetListCacheAsync(
        string cacheKey,
        ListPuzzlesResponse response,
        CancellationToken cancellationToken)
    {
        var options = new DistributedCacheEntryOptions
        {
            AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(ListPuzzlesCacheMinutes),
        };

        await _cache.SetAsync(cacheKey, response.ToByteArray(), options, cancellationToken);
    }

    private async Task InvalidateListCachesAsync(CancellationToken cancellationToken)
    {
        var database = _redis.GetDatabase();

        foreach (var endpoint in _redis.GetEndPoints())
        {
            var server = _redis.GetServer(endpoint);
            foreach (var key in server.Keys(pattern: ListCacheKeyPattern))
            {
                await database.KeyDeleteAsync(key);
            }
        }

        _logger.LogInformation("Invalidated list puzzles cache entries");
    }

    private static string BuildPuzzleCacheKey(string puzzleId) => $"puzzle_{puzzleId}";

    private static string BuildListCacheKey(int page, int pageSize) => $"puzzles_page_{page}_size_{pageSize}";

    private static PuzzleResponse MapToPuzzleResponse(PuzzleEntity puzzle) => new()
    {
        PuzzleId = puzzle.Id,
        OpenPart = puzzle.OpenPart,
        SourceUrl = puzzle.SourceUrl,
        CreatedAt = ((DateTimeOffset)puzzle.CreatedAt).ToUnixTimeSeconds(),
    };
}
