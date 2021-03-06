﻿using System;
using System.IO;
using System.Linq;
using Cachalot.Linq;
using Client.Interface;
using Client.Queries;
using NUnit.Framework;
using UnitTests.TestData;
using UnitTests.TestData.Events;

namespace UnitTests
{
    [TestFixture]
    public class TestFixtureLinq
    {
        [OneTimeSetUp]
        public void RunBeforeAnyTests()
        {
            Environment.CurrentDirectory = TestContext.CurrentContext.TestDirectory;
            Directory.SetCurrentDirectory(TestContext.CurrentContext.TestDirectory);
        }


        [Test]
        public void Between_operator_optimization()
        {
            ClientConfig config = new ClientConfig();
            config.LoadFromFile("inprocess_config.xml");

            using (var connector = new Connector(config))
            {
                var trades = connector.DataSource<Trade>();

                var today = DateTime.Today;
                var tomorrow = today.AddDays(1);
                

                var q = trades.PredicateToQuery(t =>
                    t.ValueDate >= today && t.ValueDate <= tomorrow);

                Assert.IsTrue(q.Elements.Single().Elements.Single().Operator == QueryOperator.Btw, "BETWEEN optimization not working");

                Console.WriteLine(q.ToString());
            }
        }


        [Test]
        public void Check_contains_operator_parsing()
        {
            ClientConfig config = new ClientConfig();
            config.LoadFromFile("inprocess_config.xml");

            using (var connector = new Connector(config))
            {
                var trades = connector.DataSource<Trade>();
                
                var q = trades.PredicateToQuery(t =>
                    t.Accounts.Contains(111));

              
                Assert.IsTrue(q.Elements.Single().Elements.Single().Operator == QueryOperator.In, "IN optimization not working");

                Console.WriteLine(q.ToString());

                // check with two CONTAINS
                q = trades.PredicateToQuery(t =>
                    t.Accounts.Contains(111) && t.Accounts.Contains(222));

                
                Console.WriteLine(q.ToString());
            }
        }

        [Test]
        public void Linq_extension()
        {
            ClientConfig config = new ClientConfig();
            config.LoadFromFile("inprocess_config.xml");

            using (var connector = new Connector(config))
            {
                var trades = connector.DataSource<Trade>();

                QueryExecutor.Probe(query =>
                {
                    Assert.AreEqual("something funny", query.FullTextSearch);

                    Console.WriteLine(query);
                });
                
                var result = trades.Where(t=>t.Folder == "TF").FullTextSearch("something funny").ToList();

                // disable the monitoring
                QueryExecutor.Probe(null);
                
            }
        }



        [Test]
        public void Linq_with_contains_extension_on_scalar_field()
        {
            ClientConfig config = new ClientConfig();
            config.LoadFromFile("inprocess_config.xml");

            using (var connector = new Connector(config))
            {

                DataSource<Trade> dataSource = connector.DataSource<Trade>();

                dataSource.PutMany(new[]
                {
                    new Trade(1, 5465, "TATA", DateTime.Now.Date, 150),
                    new Trade(3, 5467, "TATA", DateTime.Now.Date.AddDays(-1), 150),
                    new Trade(2, 5466, "TOTO", DateTime.Now.Date, 200),
                    new Trade(4, 5476, "TITO", DateTime.Now.Date, 250)
                });


                {
                    var folders = new[] {"TATA", "TOTO"};

                    var list = dataSource.Where(t => folders.Contains(t.Folder)).ToList();

                    Assert.AreEqual(3, list.Count);
                }

                // with strings
                {
                    var folders = new[] {"TATA", "TOTO"};

                    var list = dataSource.Where(t => folders.Contains(t.Folder) && t.ValueDate < DateTime.Today).ToList();

                    Assert.AreEqual(1, list.Count);
                }

                // with ints
                {
                    var ids = new[] {1, 2, 3};

                    var list = dataSource.Where(t => ids.Contains(t.Id)).ToList();

                    Assert.AreEqual(3, list.Count);
                }

                // with convertors (dates are internally concverted to ints
                {
                    var dates = new[] {DateTime.Today};
                    var list = dataSource.Where(t => dates.Contains(t.ValueDate)).ToList();

                    Assert.AreEqual(3, list.Count);
                }
            }
        }


        [Test]
        public void Linq_with_contains_extension_on_vector_field()
        {


            ClientConfig config = new ClientConfig();
            config.LoadFromFile("inprocess_config.xml");

            using (var connector = new Connector(config))
            {

                DataSource<Trade> dataSource = connector.DataSource<Trade>();

                dataSource.PutMany(new[]
                {
                    new Trade(1, 5465, "TATA", DateTime.Now.Date, 150) {Accounts = {44, 45, 46}},
                    new Trade(3, 5467, "TATA", DateTime.Now.Date.AddDays(-1), 150)
                    {
                        FixingDates = {DateTime.Today, DateTime.Today.AddMonths(3)}
                    },
                    new Trade(2, 5466, "TOTO", DateTime.Now.Date, 200) {Accounts = {44, 48, 49}},
                    new Trade(4, 5476, "TITO", DateTime.Now.Date, 250)
                    {
                        FixingDates = {DateTime.Today, DateTime.Today.AddMonths(6)}
                    }
                });


                {
                    var list = dataSource.Where(t => t.FixingDates.Contains(DateTime.Today)).ToList();

                    Assert.AreEqual(2, list.Count);
                }

                {
                    var list = dataSource.Where(t => t.Accounts.Contains(44)).ToList();

                    Assert.AreEqual(2, list.Count);
                }

                {
                    var list = dataSource.Where(t => t.Accounts.Contains(48)).ToList();

                    Assert.AreEqual(1, list.Count);
                }
            }
        }

        [Test]
        public void Simple_linq_expression()
        {

            ClientConfig config = new ClientConfig();
            config.LoadFromFile("inprocess_config.xml");

            using (var connector = new Connector(config))
            {

                DataSource<Trade> dataSource = connector.DataSource<Trade>();

                dataSource.PutMany(new[]
                {
                    new Trade(1, 5465, "TATA", DateTime.Now.Date, 150),
                    new Trade(3, 5467, "TATA", DateTime.Now.Date.AddDays(-1), 150),
                    new Trade(2, 5466, "TOTO", DateTime.Now.Date, 200)
                });


                {
                    var t1 = dataSource.FirstOrDefault(t => t.Folder == "TOTO");

                    Assert.IsNotNull(t1);
                    Assert.AreEqual(2, t1.Id);
                }

                {
                    var t1 = dataSource.FirstOrDefault(t => t.Folder == "TATA" && t.ValueDate < DateTime.Today);

                    Assert.IsNotNull(t1);
                    Assert.AreEqual(3, t1.Id);
                }

                {
                    var list = dataSource.Where(t => t.Folder == "TATA" && t.ValueDate <= DateTime.Today).ToList();
                    Assert.AreEqual(list.Count, 2);
                    Assert.IsTrue(list.All(t => t.Folder == "TATA"));
                }

                {
                    var list = dataSource
                        .Where(t => t.Folder == "TATA" && t.ValueDate <= DateTime.Today || t.Folder == "TOTO").ToList();
                    Assert.AreEqual(list.Count, 3);
                }


                // check if time values can be compared with date values
                {
                    var list = dataSource
                        .Where(t => t.ValueDate > DateTime.Now.AddDays(-1) ).ToList();
                    Assert.AreEqual(list.Count, 2);
                }
            }
        }

        [Test]
        public void Polymorphic_collection()
        {

            ClientConfig config = new ClientConfig();
            config.LoadFromFile("inprocess_config.xml");

            using (var connector = new Connector(config))
            {

                DataSource<ProductEvent> dataSource = connector.DataSource<ProductEvent>();

                dataSource.PutMany(new ProductEvent[]
                {
                    new FixingEvent(1, "AXA", 150, "EQ-256"),
                    new FixingEvent(2, "TOTAL", 180, "IRD-400"),
                    new Increase(3, 180, "EQ-256")
                });


                var events = dataSource.Where(evt => evt.DealId == "EQ-256").ToList();

                Assert.AreEqual(2, events.Count);

                events = dataSource.Where(evt => evt.EventType == "FIXING").ToList();

                Assert.AreEqual(2, events.Count);


                // delete one fixing event
                dataSource.Delete(events[0]);

                events = dataSource.Where(evt => evt.EventType == "FIXING").ToList();

                Assert.AreEqual(1, events.Count);

                dataSource.Put(new Increase(4, 180, "EQ-256"));

                events = dataSource.Where(evt => evt.EventType == "INCREASE").ToList();

                Assert.AreEqual(2, events.Count);
            }

           
        }

        
        
    }
}