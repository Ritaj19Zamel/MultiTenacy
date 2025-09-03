namespace MultiTenacy.Dtos
{
    public class CreateProductDto
    {
        public string Name { get; set; } = null!;
        public int Rate { get; set; }
        public decimal Price { get; set; }
        public decimal Stock { get; set; }
        public string Description { get; set; } = null!;

    }
}
