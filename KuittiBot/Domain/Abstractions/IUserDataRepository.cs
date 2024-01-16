using KuittiBot.Functions.Domain.Models;
using KuittiBot.Functions.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KuittiBot.Functions.Domain.Abstractions
{
    public interface IUserDataRepository
    {
        Task InsertAsync(UserDataEntity user);
        Task UpdateUserAsync(UserDataEntity user);
        Task<UserDataEntity> GetUserByIdAsync(string userId);
        Task<string> GetUserStateByIdAsync(string userId);
        Task<int> GetCurrentSessionByIdAsync(string userId);
        Task SetNewSessionForUserAsync(int sessionId, string userId);
        Task<IList<UserDataEntity>> GetAllUsers();
        //Task DeleteAsync(string kohdetunnus);
    }
}
