using AzureTableDataStore;
using Dapper;
using KuittiBot.Functions.Domain.Abstractions;
using KuittiBot.Functions.Domain.Models;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Linq;
using System.Linq.Expressions;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using static Dapper.SqlMapper;

namespace KuittiBot.Functions.Infrastructure
{
    public class ReceiptSessionRepository : IReceiptSessionRepository
    {
        private ILogger<ReceiptSessionRepository> _logger;
        private readonly string _connectionString;
        public ReceiptSessionRepository(
            ILogger<ReceiptSessionRepository> logger,
            string connectionString)
        {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _connectionString = connectionString;
        }

        public async Task<int> InitializeSession(SessionInfo session)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);

                var sessionExistsQuery = "SELECT COUNT(1) FROM Receipts WHERE Hash = @Hash";
                var sessionExists = await connection.ExecuteScalarAsync<int>(sessionExistsQuery, new { Hash = session.Hash }) > 0;

                if (!sessionExists)
                {
                    string insertQuery = @"
                        INSERT INTO Receipts (Hash, FileName, SessionSuccessful, GroupMode) 
                        VALUES (@Hash, @FileName, @SessionSuccessful, @GroupMode); 
                        SELECT CAST(SCOPE_IDENTITY() as int);";
                    var sessionId = await connection.ExecuteScalarAsync<int>(insertQuery, session);
                    return sessionId;
                }
                else
                {
                    Console.WriteLine($"The same hash for the Session file '{session.FileName}' already exists in the storage with filename '{session.FileName}'");
                    return -1;
                }
            }
            catch (Exception e)
            {
                throw new Exception("Inserting into session table failed: " + e.Message, e);
            }
        }

        public async Task SetGroupModeForCurrentSession(int sessionId, bool groupMode)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);

                string query = "UPDATE Receipts SET GroupMode = @GroupMode WHERE SessionId = @SessionId";

                await connection.ExecuteAsync(query, new { GroupMode = groupMode, SessionId = sessionId });
            }
            catch (Exception e)
            {
                throw new Exception("Error in setting group mode for current session: " + e.Message, e);
            }
        }

        public async Task<bool> IsGroupModeEnabledAsync(int sessionId)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);

                var query = @"
                            SELECT GroupMode 
                            FROM Receipts 
                            WHERE SessionId = @SessionId;";

                var groupMode = await connection.QuerySingleOrDefaultAsync<bool>(query, new { SessionId = sessionId });

                return groupMode;
            }
            catch (Exception e)
            {
                throw new Exception("Fetching GroupMode from Receipts table failed: " + e.Message, e);
            }
        }


        public async Task InsertSessionIfNotExistAsync(SessionInfo session)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);

                var sessionExistsQuery = "SELECT COUNT(1) FROM Receipts WHERE Hash = @Hash";
                var sessionExists = await connection.ExecuteScalarAsync<int>(sessionExistsQuery, new { Hash = session.Hash }) > 0;

                if (!sessionExists)
                {
                    string query = "INSERT INTO Receipts (SessionId, Hash, FileName, SessionSuccesful, ShopName, RawTotalCost) VALUES (@SessionId, @Hash, @FileName, @SessionSuccesful, @ShopName, @RawTotalCost)";
                    var rowsAffected = await connection.ExecuteAsync(query, session);
                }
                else
                {
                    Console.WriteLine($"The same hash for the Session file '{session.FileName}' already exists in the storage with filename '{session.FileName}'");
                }
            }
            catch (Exception e)
            {
                throw new Exception("Inserting into session table failed: " + e.Message, e);
            }
        }

        public async Task SetSessionPayers(List<string> payers, int sessionId)
        {
            try
            {
                foreach (var payer in payers)
                {
                    string insertPayerQuery = "INSERT INTO Payers (SessionId, Name) VALUES (@SessionId, @Name); SELECT CAST(SCOPE_IDENTITY() as int);";
                    //string insertPayerReceiptQuery = "INSERT INTO PayerReceipts (PayerId, SessionId) VALUES (@PayerId, @SessionId);";

                    using var connection = new SqlConnection(_connectionString);

                    // Insert into Payers table and get the PayerId
                    var payerId = await connection.ExecuteScalarAsync<int>(insertPayerQuery, new { SessionId = sessionId, Name = payer });
                }
            }
            catch (Exception e)
            {
                throw new Exception("Error in setting session payers: " + e.Message, e);
            }
        }

        public async Task SetSessionGroups(Dictionary<string, List<string>> groups, int sessionId)
        {
            try
            {
                using var connection = new SqlConnection(_connectionString);
                await connection.OpenAsync();
                using var transaction = connection.BeginTransaction();

                foreach (var group in groups)
                {
                    // Insert the group into the Groups table and get the GroupId
                    string insertGroupQuery = @"
                            INSERT INTO Groups (SessionId, GroupName) 
                            VALUES (@SessionId, @GroupName); 
                            SELECT CAST(SCOPE_IDENTITY() as int);";
                    var groupId = await connection.ExecuteScalarAsync<int>(
                        insertGroupQuery,
                        new { SessionId = sessionId, GroupName = group.Key },
                        transaction
                    );

                    // Insert the members into the GroupPayers table
                    string insertMemberQuery = "INSERT INTO GroupPayers (GroupId, PayerId) VALUES (@GroupId, @PayerId);";

                    foreach (var member in group.Value)
                    {
                        var payerId = await GetPayerIdByNameAndSessionIdAsync(connection, member, sessionId, transaction);
                        if (payerId.HasValue)
                        {
                            await connection.ExecuteAsync(
                                insertMemberQuery,
                                new { GroupId = groupId, PayerId = payerId.Value },
                                transaction
                            );
                        }
                    }
                }

                transaction.Commit();
            }
            catch (Exception e)
            {
                _logger.LogError(e, "Error in setting session groups.");
                throw new Exception("Error in setting session groups: " + e.Message, e);
            }
        }
        private async Task<int?> GetPayerIdByNameAndSessionIdAsync(SqlConnection connection, string payerName, int sessionId, SqlTransaction transaction)
        {
            string query = @"
                SELECT PayerId 
                FROM Payers 
                WHERE Name = @Name AND SessionId = @SessionId;";

            return await connection.QuerySingleOrDefaultAsync<int?>(
                query,
                new { Name = payerName, SessionId = sessionId },
                transaction
            );
        }

        //public async Task SetSessionGroups(List<string> groups, int sessionId)
        //{
        //    try
        //    {
        //        foreach (var group in groups)
        //        {
        //            string insertPayerQuery = "INSERT INTO Groups (SessionId, GroupName) VALUES (@SessionId, @GroupName); SELECT CAST(SCOPE_IDENTITY() as int);";

        //            using var connection = new SqlConnection(_connectionString);

        //            // Insert into Groups table and get the GroupId
        //            var payerId = await connection.ExecuteScalarAsync<int>(insertPayerQuery, new { SessionId = sessionId, Name = group });
        //        }
        //    }
        //    catch (Exception e)
        //    {
        //        throw new Exception("Error in setting session payers: " + e.Message, e);
        //    }
        //}

        //TODO: merge Receipt and ReceiptSessionEntity
        public async Task SaveReceiptAsync(Receipt receipt/*, ReceiptSessionEntity session*/)
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            // Transaction ensures all or nothing is committed
            using var transaction = connection.BeginTransaction();

            try
            {
                //// Insert receipt and get SessionId
                //var sessionId = await connection.ExecuteScalarAsync<int>(
                //    "INSERT INTO Receipts (Hash, FileName, SessionSuccessful, ShopName, RawTotalCost) OUTPUT INSERTED.SessionId VALUES (@Hash, @FileName, @SessionSuccessful, @ShopName, @RawTotalCost);",
                //    new { session.Hash, session.FileName, session.SessionSuccessful, receipt.ShopName, receipt.RawTotalCost },
                //    transaction);

                int productNumber = 0;
                string query = "UPDATE Receipts SET SessionSuccessful = @SessionSuccessful, ShopName = @ShopName, RawTotalCost = @RawTotalCost, ProductsInTotal = @ProductsInTotal, CurrentProduct = @CurrentProduct WHERE SessionId = @SessionId;";
                await connection.ExecuteAsync(query, new { receipt.SessionSuccessful, receipt.ShopName, receipt.RawTotalCost, SessionId = receipt.SessionId, ProductsInTotal = receipt.Products.Count, CurrentProduct = productNumber+1 }, transaction);

                foreach (var product in receipt.Products)
                {
                    // Insert product and get ProductId
                    var productId = await connection.ExecuteScalarAsync<int>(
                        "INSERT INTO Products (ProductNumber, Name, Cost, Quantity) OUTPUT INSERTED.ProductId VALUES (@ProductNumber, @Name, @Cost);",
                        new { ProductNumber = productNumber, product.Name, product.Cost, product.Quantity },
                        transaction);
                    productNumber++;

                    // Insert discounts if any
                    foreach (var discount in product.Discounts ?? Enumerable.Empty<decimal>())
                    {
                        await connection.ExecuteAsync(
                            "INSERT INTO ProductDiscounts (ProductId, Discount) VALUES (@ProductId, @Discount);",
                            new { ProductId = productId, Discount = discount },
                            transaction);
                    }

                    // Link receipt and product
                    await connection.ExecuteAsync(
                        "INSERT INTO ReceiptProducts (SessionId, ProductId, DividedCost) VALUES (@SessionId, @ProductId, @DividedCost);",
                        new { SessionId = receipt.SessionId, ProductId = productId, DividedCost = product.DividedCost },
                        transaction);
                }

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }




        //public async Task<Product> GetNextProductBySessionIdAsync(int sessionId)
        //{
        //    using var connection = new SqlConnection(_connectionString);

        //    var productNumberQuery = @"SELECT CurrentProduct FROM Receipts WHERE SessionId = @SessionId;";
        //    var nextProductNumber = await connection.QuerySingleOrDefaultAsync<int>(productNumberQuery, new
        //    {
        //        SessionId = sessionId
        //    });

        //    var query = @"
        //        SELECT p.ProductId, p.Name, p.Cost, rp.DividedCost
        //        FROM Products p
        //        JOIN ReceiptProducts rp ON p.ProductId = rp.ProductId
        //        WHERE p.ProductNumber = @ProductNumber AND rp.SessionId = @SessionId;";

        //    var product = await connection.QuerySingleOrDefaultAsync<Product>(query, new
        //    {
        //        ProductNumber = nextProductNumber-1,
        //        SessionId = sessionId
        //    });

        //    return product;
        //}
        public async Task<Product> GetNextProductBySessionIdAsync(int sessionId)
        {
            using var connection = new SqlConnection(_connectionString);

            var productNumberQuery = @"SELECT CurrentProduct FROM Receipts WHERE SessionId = @SessionId;";
            var nextProductNumber = await connection.QuerySingleOrDefaultAsync<int>(productNumberQuery, new
            {
                SessionId = sessionId
            });

            var query = @"
                SELECT p.ProductId, p.Name, p.Cost, rp.DividedCost
                FROM Products p
                JOIN ReceiptProducts rp ON p.ProductId = rp.ProductId
                WHERE p.ProductNumber = @ProductNumber AND rp.SessionId = @SessionId;";

            var product = await connection.QuerySingleOrDefaultAsync<Product>(query, new
            {
                ProductNumber = nextProductNumber - 1,
                SessionId = sessionId
            });

            return product;
        }

        public async Task<bool> ProcessNextProductAndCheckIfDoneAsync(int sessionId)
        {
            using var connection = new SqlConnection(_connectionString);
            var checkQuery = "SELECT CurrentProduct, ProductsInTotal FROM Receipts WHERE SessionId = @SessionId;";
            var productInfo = await connection.QuerySingleOrDefaultAsync<(int CurrentProduct, int ProductsInTotal)>(checkQuery, new { SessionId = sessionId });
            
            var updateQuery = "UPDATE Receipts SET CurrentProduct = CurrentProduct + 1 WHERE SessionId = @SessionId;";

            await connection.ExecuteAsync(updateQuery, new { SessionId = sessionId });

            return productInfo.CurrentProduct >= productInfo.ProductsInTotal; // Return true if CurrentProduct equals ProductsInTotal
        }

        public async Task<bool> CheckIfProductAskedDoneAsync(int sessionId)
        {
            using var connection = new SqlConnection(_connectionString);
            var checkQuery = "SELECT CurrentProduct, ProductsInTotal FROM Receipts WHERE SessionId = @SessionId;";
            var productInfo = await connection.QuerySingleOrDefaultAsync<(int CurrentProduct, int ProductsInTotal)>(checkQuery, new { SessionId = sessionId });

            return productInfo.CurrentProduct == productInfo.ProductsInTotal; // Return true if CurrentProduct equals ProductsInTotal
        }

        //public async Task<bool> ProcessNextProductAndCheckIfDoneAsync(int sessionId)
        //{
        //    using var connection = new SqlConnection(_connectionString);

        //    var updateQuery = "UPDATE Receipts SET CurrentProduct = CurrentProduct + 1 WHERE SessionId = @SessionId;";

        //    await connection.ExecuteAsync(updateQuery, new { SessionId = sessionId });

        //    return productInfo.CurrentProduct == productInfo.ProductsInTotal; // Return true if CurrentProduct equals ProductsInTotal
        //}




        //public async Task<(Product ProcessedProduct, bool IsProcessingComplete)> ProcessNextProductAndCheckIfDoneAsync(int sessionId)
        //{
        //    using var connection = new SqlConnection(_connectionString);
        //    await connection.OpenAsync();

        //    using var transaction = connection.BeginTransaction();

        //    try
        //    {
        //        // Update CurrentProduct
        //        var updateQuery = "UPDATE Receipts SET CurrentProduct = CurrentProduct + 1 OUTPUT INSERTED.CurrentProduct WHERE SessionId = @SessionId;";
        //        var currentProductNumber = await connection.ExecuteScalarAsync<int>(updateQuery, new { SessionId = sessionId }, transaction);

        //        // Get the processed product
        //        var productQuery = @"
        //            SELECT p.*
        //            FROM Products p
        //            JOIN ReceiptProducts rp ON p.ProductId = rp.ProductId
        //            WHERE p.ProductNumber = @ProductNumber AND rp.SessionId = @SessionId;";
        //        var product = await connection.QuerySingleOrDefaultAsync<Product>(productQuery, new { ProductNumber = currentProductNumber, SessionId = sessionId }, transaction);

        //        // Check if processing is complete
        //        var checkQuery = "SELECT ProductsInTotal FROM Receipts WHERE SessionId = @SessionId;";
        //        var productsInTotal = await connection.QuerySingleAsync<int>(checkQuery, new { SessionId = sessionId }, transaction);

        //        transaction.Commit();

        //        return (product, currentProductNumber == productsInTotal);
        //    }
        //    catch
        //    {
        //        transaction.Rollback();
        //        throw;
        //    }
        //}


        public async Task<List<string>> GetPayerNamesBySessionIdAsync(int sessionId)
        {
            var query = @"
                SELECT Name 
                FROM Payers
                WHERE SessionId = @SessionId;";

            using var connection = new SqlConnection(_connectionString);
            var payerNames = await connection.QueryAsync<string>(query, new { SessionId = sessionId });

            return payerNames.ToList();
        }

        public async Task<List<string>> GetGroupNamesBySessionIdAsync(int sessionId)
        {
            var query = @"
                SELECT GroupName 
                FROM Groups
                WHERE SessionId = @SessionId;";

            using var connection = new SqlConnection(_connectionString);
            var payerNames = await connection.QueryAsync<string>(query, new { SessionId = sessionId });

            return payerNames.ToList();
        }

        public async Task<List<int>> GetGroupMembersByGroupNameAndSessionIdAsync(string groupName, int sessionId)
        {
            var query = @"
                SELECT p.PayerId
                FROM Payers p
                INNER JOIN GroupPayers gp ON p.PayerId = gp.PayerId
                INNER JOIN Groups g ON gp.GroupId = g.GroupId
                WHERE g.SessionId = @SessionId AND g.GroupName = @GroupName;
            ";

            using var connection = new SqlConnection(_connectionString);
            var groupMembers = await connection.QueryAsync<int>(query, new { SessionId = sessionId, GroupName = groupName });

            return groupMembers.ToList();
        }

        public async Task<bool> IsPayerLinkedToProductAsync(int payerId, int productId)
        {
            var query = "SELECT COUNT(1) FROM PayerProducts WHERE PayerId = @PayerId AND ProductId = @ProductId;";

            using var connection = new SqlConnection(_connectionString);
            return await connection.ExecuteScalarAsync<bool>(query, new { PayerId = payerId, ProductId = productId });
        }



        public async Task AddPayerToProductAsync(int payerId, int productId)
        {
            var query = "INSERT INTO PayerProducts (PayerId, ProductId) VALUES (@PayerId, @ProductId);";

            using var connection = new SqlConnection(_connectionString);
            await connection.ExecuteAsync(query, new { PayerId = payerId, ProductId = productId });
        }

        public async Task RemovePayerFromProductAsync(int payerId, int productId)
        {
            var query = "DELETE FROM PayerProducts WHERE PayerId = @PayerId AND ProductId = @ProductId;";

            using var connection = new SqlConnection(_connectionString);
            await connection.ExecuteAsync(query, new { PayerId = payerId, ProductId = productId });
        }


        //public async Task<Payer> GetPayerByNameAndSessionIdAsync(string name, int sessionId)
        //{
        //    var query = @"
        //        SELECT *
        //        FROM Payers
        //        WHERE Name = @Name AND SessionId = @SessionId;";

        //    using var connection = new SqlConnection(_connectionString);
        //    return await connection.QuerySingleOrDefaultAsync<Payer>(query, new { Name = name, SessionId = sessionId });
        //}

        public async Task<int> GetPayerIdByNameAndSessionIdAsync(string name, int sessionId)
        {
            var query = @"
                SELECT PayerId
                FROM Payers
                WHERE Name = @Name AND SessionId = @SessionId;";

            using var connection = new SqlConnection(_connectionString);
            return await connection.QuerySingleOrDefaultAsync<int>(query, new { Name = name, SessionId = sessionId });
        }

        public async Task<List<int>> GetAllPayerIdsBySessionIdAsync(int sessionId)
        {
            var query = @"
                SELECT PayerId
                FROM Payers
                WHERE SessionId = @SessionId;";

            using var connection = new SqlConnection(_connectionString);

            var result = await connection.QueryAsync<int>(query, new { SessionId = sessionId });

            return result.ToList();
        }

        public async Task<List<Payer>> GetPayersForProductBySessionAsync(int productId, int sessionId)
        {
            var query = @"
                SELECT p.PayerId, p.Name
                FROM Payers p
                INNER JOIN PayerProducts pp ON p.PayerId = pp.PayerId
                INNER JOIN ReceiptProducts rp ON pp.ProductId = rp.ProductId
                WHERE pp.ProductId = @ProductId AND rp.SessionId = @SessionId;";

            using var connection = new SqlConnection(_connectionString);
            var payers = await connection.QueryAsync<Payer>(query, new
            {
                ProductId = productId,
                SessionId = sessionId
            });

            return payers.ToList();
        }

        public async Task<List<Product>> GetProductsForPayerAsync(int payerId)
        {
            var query = @"
                SELECT p.ProductId, p.Name, p.Cost, rp.DividedCost
                FROM Products p
                INNER JOIN PayerProducts pp ON p.ProductId = pp.ProductId
                WHERE pp.PayerId = @PayerId;";

            using var connection = new SqlConnection(_connectionString);
            return (await connection.QueryAsync<Product>(query, new { PayerId = payerId })).ToList();
        }

        public async Task<List<Payer>> GetProductsForEachPayerAsync(int sessionId)
        {
            var payerQuery = @"
                SELECT p.PayerId, p.Name
                FROM Payers p
                WHERE p.SessionId = @SessionId;";

            var productQuery = @"
                SELECT pr.ProductId, pr.Name, pr.Cost, rp.DividedCost
                FROM PayerProducts pp
                JOIN Products pr ON pp.ProductId = pr.ProductId
                JOIN ReceiptProducts rp ON pr.ProductId = rp.ProductId
                WHERE pp.PayerId = @PayerId AND rp.SessionId = @SessionId;"; 


            using var connection = new SqlConnection(_connectionString);
            var payers = (await connection.QueryAsync<Payer>(payerQuery, new { SessionId = sessionId })).ToList();

            try
            {

                foreach (var payer in payers)
                {
                    var products = (await connection.QueryAsync<Product>(productQuery, new { PayerId = payer.PayerId, SessionId = sessionId })).ToList();
                    payer.Products = products;
                }
            }
            catch (Exception e)
            {
                throw new Exception("Inserting into session table failed: " + e.Message, e);
            }

            return payers;
        }

        public async Task<Dictionary<int, decimal>> GetCostsForPayerAsync(int payerId)
        {
            var query = @"
                SELECT p.ProductId, rp.DividedCost
                FROM Products p
                INNER JOIN PayerProducts pp ON p.ProductId = pp.ProductId
                INNER JOIN ReceiptProducts rp ON p.ProductId = rp.ProductId
                WHERE pp.PayerId = @PayerId;";

            using var connection = new SqlConnection(_connectionString);
            var payerProducts = await connection.QueryAsync(query, new { PayerId = payerId });

            // Calculate the total cost for each product
            var payerCosts = new Dictionary<int, decimal>();
            foreach (var product in payerProducts)
            {
                if (!payerCosts.ContainsKey(product.ProductId))
                    payerCosts[product.ProductId] = 0;
                payerCosts[product.ProductId] += product.DividedCost;
            }

            return payerCosts;
        }

        public async Task CalculateCostsForEachPayerAsync(List<Payer> payers)
        {

            try
            {
                var payerCountQuery = @"
                SELECT COUNT(*) 
                FROM PayerProducts 
                WHERE ProductId = @ProductId;";

                using var connection = new SqlConnection(_connectionString);

                foreach (var payer in payers)
                {
                    foreach (var product in payer.Products)
                    {
                        var payerCount = await connection.ExecuteScalarAsync<int>(payerCountQuery, new { ProductId = product.ProductId });
                        var totalCost = product.Cost + (product.Discounts?.Sum() ?? 0);
                        var dividedCost = totalCost / payerCount;

                        product.DividedCost = dividedCost;
                    }
                }
            }
            catch (Exception e)
            {
                throw new Exception("Calculating costs failed: " + e.Message, e);
            }
        }





        public async Task DeleteAllDataAsync()
        {
            using var connection = new SqlConnection(_connectionString);
            await connection.OpenAsync();

            using var transaction = connection.BeginTransaction();

            try
            {
                // Delete data from tables in reverse order of dependency
                await connection.ExecuteAsync("DELETE FROM GroupPayers;", transaction: transaction);
                await connection.ExecuteAsync("DELETE FROM PayerProducts;", transaction: transaction);
                //await connection.ExecuteAsync("DELETE FROM PayerReceipts;", transaction: transaction);
                await connection.ExecuteAsync("DELETE FROM ReceiptProducts;", transaction: transaction);
                await connection.ExecuteAsync("DELETE FROM ProductDiscounts;", transaction: transaction);
                await connection.ExecuteAsync("DELETE FROM Products;", transaction: transaction);
                await connection.ExecuteAsync("DELETE FROM Payers;", transaction: transaction);
                await connection.ExecuteAsync("DELETE FROM Groups;", transaction: transaction);
                await connection.ExecuteAsync("DELETE FROM Users;", transaction: transaction);
                await connection.ExecuteAsync("DELETE FROM Receipts;", transaction: transaction);

                transaction.Commit();
            }
            catch
            {
                transaction.Rollback();
                throw;
            }
        }
    }
}
