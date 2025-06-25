using System.Text.RegularExpressions;

namespace BookStore.Domain.Specifications;

public static class BookSpecifications
{
    public static bool HasValidTitle(string title) => 
        !string.IsNullOrWhiteSpace(title) && title.Length <= 200;
        
    public static bool HasValidAuthor(string author) => 
        !string.IsNullOrWhiteSpace(author) && author.Length <= 100;
        
    public static bool HasValidISBN(string isbn) => 
        !string.IsNullOrWhiteSpace(isbn) && 
        Regex.IsMatch(isbn, @"^(?:\d{10}|\d{13})$");
        
    public static bool HasValidPrice(decimal price) => 
        price > 0 && price <= 999999.99m;
        
    public static bool HasValidStock(int stock) => 
        stock >= 0;
}