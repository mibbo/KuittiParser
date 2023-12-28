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
    public class ReceiptSessionCache : IReceiptSessionCache
    {
        private ILogger<ReceiptSessionCache> _logger;
        private ITableDataStore<ReceiptSessionEntity> _tableDataStore;

        public ReceiptSessionCache(
            ITableDataStore<ReceiptSessionEntity> tableDataStore,
            ILogger<ReceiptSessionCache> logger)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _tableDataStore = tableDataStore;
        }

        public async Task InsertSessionIfNotExistAsync(ReceiptSessionEntity entity)
        {
            try
            {
                var fileExistsWithSameHash = await GetSessionByHash(entity.Hash);

                if (fileExistsWithSameHash == null)
                {
                    await _tableDataStore.InsertAsync(BatchingMode.None, entity);
                    Console.WriteLine($"Writing current file info '{entity.FileName}' in to the user session info storage'");
                }
                else
                {
                    Console.WriteLine($"The same hash for the Session file '{entity.FileName}' already exists in the storage with filename '{fileExistsWithSameHash.FileName}'");
                }
            }
            catch (AzureTableDataStoreSingleOperationException<ReceiptSessionEntity> e)
            {
                throw new Exception("Inserting into session cache table failed: " + e.Message, e);
            }
        }

        public async Task<int> GetSessionCountByUserId(string userId)
        {
            try
            {
                Expression<Func<ReceiptSessionEntity, bool>> query = file => file.UserId == userId;
                var file = await _tableDataStore.FindAsync(query);
                return file.Count();
            }
            catch (Exception e)
            {
                throw new Exception("Retrieving from session cache table failed: " + e.Message, e);
            }
        }

        public async Task<ReceiptSessionEntity> GetSessionByHash(string hash)
        {
            try
            {
                Expression<Func<ReceiptSessionEntity, bool>> query = file => file.Hash == hash;
                var file = await _tableDataStore.FindAsync(query);
                return file.ToList().FirstOrDefault();
            }
            catch (Exception e)
            {
                throw new Exception("Retrieving from session cache table failed: " + e.Message, e);
            }
        }

        public async Task UpdateSessionSuccessState(string hash, bool successState)
        {
            try
            {
                Expression<Func<ReceiptSessionEntity, bool>> query = file => file.Hash == hash;
                var fileToUpdate = await _tableDataStore.FindAsync(query);
                var entity = fileToUpdate.ToList().FirstOrDefault();

                if (entity != null)
                {
                    entity.SessionSuccessful = successState;

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
                throw new Exception("Updating the success state in session cache table failed: " + e.Message, e);
            }
        }
}
