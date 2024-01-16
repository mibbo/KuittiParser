//using AzureTableDataStore;
//using KuittiBot.Functions.Domain.Abstractions;
//using KuittiBot.Functions.Domain.Models;
//using Microsoft.Extensions.Logging;
//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Linq.Expressions;
//using System.Text;
//using System.Threading.Tasks;

//namespace KuittiBot.Functions.Infrastructure
//{
//    public class UserDataCache : IUserDataCache
//    {
//        private ILogger<UserDataCache> _logger;
//        private ITableDataStore<UserDataEntity> _tableDataStore;

//        public UserDataCache(
//            ITableDataStore<UserDataEntity> tableDataStore,
//            ILogger<UserDataCache> logger)
//        {
//            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
//            _tableDataStore = tableDataStore;
//        }

//        public async Task InsertAsync(UserDataEntity property)
//        {
//            try
//            {
//                var entity = property;
//                await _tableDataStore.InsertAsync(BatchingMode.None, entity);
//            }
//            catch (AzureTableDataStoreSingleOperationException<UserDataEntity> e)
//            {
//                throw new Exception("Inserting into property cache table failed: " + e.Message, e);
//            }
//        }

//        public async Task UpdateUserAsync(UserDataEntity property)
//        {
//            try
//            {
//                var entity = property;
//                await _tableDataStore.InsertOrReplaceAsync(BatchingMode.None, entity);
//            }
//            catch (Exception e)
//            {
//                throw new Exception("Updating property cache table failed: " + e.Message, e);
//            }
//        }

//        public async Task<UserDataEntity> GetUserByIdAsync(string userId)
//        {
//            try
//            {
//                Expression<Func<UserDataEntity, bool>> query = user => user.UserId == userId;
//                var user = await _tableDataStore.FindAsync(query);
//                return user.ToList().FirstOrDefault();
//            }
//            catch (Exception e)
//            {
//                throw new Exception($"Retrieving user id '{userId}' from property cache table failed: " + e.Message, e);
//            }
//        }

//        public async Task<string> GetUserStateByIdAsync(string userId)
//        {
//            try
//            {
//                Expression<Func<UserDataEntity, bool>> query = user => user.UserId == userId;
//                var userFromStorage = await _tableDataStore.FindAsync(query);
//                var userIdFromStorage = userFromStorage.ToList().FirstOrDefault()?.UserId;

//                return userIdFromStorage;
//            }
//            catch (Exception e)
//            {
//                throw new Exception($"Retrieving user id '{userId}' from property cache table failed: " + e.Message, e);
//            }
//        }


//        public async Task<IList<UserDataEntity>> GetAllUsers()
//        {
//            try
//            {
//                var users = await _tableDataStore.ListAsync();
//                return users;
//            }
//            catch (Exception e)
//            {
//                throw new Exception($"Retrieving users from user cache table failed: " + e.Message, e);
//            }
//        }

//        //public async Task DeleteAsync(string kohdetunnus)
//        //{
//        //    try
//        //    {
//        //        await _tableDataStore.DeleteAsync(BatchingMode.None, x => x.Id == kohdetunnus);
//        //    }
//        //    catch (Exception e)
//        //    {
//        //        throw new Exception($"Failed to delete property number {kohdetunnus}: " + e.Message, e);
//        //    }
//        //}
//    }
//}
