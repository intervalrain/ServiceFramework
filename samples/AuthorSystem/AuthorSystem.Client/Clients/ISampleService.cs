namespace AuthorSystem.Client.Clients;

public interface ISampleService
{
    Task<int> Square(int num);
    Task<int> Sqrt(int num);
    Task<int> Double(int num);
    Task<bool> Prime(int num);
}