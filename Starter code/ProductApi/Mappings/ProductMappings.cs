using ProductApi.Models;
using ProductApi.Models.Dtos;

namespace ProductApi.Mappings;

public static class ProductMappings
{
    public static ProductResponse ToResponse(this Product product)
    {
        return new ProductResponse
        {
            Id = product.Id,
            Name = product.Name,
            Price = product.Price,
            Description = product.Description,
            CreatedAt = product.CreatedAt,
            UpdatedAt = product.UpdatedAt
        };
    }

    public static Product ToEntity(this CreateProductRequest request)
    {
        return new Product
        {
            Name = request.Name,
            Price = request.Price,
            Description = request.Description
        };
    }

    public static void UpdateEntity(this UpdateProductRequest request, Product product)
    {
        product.Name = request.Name;
        product.Price = request.Price;
        product.Description = request.Description;
    }
}
