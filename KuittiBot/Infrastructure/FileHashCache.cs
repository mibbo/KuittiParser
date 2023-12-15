using AzureTableDataStore;
using KuittiBot.Functions.Domain.Abstractions;
using KuittiBot.Functions.Domain.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace KuittiBot.Functions.Infrastructure
{
    public class FileHashCache : IFileHashCache
    {
        private ILogger<FileHashCache> _logger;
        private ITableDataStore<FileHashEntity> _tableDataStore;

        public FileHashCache(
            ITableDataStore<FileHashEntity> tableDataStore,
            ILogger<FileHashCache> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tableDataStore = tableDataStore;
        }

        public async Task InsertFileHashAsync(string fileName, string hash)
        {
            try
            {
                var entity = new FileHashEntity
                {
                    FileName = fileName,
                    Hash = hash
                };

                var fileExistsWithSameHash = await GetFileByHash(entity.Hash);

                if (fileExistsWithSameHash == null)
                {
                    await _tableDataStore.InsertAsync(BatchingMode.None, entity);
                    Console.WriteLine($"Writing file '{fileName}' in to the storage with the hash '{entity.Hash}'");
                }
                else
                {
                    Console.WriteLine($"The same hash for the File '{fileName}' already exists in the storage with filename '{fileExistsWithSameHash.FileName}'");
                }
            }
            catch (AzureTableDataStoreSingleOperationException<FileHashEntity> e)
            {
                throw new Exception("Inserting into property cache table failed: " + e.Message, e);
            }
        }

        public async Task TestConnectivity()
        {
            await _tableDataStore.ListAsync(x => new { x.FileName }, 1);
        }

        public async Task<FileHashEntity> GetFileById(string fileName)
        {
            try
            {
                Expression<Func<FileHashEntity, bool>> query = file => file.FileName == fileName;
                var file = await _tableDataStore.FindAsync(query);
                return file.ToList().FirstOrDefault();
            }
            catch (Exception e)
            {
                throw new Exception("Retrieving from property cache table failed: " + e.Message, e);
            }
        }

        public async Task<FileHashEntity> GetFileByHash(string hash)
        {
            try
            {
                Expression<Func<FileHashEntity, bool>> query = file => file.Hash == hash;
                var file = await _tableDataStore.FindAsync(query);
                return file.ToList().FirstOrDefault();
            }
            catch (Exception e)
            {
                throw new Exception("Retrieving from property cache table failed: " + e.Message, e);
            }
        }


        //public async Task<int> GetCount(string userId)
        //{
        //    try
        //    {
        //        Expression<Func<UserDataCacheEntity, bool>> query = user => user.Id == userId;
        //        var user = await _tableDataStore.FindAsync(query);
        //        return user.ToList().Count()+1;
        //    }
        //    catch (Exception e)
        //    {
        //        throw new Exception("Retrieving from property cache table failed: " + e.Message, e);
        //    }
        //}

        //public async Task DeleteAsync(string kohdetunnus)
        //{
        //    try
        //    {
        //        await _tableDataStore.DeleteAsync(BatchingMode.None, x => x.Id == kohdetunnus);
        //    }
        //    catch (Exception e)
        //    {
        //        throw new Exception($"Failed to delete property number {kohdetunnus}: " + e.Message, e);
        //    }
        //}
    }
}
