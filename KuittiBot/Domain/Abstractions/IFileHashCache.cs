using KuittiBot.Functions.Domain.Models;
using KuittiBot.Functions.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KuittiBot.Functions.Domain.Abstractions
{
    public interface IFileHashCache
    {
        Task InsertFileHashAsync(string fileName, string hash);
        Task<FileHashEntity> GetFileById(string fileName);
        Task<FileHashEntity> GetFileByHash(string hash);
    }
}
