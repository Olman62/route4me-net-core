﻿using System.Linq;
using Xunit;
using Xunit.Abstractions;
using Route4MeDB.Route4MeDbLibrary;
using Route4MeDB.Infrastructure.Data;
using Route4MeDB.ApplicationCore.Entities.OrderAggregate;
using Microsoft.Extensions.Configuration;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace Route4MeDB.FunctionalTests.LocalDb
{
    public class DatabaseOrdersFixture : DatabaseFixtureBase
    {
        public DatabaseOrdersFixture()
        {
            Route4MeDbManager.DatabaseProvider = DatabaseProviders.LocalDb;

            GetDbContext(DatabaseProviders.LocalDb);

            _orderRepository = new OrderRepository(_route4meDbContext);
        }

        public OrderRepository _orderRepository;
    }

    public class OrderTests : FactAttribute, IClassFixture<DatabaseOrdersFixture>
    {
        private readonly ITestOutputHelper _output;
        DatabaseOrdersFixture fixture;
        public IConfigurationRoot Configuration { get; }

        public OrderTests(DatabaseOrdersFixture fixture, ITestOutputHelper output)
        {
            this.fixture = fixture;

            var curPath = Directory.GetCurrentDirectory();
            var configBuilder = new ConfigurationBuilder()
               .SetBasePath(curPath)
               .AddJsonFile("appsettings.json", optional: true);
            var config = configBuilder.Build();

            Route4MeDbManager.DatabaseProvider = DatabaseProviders.LocalDb;
            fixture.r4mdbManager = new Route4MeDbManager(config);

            fixture._route4meDbContext = fixture.r4mdbManager.Route4MeContext;

            fixture._orderRepository = new OrderRepository(fixture._route4meDbContext);
            _output = output;
        }

        [Fact]
        public async Task GetOrdersTest()
        {
            var orderDbIDs = new List<int>();

            var firstOrder = fixture.orderBuilder.WithDefaultValues();
            await fixture._route4meDbContext.Orders.AddAsync(firstOrder);

            int firstOrderDbId = firstOrder.OrderDbId;
            orderDbIDs.Add(firstOrderDbId);

            var secondOrder = fixture.orderBuilder.WithCustomData();
            await fixture._route4meDbContext.Orders.AddAsync(secondOrder);

            int secondOrderDbId = secondOrder.OrderDbId;
            orderDbIDs.Add(secondOrderDbId);

            await fixture._route4meDbContext.SaveChangesAsync();

            var orders = fixture._route4meDbContext.AddressBookContacts.Skip(0).Take(2);

            var linqOrders = fixture._route4meDbContext.Orders
                .Where(x => orderDbIDs.Contains(x.OrderDbId)).ToList<Order>();

            foreach (var linqOrder in linqOrders)
            {
                Assert.Contains<int>(linqOrder.OrderDbId, orders.Select(x => x.AddressDbId));
            }
        }

        [Fact]
        public async Task GetExistingOrderAsync()
        {
            var firstOrder = fixture.orderBuilder.WithDefaultValues();
            await fixture._route4meDbContext.Orders.AddAsync(firstOrder);

            await fixture._route4meDbContext.SaveChangesAsync();

            int createdOrderDbId = firstOrder.OrderDbId;

            var orderFromRepo = await fixture._orderRepository
                .GetOrderByIdAsync(createdOrderDbId);

            var linqOrder = fixture._route4meDbContext.Orders
                .Where(x => createdOrderDbId == x.OrderDbId).FirstOrDefault();

            Assert.Equal(createdOrderDbId, orderFromRepo.OrderDbId);
            Assert.Equal(firstOrder.EXT_FIELD_first_name, orderFromRepo.EXT_FIELD_first_name);
            Assert.Equal(firstOrder.EXT_FIELD_last_name, orderFromRepo.EXT_FIELD_last_name);
            Assert.Equal(firstOrder.EXT_FIELD_first_name, linqOrder.EXT_FIELD_first_name);
            Assert.Equal(firstOrder.EXT_FIELD_last_name, linqOrder.EXT_FIELD_last_name);
        }

        [Fact]
        public async Task UpdateOrderAsync()
        {
            var order = fixture.orderBuilder.WithDefaultValues();
            await fixture._route4meDbContext.Orders.AddAsync(order);

            await fixture._route4meDbContext.SaveChangesAsync();

            order.EXT_FIELD_first_name = "Peter Modified";
            order.EXT_FIELD_last_name = "Newman Modified";

            var updatedOrder = await fixture._orderRepository
                .UpdateOrderAsync(order.OrderDbId, order);

            await fixture._route4meDbContext.SaveChangesAsync();

            var linqOrder = fixture._route4meDbContext.Orders
                .Where(x => x.OrderDbId == updatedOrder.OrderDbId).FirstOrDefault();

            Assert.Equal<Order>(updatedOrder, linqOrder);
        }
    }
}
