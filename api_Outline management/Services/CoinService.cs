using System;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using api_Outline_management.Data;
using api_Outline_management.Entities.Auth;

namespace api_Outline_management.Services;

public interface ICoinService
{
    Task<int> GetBalanceAsync(Guid userId);
    Task<bool> DeductCoinsAsync(Guid userId, int amount, string reason);
    Task RewardCoinsAsync(Guid userId, int amount, string reason, string transactionType = "PvPReward");
}

public class CoinService : ICoinService
{
    private readonly EduClashDbContext _context;

    public CoinService(EduClashDbContext context)
    {
        _context = context;
    }

    public async Task<int> GetBalanceAsync(Guid userId)
    {
        var profile = await _context.Profiles.FirstOrDefaultAsync(p => p.UserId == userId);
        return profile?.Coins ?? 0;
    }

    public async Task<bool> DeductCoinsAsync(Guid userId, int amount, string reason)
    {
        if (amount <= 0) return true;

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var profile = await _context.Profiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile == null || profile.Coins < amount)
            {
                return false; // Không đủ xu
            }

            profile.Coins -= amount;

            var coinTx = new CoinTransaction
            {
                UserId = userId,
                Amount = -amount,
                TransactionType = "AiQuizGeneration",
                Description = reason,
                CreatedAt = DateTimeOffset.UtcNow
            };

            _context.CoinTransactions.Add(coinTx);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            return false;
        }
    }

    public async Task RewardCoinsAsync(Guid userId, int amount, string reason, string transactionType = "PvPReward")
    {
        if (amount <= 0) return;

        using var transaction = await _context.Database.BeginTransactionAsync();
        try
        {
            var profile = await _context.Profiles
                .FirstOrDefaultAsync(p => p.UserId == userId);

            if (profile != null)
            {
                profile.Coins += amount;

                var coinTx = new CoinTransaction
                {
                    UserId = userId,
                    Amount = amount,
                    TransactionType = transactionType,
                    Description = reason,
                    CreatedAt = DateTimeOffset.UtcNow
                };

                _context.CoinTransactions.Add(coinTx);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
        }
        catch
        {
            await transaction.RollbackAsync();
        }
    }
}
