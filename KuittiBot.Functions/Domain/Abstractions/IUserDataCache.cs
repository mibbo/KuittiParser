using KuittiBot.Functions.Domain.Models;
using KuittiBot.Functions.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KuittiBot.Functions.Domain.Abstractions
{
    public interface IUserDataCache
    {
        Task InsertAsync(UserDataCacheEntity property);
        Task UpdateAsync(UserDataCacheEntity property);
        Task<UserDataCacheEntity> GetUserById(string userId);
        Task DeleteAsync(string kohdetunnus);
    }
}
