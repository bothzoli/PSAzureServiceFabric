using Microsoft.ServiceFabric.Services.Remoting;
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;

namespace ECommerce.ProductCatalog.Model
{
    public interface IProductCatalogService : IService
    {
        Task<Product> GetProductAsync(Guid productId);

        Task<Product[]> GetAllProducts();

        Task AddProductAsync(Product product);
    }
}
