using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

namespace blockchain_enricher
{
    public class Datastore : IDisposable
    {
        private bool _disposed;
        private readonly SqlConnection _sqlConnection;

        public Datastore(string connectionString)
        {
            _sqlConnection = new SqlConnection(connectionString);
            _sqlConnection.Open();
        }

        ~Datastore()
        {
            Dispose(false);
        }

        public void Dispose()
        {
            GC.SuppressFinalize(this);
            Dispose(true);
        }

        private void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    if (_sqlConnection != null)
                        _sqlConnection.Dispose();
                    _disposed = true;
                }
            }
        }

        public bool ContainsAddress(string address)
        {
            const string addressCountQuery = "SELECT COUNT(*) FROM [AddressQueue] WHERE AddressHash = @AddressHash";
            var command = CreateCommand(addressCountQuery);
            command.Parameters.Add(new SqlParameter("AddressHash", address));
            return (int)command.ExecuteScalar() != 0;
        }

        public int AddAddressData(Address addressData)
        {
            int newTransactions = 0;
            using (var tx = _sqlConnection.BeginTransaction())
            {
                const string insertAddressDataQuery =
                    "INSERT INTO [Address] (AddressHash, NumberOfTransactions, AmountReceived, AmountSent, FinalBalance, EarliestTransactionDate, IsDirectFind) VALUES " +
                    "(@AddressHash, @NumberOfTransactions, @AmountReceived, @AmountSent, @FinalBalance, @EarliestTransactionDate, @IsDirectFind)";

                foreach (var transaction in addressData.Transactions)
                {
                    if (AddTransaction(addressData, transaction, tx))
                    {
                        newTransactions++;
                    }
                }

                var command = CreateCommand(insertAddressDataQuery, tx);
                command.Parameters.Add(new SqlParameter("AddressHash", addressData.AddressHash));
                command.Parameters.Add(new SqlParameter("NumberOfTransactions", addressData.NumberOfTransactions));
                command.Parameters.Add(new SqlParameter("AmountReceived", addressData.TotalReceived));
                command.Parameters.Add(new SqlParameter("AmountSent", addressData.TotalSent));
                command.Parameters.Add(new SqlParameter("FinalBalance", addressData.FinalBalance));
                if (addressData.GetEarliestTransactionDate() != null)
                    command.Parameters.Add(new SqlParameter("EarliestTransactionDate",
                        addressData.GetEarliestTransactionDate()));
                else
                    command.Parameters.Add(new SqlParameter("EarliestTransactionDate", DBNull.Value));
                command.Parameters.Add(new SqlParameter("IsDirectFind", addressData.IsDirectFind));
                command.ExecuteNonQuery();

                tx.Commit();
            }

            return newTransactions;
        }

        private bool AddTransaction(Address parentAddress, Transaction transaction, SqlTransaction tx)
        {
            if (TransactionExists(transaction.Hash, tx))
                return false;

            const string insertTransactionQuery =
                "INSERT INTO [Transaction] (TransactionHash, IsBitpayTransaction, TransactionDate, Fees, ValueIn, ValueOut) " +
                    "VALUES (@TransactionHash, @IsBitpayTransaction, @TransactionDate, @Fees, @ValueIn, @ValueOut)";
            const string insertTransactionInputQuery =
                "INSERT INTO [TransactionInput] (TransactionHash, AddressHash, Amount) VALUES (@TransactionHash, @AddressHash, @Amount)";
            const string insertTransactionOutputQuery =
                "INSERT INTO [TransactionOutput] (TransactionHash, AddressHash, Amount) VALUES (@TransactionHash, @AddressHash, @Amount)";
            foreach (var input in transaction.Inputs)
            {
                var command = CreateCommand(insertTransactionInputQuery, tx);
                command.Parameters.Add(new SqlParameter("TransactionHash", transaction.Hash));
                command.Parameters.Add(new SqlParameter("AddressHash", input.AddressHash));
                command.Parameters.Add(new SqlParameter("Amount", input.Value));                                
                command.ExecuteNonQuery();
            }
            foreach (var output in transaction.Outputs)
            {
                var command = CreateCommand(insertTransactionOutputQuery, tx);
                command.Parameters.Add(new SqlParameter("TransactionHash", transaction.Hash));
                command.Parameters.Add(new SqlParameter("AddressHash", output.Address));
                command.Parameters.Add(new SqlParameter("Amount", output.Value));
                command.ExecuteNonQuery();
            }

            var txCommand = CreateCommand(insertTransactionQuery, tx);
            txCommand.Parameters.Add(new SqlParameter("TransactionHash", transaction.Hash));
            txCommand.Parameters.Add(new SqlParameter("IsBitpayTransaction",
                transaction.IsAggregationTransaction(parentAddress.AddressHash)));
            txCommand.Parameters.Add(new SqlParameter("TransactionDate",
                transaction.Time));
            txCommand.Parameters.Add(new SqlParameter("Fees", transaction.Fees));
            txCommand.Parameters.Add(new SqlParameter("ValueIn", transaction.ValueIn));
            txCommand.Parameters.Add(new SqlParameter("ValueOut", transaction.ValueOut));
            txCommand.ExecuteNonQuery();

            return transaction.IsAggregationTransaction(parentAddress.AddressHash);
        }

        public bool TransactionExists(string hash, SqlTransaction tx = null)
        {
            const string countQuery = "SELECT COUNT(*) FROM [Transaction] WHERE TransactionHash = @TransactionHash";
            var command = CreateCommand(countQuery, tx);
            command.Parameters.Add(new SqlParameter("TransactionHash", hash));
            return (int) command.ExecuteScalar() != 0;
        }

        public int GetAnalyzedAddressCount()
        {
            const string countQuery = "SELECT COUNT(*) FROM [Address]";
            return (int)CreateCommand(countQuery).ExecuteScalar();
        }

        public int GetUnanalyzedAddressCount()
        {
            const string countQuery = "SELECT COUNT(*) FROM [AddressQueue] WHERE [IsProcessed] = 0";
            return (int)CreateCommand(countQuery).ExecuteScalar();
        }

        public void MarkAsAnalyzed(string hash)
        {
            string addressQuery =
                string.Format("UPDATE [AddressQueue] SET [IsProcessed] = 1 WHERE [AddressHash] = '{0}'", hash);
            CreateCommand(addressQuery).ExecuteNonQuery();
        }

        public bool GetNextAddressToAnalyze(out AddressToAnalyze addressToAnalyze)
        {
            string selectCommand = "SELECT TOP(1) [AddressHash], [IsDirectFind] FROM [AddressQueue] WHERE [IsProcessed] = 0";
            var command = CreateCommand(selectCommand);
            using (var reader = command.ExecuteReader())
            {                           
                bool hasRow = reader.Read();
                if (!hasRow)
                {
                    addressToAnalyze = null;
                    return false;
                }

                string hash = (string) reader["AddressHash"];
                bool isDirectFind = (bool) reader["IsDirectFind"];
                addressToAnalyze = new AddressToAnalyze
                {
                    Hash = hash,
                    IsDirectFind = isDirectFind
                };
            }

            return true;
        }

        public void AddAddressToQueue(string address)
        {
            const string sqlString =
                "INSERT INTO [AddressQueue] ([AddressHash], [IsDirectFind], [DateAdded], [IsProcessed]) VALUES (@AddressHash, 0, @DateAdded, 0)";
            var command = CreateCommand(sqlString);
            command.Parameters.Add(new SqlParameter("AddressHash", address));
            command.Parameters.Add(new SqlParameter("DateAdded", DateTime.UtcNow));

            command.ExecuteNonQuery();
        }

        private IDbCommand CreateCommand(string query, SqlTransaction transaction = null)
        {
            var dbCommand = _sqlConnection.CreateCommand();
            dbCommand.CommandText = query;
            dbCommand.Transaction = transaction;
            dbCommand.CommandTimeout = 120;
            return dbCommand;
        }

        public IEnumerable<string> GetAllSeenAddresses()
        {
            const string sqlString =
                "SELECT [AddressHash] FROM [AddressQueue]";
            var command = CreateCommand(sqlString);
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                {
                    yield return (string)reader["AddressHash"];
                }
            }
        }  
    }

    public class AddressToAnalyze
    {
        public string Hash { get; set; }
        public bool IsDirectFind { get; set; }
    }
}