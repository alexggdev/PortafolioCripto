namespace CriptoPortfolio.Domain.Entities;

public class Portfolio
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public string Name { get; set; } = "";
    public DateTime CreatedAtUtc { get; set; }

    public List<Transaction> Transactions { get; set; } = new();
}
