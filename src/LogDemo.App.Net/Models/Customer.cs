namespace LogDemo.App.Net.Models;

public sealed class Customer
{
    public Customer(int id, string name, string email, string city)
    {
        Id = id;
        Name = name;
        Email = email;
        City = city;
    }

    public int Id { get; }

    /// <summary>Personal data: shown in the UI, never written to the log.</summary>
    public string Name { get; }

    /// <summary>Personal data: shown in the UI, never written to the log.</summary>
    public string Email { get; }

    public string City { get; }
}
