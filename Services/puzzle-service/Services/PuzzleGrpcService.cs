using Grpc.Core;
using Microsoft.EntityFrameworkCore;
using PuzzleService.Data;
using PuzzleService.Models;
using System;
using System.Linq;
using System.Threading.Tasks;

using Danetka.Contracts.Puzzle; 

namespace PuzzleService.Services
{
    public class PuzzleGrpcService : Danetka.Contracts.Puzzle.PuzzleService.PuzzleServiceBase
    {
        private readonly PuzzleDbContext _dbContext;

        public PuzzleGrpcService(PuzzleDbContext dbContext)
        {
            _dbContext = dbContext;
        }

        //  Принимает и сохраняет готовую данетку от AI Worker
        public override async Task<SavePuzzleResponse> SavePuzzle(SavePuzzleRequest request, ServerCallContext context)
        {
            var entity = new PuzzleEntity
            {
                OpenPart = request.OpenPart,
                HiddenPart = request.HiddenPart,
                SourceUrl = request.SourceUrl,
                StoryId = request.StoryId 
            };

            _dbContext.Puzzles.Add(entity);
            await _dbContext.SaveChangesAsync();

            return new SavePuzzleResponse
            {
                PuzzleId = entity.Id,
                Success = true
            };
        }

        //  Выдает конкретную данетку по ID (БЕЗ hidden_part)
        public override async Task<PuzzleResponse> GetPuzzleById(GetPuzzleByIdRequest request, ServerCallContext context)
        {
            var puzzle = await _dbContext.Puzzles.FindAsync(request.PuzzleId);

            if (puzzle == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, $"Данетка с ID {request.PuzzleId} не найдена"));
            }

            return new PuzzleResponse
            {
                PuzzleId = puzzle.Id,
                OpenPart = puzzle.OpenPart,
                SourceUrl = puzzle.SourceUrl,
                CreatedAt = ((DateTimeOffset)puzzle.CreatedAt).ToUnixTimeSeconds() // Конвертируем в Unix timestamp
            };
        }

        // Выдает случайную данетку с учетом исключений (exclude_ids)
        public override async Task<PuzzleResponse> GetRandomPuzzle(GetRandomPuzzleRequest request, ServerCallContext context)
        {
            var query = _dbContext.Puzzles.AsQueryable();

            if (request.ExcludeIds.Any())
            {
                query = query.Where(p => !request.ExcludeIds.Contains(p.Id));
            }

            var puzzle = await query
                .OrderBy(p => EF.Functions.Random())
                .FirstOrDefaultAsync();

            if (puzzle == null)
            {
                throw new RpcException(new Status(StatusCode.NotFound, "Доступные данетки не найдены."));
            }

            return new PuzzleResponse
            {
                PuzzleId = puzzle.Id,
                OpenPart = puzzle.OpenPart,
                SourceUrl = puzzle.SourceUrl,
                CreatedAt = ((DateTimeOffset)puzzle.CreatedAt).ToUnixTimeSeconds()
            };
        }

        // 4. Выдает список данеток
        public override async Task<ListPuzzlesResponse> ListPuzzles(ListPuzzlesRequest request, ServerCallContext context)
        {
            int pageSize = Math.Clamp(request.PageSize, 1, 50);
            int page = Math.Max(request.Page, 1);

            var totalItems = await _dbContext.Puzzles.CountAsync();

            var puzzles = await _dbContext.Puzzles
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new PuzzleResponse
                {
                    PuzzleId = p.Id,
                    OpenPart = p.OpenPart,
                    SourceUrl = p.SourceUrl,
                    CreatedAt = ((DateTimeOffset)p.CreatedAt).ToUnixTimeSeconds()
                })
                .ToListAsync();

            var response = new ListPuzzlesResponse
            {
                Total = totalItems,
                Page = page
            };
            
            response.Puzzles.AddRange(puzzles);

            return response;
        }
    }
}