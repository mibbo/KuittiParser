using KuittiBot.Functions.Domain.Models;
using KuittiBot.Functions.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KuittiBot.Functions.Domain.Abstractions
{
    public interface IReceiptSessionRepository
    {
        Task<int> InitializeSession(SessionInfo session);
        //Task<int> GetSessionCountByUserId(string userId);
        //Task<ReceiptSessionEntity> GetSessionByHash(string hash);
        //Task UpdateSession(SessionInfo updatedSession);
        Task SetSessionPayers(List<string> payers, int sessionId, string userId);
        Task SaveReceiptAsync(Receipt receipt/*, ReceiptSessionEntity session*/);
        Task<Product> GetNextProductBySessionIdAsync(int sessionId);
        Task<bool> ProcessNextProductAndCheckIfDoneAsync(int sessionId);
        //Task<(Product ProcessedProduct, bool IsProcessingComplete)> ProcessNextProductAndCheckIfDoneAsync(int sessionId);
        Task<List<string>> GetPayerNamesBySessionIdAsync(int sessionId);
        Task DeleteAllDataAsync();
    }
}
