using KuittiBot.Functions.Domain.Models;
using KuittiBot.Functions.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KuittiBot.Functions.Domain.Abstractions
{
    public interface IUserFileInfoCache
    {
        Task InsertUserFileInfoIfNotExistAsync(UserFileInfoEntity entity);
        Task<int> GetFileCountByUserId(string userId);
        Task<UserFileInfoEntity> GetFileByHash(string hash);
        Task UpdateSuccessState(string hash, bool successState);
    }
}
