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
    public class UserFileInfoCache : IUserFileInfoCache
    {
        private ILogger<UserFileInfoCache> _logger;
        private ITableDataStore<UserFileInfoEntity> _tableDataStore;

        public UserFileInfoCache(
            ITableDataStore<UserFileInfoEntity> tableDataStore,
            ILogger<UserFileInfoCache> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tableDataStore = tableDataStore;
        }

        public async Task InsertUserFileInfoIfNotExistAsync(UserFileInfoEntity entity)
        {
            try
            {
                var fileExistsWithSameHash = await GetFileByHash(entity.Hash);

                if (fileExistsWithSameHash == null)
                {
                    await _tableDataStore.InsertAsync(BatchingMode.None, entity);
                    Console.WriteLine($"Writing current file info '{entity.FileName}' in to the user file info storage'");
                }
                else
                {
                    Console.WriteLine($"The same hash for the File '{entity.FileName}' already exists in the storage with filename '{fileExistsWithSameHash.FileName}'");
                }
            }
            catch (AzureTableDataStoreSingleOperationException<UserFileInfoEntity> e)
            {
                throw new Exception("Inserting into property cache table failed: " + e.Message, e);
            }
        }

        public async Task TestConnectivity()
        {
            await _tableDataStore.ListAsync(x => new { x.FileName }, 1);
        }

        public async Task<int> GetFileCountByUserId(string userId)
        {
            try
            {
                Expression<Func<UserFileInfoEntity, bool>> query = file => file.UserId == userId;
                var file = await _tableDataStore.FindAsync(query);
                return file.Count();
            }
            catch (Exception e)
            {
                throw new Exception("Retrieving from property cache table failed: " + e.Message, e);
            }
        }

        public async Task<UserFileInfoEntity> GetFileByHash(string hash)
        {
            try
            {
                Expression<Func<UserFileInfoEntity, bool>> query = file => file.Hash == hash;
                var file = await _tableDataStore.FindAsync(query);
                return file.ToList().FirstOrDefault();
            }
            catch (Exception e)
            {
                throw new Exception("Retrieving from property cache table failed: " + e.Message, e);
            }
        }

        public async Task UpdateSuccessState(string hash, bool successState)
        {
            try
            {
                Expression<Func<UserFileInfoEntity, bool>> query = file => file.Hash == hash;
                var fileToUpdate = await _tableDataStore.FindAsync(query);
                var entity = fileToUpdate.ToList().FirstOrDefault();

                if (entity != null)
                {
                    entity.SuccessFullyParsed = successState;

                    await _tableDataStore.InsertOrReplaceAsync(BatchingMode.None, entity);
                    Console.WriteLine($"Updated SuccessFullyParsed for file '{entity.FileName}' to '{successState}'.");
                }
                else
                {
                    Console.WriteLine($"No file found with hash '{hash}' to update.");
                }
            }
            catch (Exception e)
            {
                throw new Exception("Updating the success state in property cache table failed: " + e.Message, e);
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
