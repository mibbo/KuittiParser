﻿using AzureTableDataStore;
using KuittiBot.Functions.Domain.Abstractions;
using KuittiBot.Functions.Domain.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;

namespace KuittiBot.Functions.Infrastructure
{
    public class UserDataCache : IUserDataCache
    {
        private ILogger<UserDataCache> _logger;
        private ITableDataStore<UserDataCacheEntity> _tableDataStore;

        public UserDataCache(
            ITableDataStore<UserDataCacheEntity> tableDataStore,
            ILogger<UserDataCache> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tableDataStore = tableDataStore;
        }

        public async Task InsertAsync(UserDataCacheEntity property)
        {
            try
            {
                var entity = property;
                await _tableDataStore.InsertAsync(BatchingMode.None, entity);
            }
            catch (AzureTableDataStoreSingleOperationException<UserDataCacheEntity> e)
            {
                throw new Exception("Inserting into property cache table failed: " + e.Message, e);
            }
        }

        public async Task UpdateUserStateAsync(UserDataCacheEntity property)
        {
            try
            {
                var entity = property;
                await _tableDataStore.InsertOrReplaceAsync(BatchingMode.None, entity);
            }
            catch (Exception e)
            {
                throw new Exception("Updating property cache table failed: " + e.Message, e);
            }
        }

        public async Task TestConnectivity()
        {
            await _tableDataStore.ListAsync(x => new { x.Id }, 1);
        }

        public async Task<UserDataCacheEntity> GetUserById(string userId)
        {
            try
            {
                Expression<Func<UserDataCacheEntity, bool>> query = user => user.Id == userId;
                var user = await _tableDataStore.FindAsync(query);
                return user.ToList().FirstOrDefault();
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
