﻿using System;
using System.Diagnostics;
using System.Linq;
using Cachalot.Linq;
using Client.Core;
using Client.Interface;

namespace Accounts
{
    internal class Program
    {

        const int TestIterations = 1000;
        
        private static void Main(string[] args)
        {
            

            var config = new ClientConfig();
            config.LoadFromFile("two_nodes_cluster.xml");
            
            try
            {
                // quick test with a cluster of two nodes
                using (var connector = new Connector(config))
                {
                    Console.WriteLine();
                    Console.WriteLine("test with a cluster of two servers");
                    Console.WriteLine("-------------------------------------");
                    
                    PerfTest(connector);
                }
            }
            catch (CacheException e)
            {
                Console.WriteLine(e.Message);
            }

            try
            {
                config = new ClientConfig
                {
                    Servers = { new ServerConfig { Host = "127.0.0.1", Port = 4848 } }
                };

                // quick test with one external server
                using (var connector = new Connector(config))
                {
                    Console.WriteLine();
                    Console.WriteLine("test with one server");
                    Console.WriteLine("-------------------------------------");

                    PerfTest(connector);
                }
            }
            catch (CacheException e)
            {
                Console.WriteLine(e.Message);
            }

            try
            {
                // quick test with in-process server
                using (var connector = new Connector(new ClientConfig { IsPersistent = true }))
                {
                   
                    Console.WriteLine();
                    Console.WriteLine("test with in-process server");
                    Console.WriteLine("---------------------------");
                    PerfTest(connector);
                }
            }
            catch (CacheException e)
            {
                Console.WriteLine(e.Message);
            }
        }

        private static void PerfTest(Connector connector)
        {
            
            TransactionStatistics.Reset();
            // first delete all data to start with a clean database
            connector.AdminInterface().DropDatabase();
            
            var accounts = connector.DataSource<Account>();

            
            // create the accounts
            for (int i = 0; i < TestIterations; i++)
            {
                accounts.Put(new Account { Id = i, Balance = 1000 });
            }



            var tids = connector.GenerateUniqueIds("transfer_id", TestIterations);

            var rand = new Random();

            int failed = 0;

            var sw = new Stopwatch();

            sw.Start();

            for (int i = 0; i < TestIterations; i++)
            {
                var src = rand.Next(1, 1000);
                var dst = src + 1 > 999? src-1:src+1;

                var transferred = rand.Next(1, 1000);

                var srcAccount = accounts[src];
                var dstAccount = accounts[dst];

                srcAccount.Balance -= transferred;
                dstAccount.Balance += transferred;

                var transaction = connector.BeginTransaction();
                transaction.UpdateIf(srcAccount, account => account.Balance >= transferred);
                transaction.Put(dstAccount);
                transaction.Put(new MoneyTransfer
                {
                    Amount = transferred,
                    Date = DateTime.Today,
                    SourceAccount = src,
                    DestinationAccount = dst,
                    Id = tids[i]
                });

                try
                {
                    transaction.Commit();
                }
                catch (CacheException e)
                {
                    if (e.IsTransactionException)
                    {
                        failed++;
                    }
                }


            }

            sw.Stop();

            Console.WriteLine($"{TestIterations} transactions took {sw.ElapsedMilliseconds} milliseconds. Rolled back transactions: {failed}. ");
            var stats = TransactionStatistics.AsString();

            Console.WriteLine(stats);

            // check that all accounts have positive balance
            var negatives = accounts.Count(acc => acc.Balance < 0);

            if (negatives > 0)
            {
                Console.WriteLine("Some accounts have negative balance");
            }
            else
            {
                Console.WriteLine("All accounts have positive balance");
            }


        }

    }
}