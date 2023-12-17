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
        Task InsertAsync(UserDataCacheEntity user);
        Task UpdateUserAsync(UserDataCacheEntity user);
        Task<UserDataCacheEntity> GetUserByIdAsync(string userId);
        Task<string> GetUserStateByIdAsync(string userId);
        Task<IList<UserDataCacheEntity>> GetAllUsers();
        //Task DeleteAsync(string kohdetunnus);
    }
}
