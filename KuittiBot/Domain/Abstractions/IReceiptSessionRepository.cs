using KuittiBot.Functions.Domain.Models;
using KuittiBot.Functions.Infrastructure;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
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
        Task SetGroupModeForCurrentSession(int sessionId, bool groupMode);
        Task<bool> IsGroupModeEnabledAsync(int sessionId);
        Task SetSessionPayers(List<string> payers, int sessionId);
        Task SetSessionGroups(Dictionary<string, List<string>> groups, int sessionId);
        Task SaveReceiptAsync(Receipt receipt/*, ReceiptSessionEntity session*/);
        Task<Product> GetNextProductBySessionIdAsync(int sessionId);
        Task<bool> ProcessNextProductAndCheckIfDoneAsync(int sessionId);
        Task<bool> CheckIfProductAskedDoneAsync(int sessionId);
        //Task<(Product ProcessedProduct, bool IsProcessingComplete)> ProcessNextProductAndCheckIfDoneAsync(int sessionId);
        Task<List<string>> GetPayerNamesBySessionIdAsync(int sessionId);
        Task<List<string>> GetGroupNamesBySessionIdAsync(int sessionId);
        Task<List<int>> GetGroupMembersByGroupNameAndSessionIdAsync(string groupName, int sessionId);
        Task<bool> IsPayerLinkedToProductAsync(int payerId, int productId);
        Task AddPayerToProductAsync(int payerId, int productId);
        Task RemovePayerFromProductAsync(int payerId, int productId);
        Task<int> GetPayerIdByNameAndSessionIdAsync(string name, int sessionId);
        Task<List<int>> GetAllPayerIdsBySessionIdAsync(int sessionId);
        Task<List<Payer>> GetPayersForProductBySessionAsync(int productId, int sessionId);
        Task<List<Product>> GetProductsForPayerAsync(int payerId);
        Task<List<Payer>> GetProductsForEachPayerAsync(int sessionId);
        Task<Dictionary<int, decimal>> GetCostsForPayerAsync(int payerId);
        Task CalculateCostsForEachPayerAsync(List<Payer> payers);
        Task DeleteAllDataAsync();
    }
}
