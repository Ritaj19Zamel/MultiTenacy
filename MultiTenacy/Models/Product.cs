
namespace MultiTenacy.Models
{
    public class Product : IMustHaveTenant
    {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public int Rate { get; set; }
        public decimal Price { get; set; }
        public decimal Stock { get; set; }
        public string Description { get; set; } = null!;
        public string TenantId { get; set; } = null!;
    }
}
