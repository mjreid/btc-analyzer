using System;
using System.Collections;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using NLog;

namespace blockchain_enricher
{
    public class Program
    {
        private const string AddressUrl = "http://192.168.0.16:3000/api/addr/{0}";
        private const string TransactionsUrl = "http://192.168.0.16:3000/api/tx/{0}";

        private static readonly Logger Log = LogManager.GetCurrentClassLogger();
        private static readonly Logger SuspiciousAddressLog = LogManager.GetLogger("LargeAddressLog");
        private static readonly Logger ConsoleOnly = LogManager.GetLogger("ConsoleOnly");
        private static int _originalAddressCount, _originalAnalyzedCount;
        private static string _connectionString;
        private static IList<string> _addressCache;

        static void Main(string[] args)
        {
            try
            {
                _connectionString = ConfigurationManager.ConnectionStrings["BitcoinAnalysis"].ConnectionString;
                Execute();
            }
            catch (Exception e)
            {
                Log.Error("Unexpected error occurred!", e);
            }
        }


        private static void Execute()
        {
            using (var dataStore = new Datastore(_connectionString))
            {
                int addressesAnalyzed = 0;
                _originalAddressCount = dataStore.GetUnanalyzedAddressCount();
                _originalAnalyzedCount = dataStore.GetAnalyzedAddressCount();
                _addressCache = dataStore.GetAllSeenAddresses().ToList();                
                bool stopping = false;

                Console.CancelKeyPress += (o, c) =>
                {
                    c.Cancel = true;
                    Log.Warn("Cancel key pressed! Stopping loop...");
                    stopping = true;
                };


                using (var webClient = new LongerTimeoutWebClient())
                {

                    AddressToAnalyze addressToAnalyze;
                    while (dataStore.GetNextAddressToAnalyze(out addressToAnalyze))
                    {
                        Log.Info("Analyzing address {0}...", addressToAnalyze.Hash);
                       
                        addressesAnalyzed++;
                        var addressData = GetAddressData(dataStore, webClient, addressToAnalyze);       
              
                        if (addressData != null)
                        {
                            try
                            {
                                var sw = new Stopwatch();
                                sw.Start();
                                int newTransaction = dataStore.AddAddressData(addressData);
                                sw.Stop();
                                ConsoleOnly.Debug("Added address data in {0}ms", sw.ElapsedMilliseconds);

                                Log.Info("Found {0} NEW Bitpay transactions out of {1} total.", newTransaction,
                                    addressData.Transactions.Count);
                                AddNewAddresses(dataStore, addressData);
                                dataStore.MarkAsAnalyzed(addressToAnalyze.Hash);
                            }
                            catch (SqlException e)
                            {
                                Log.Warn(string.Format("Address {0} FAILED to persist", addressData.AddressHash), e);
                                dataStore.MarkAsAnalyzed(addressToAnalyze.Hash);
                            }
                        }
                        
                        if (stopping)
                        {
                            Log.Debug("Stop requested, breaking from loop.");
                            LogEndData(dataStore, addressesAnalyzed);
                            break;
                        }
                    }
                }
                LogEndData(dataStore, addressesAnalyzed);
            }
        }

        private static void LogEndData(Datastore dataStore, int addressesAnalyzed)
        {
            Log.Info("Analyzed {0} addresses this run.", addressesAnalyzed);
            Log.Info("Found {0} new addresses to be added to the analysis queue.", dataStore.GetUnanalyzedAddressCount() - _originalAnalyzedCount + addressesAnalyzed);            
        }

        private static void AddNewAddresses(Datastore dataStore, Address addressData)
        {
            var sw = new Stopwatch();
            sw.Start();
            var bitpayAddresses = addressData.GetOtherBitpayAddresses().Distinct().ToList();
            Log.Info("Address {0} had {1} other addresses determined to be Bitpay addresses.", addressData.AddressHash, bitpayAddresses.Count);
            int newCount = 0, repeatedCount = 0;
            foreach (var bitpayAddress in bitpayAddresses)
            {
                if (!_addressCache.Contains(bitpayAddress))
                {
                    Log.Debug("NEW address {0}, adding to analysis queue.", bitpayAddress);
                    _addressCache.Add(bitpayAddress);
                    dataStore.AddAddressToQueue(bitpayAddress);
                    newCount++;
                }
                else
                {
                    Log.Debug("REPEATED address {0}, not adding to queue.", bitpayAddress);
                    repeatedCount++;
                }                
            }
            Log.Info("Found {0} new and {1} repeated addresses in address {2}.", newCount, repeatedCount, addressData.AddressHash);
            sw.Stop();
            ConsoleOnly.Debug("Saved addresses in {0}ms", sw.ElapsedMilliseconds);
        }

        public static Address GetAddressData(Datastore datastore, WebClient webClient, AddressToAnalyze address)
        {
            try
            {
                var sw = new Stopwatch();
                sw.Start();
                string addressJsonData = webClient.DownloadString(string.Format(AddressUrl, address.Hash));
                sw.Stop();
                ConsoleOnly.Debug("Downloaded address data in in {0}ms", sw.ElapsedMilliseconds);
                sw.Reset();

                Address addressObject = JsonConvert.DeserializeObject<Address>(addressJsonData);
                addressObject.IsDirectFind = address.IsDirectFind;
                addressObject.Transactions = new List<Transaction>();

                sw.Start();
                foreach (var transactionHash in addressObject.TransactionHashes)
                {
                    if (!datastore.TransactionExists(transactionHash))
                    {
                        try
                        {
                            string transactionJsonData =
                            webClient.DownloadString(string.Format(TransactionsUrl, transactionHash));
                            var transaction =
                                JsonConvert.DeserializeObject<Transaction>(transactionJsonData);
                            addressObject.Transactions.Add(transaction);
                        }
                        catch (WebException we)
                        {
                            Log.Warn(string.Format("WebException while downloading transaction {0}, skipping", transactionHash), we);
                        }                        
                    }
                    else
                    {
                        ConsoleOnly.Debug("Skipping transaction {0} because it already existed", transactionHash);
                    }
                }
                sw.Stop();
                ConsoleOnly.Debug("Downloaded transaction data in in {0}ms", sw.ElapsedMilliseconds);

                return addressObject;
            }
            catch (JsonSerializationException e)
            {
                Log.Warn(string.Format("Error when deserializing address {0}, skipping.", address), e);
                SuspiciousAddressLog.Warn(string.Format("Could not deserialize {0}", address), e);
                return null;
            }            
        }
    }
}
