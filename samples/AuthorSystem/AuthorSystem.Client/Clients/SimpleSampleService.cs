namespace AuthorSystem.Client.Clients;

public class SimpleSampleService : ISampleService
{
    public Task<int> Double(int num)
    {
        return Task.FromResult(num + num);
    }

    public Task<bool> Prime(int num)
    {
        if (num < 0 || num >= 100) throw new ArgumentOutOfRangeException("Please enter num between 1-99");
        var set = new HashSet<int>([2, 3, 5, 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47, 53, 57, 59, 61, 67, 71, 73, 79, 83, 89, 97]);
        return Task.FromResult(set.Contains(num));
    }

    public Task<int> Sqrt(int num)
    {
        return Task.FromResult((int)Math.Sqrt(num));
    }

    public Task<int> Square(int num)
    {
        return Task.FromResult(num * num);
    }
}