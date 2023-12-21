using KuittiBot.Functions.Domain.Models;
using KuittiBot.Functions.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KuittiBot.Functions.Domain.Abstractions
{
    public interface IReceiptSessionCache
    {
        Task InsertSessionIfNotExistAsync(ReceiptSessionEntity entity);
        Task<int> GetSessionCountByUserId(string userId);
        Task<ReceiptSessionEntity> GetSessionByHash(string hash);
        Task UpdateSessionSuccessState(string hash, bool successState);
    }
}
