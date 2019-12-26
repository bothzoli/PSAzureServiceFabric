using System;
using System.Collections.Generic;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ECommerce.CheckoutService.Model;
using ECommerce.ProductCatalog.Model;
using Microsoft.ServiceFabric.Actors;
using Microsoft.ServiceFabric.Actors.Client;
using Microsoft.ServiceFabric.Data;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Client;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.Client;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Client;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using UserActor.Interfaces;

namespace ECommerce.CheckoutService
{
    /// <summary>
    /// An instance of this class is created for each service replica by the Service Fabric runtime.
    /// </summary>
    internal sealed class CheckoutService : StatefulService, ICheckoutService
    {
        public CheckoutService(StatefulServiceContext context)
            : base(context)
        { }

        public async Task<CheckoutSummary> CheckoutAsync(string userId)
        {
            var result = new CheckoutSummary();
            result.Date = DateTime.UtcNow;
            result.Products = new List<CheckoutProduct>();

            IUserActor userActor = GetUserActor(userId);
            BasketItem[] basket = await userActor.GetBasket();

            IProductCatalogService catalogService = GetProductCatalogService();

            foreach(BasketItem basketLine in basket)
            {
                Product product = await catalogService.getProductAsync(basketLine.ProductId);
                var checkoutProduct = new CheckoutProduct
                {
                    Product = product,
                    Price = product.Price,
                    Quantity = basketLine.Quantity
                };
                result.Products.Add(checkoutProduct);
            }
            result.TotalPrice = result.Products.Sum(p => p.Price * p.Quantity);

            await userActor.ClearBasket();

            await AddToHistoryAsync(result);

            return result;
        }

        private IProductCatalogService GetProductCatalogService()
        {
            var proxyFactory = new ServiceProxyFactory(
                c => new FabricTransportServiceRemotingClientFactory());

            return proxyFactory.CreateServiceProxy<IProductCatalogService>(
                new Uri("fabric:/ECommerce/ECommerce.ProductCatalog"),
                new ServicePartitionKey(0));
        }

        private IUserActor GetUserActor(string userId)
        {
            return ActorProxy.Create<IUserActor>(
                new ActorId(userId),
                new Uri("fabric:/ECommerce/UserActorService"));
        }

        public Task<CheckoutSummary[]> GetOrderHistoryAsync(string userId)
        {
            throw new NotImplementedException();
        }

        private async Task AddToHistoryAsync(CheckoutSummary checkout)
        {
            IReliableDictionary<DateTime, CheckoutSummary> history =
                await StateManager.GetOrAddAsync<IReliableDictionary<DateTime, CheckoutSummary>>("history");

            using (ITransaction tx = StateManager.CreateTransaction())
            {
                await history.AddAsync(tx, checkout.Date, checkout);

                await tx.CommitAsync();
            }
        }

        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            return new[]
            {
                new ServiceReplicaListener(context =>
                    new FabricTransportServiceRemotingListener(context, this))
            };
        }
    }
}
