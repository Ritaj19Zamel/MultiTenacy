namespace MultiTenacy.Services
{
    public interface IProductService
    {
        Task<Product?> CreateProductAsync(Product product);
        Task<Product?> GetProductByIdAsync(int id);
        Task<Product?> UpdateProductAsync(Product product);
        Task<bool> DeleteProductAsync(int id);
        Task<IReadOnlyList<Product>> GetAllProductsAsync();
    }
}
