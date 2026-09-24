namespace Matador.Core.Domain;

public class Player
{
    public string Id { get; }
    public string Name { get; }
    public int Balance { get; private set; }
    public int Position { get; set; }
    public bool IsInJail { get; set; }
    public int TurnsInJail { get; set; }
    public int GetOutOfJailCards { get; set; }
    public bool IsBankrupt { get; set; }
    public List<OwnableSpace> OwnedProperties { get; } = new();

    public Player(string id, string name, int startingBalance = 30000)
    {
        Id = id;
        Name = name;
        Balance = startingBalance;
        Position = 0;
    }

    public void AddMoney(int amount)
    {
        if (amount < 0) throw new ArgumentException("Amount must be positive", nameof(amount));
        Balance += amount;
    }

    public bool DeductMoney(int amount)
    {
        if (amount < 0) throw new ArgumentException("Amount must be positive", nameof(amount));
        Balance -= amount;
        return Balance >= 0;
    }

    public int CalculateTotalNetWorth()
    {
        int propertyValue = 0;
        foreach (var prop in OwnedProperties)
        {
            if (prop.IsMortgaged)
            {
                propertyValue += prop.MortgageValue;
            }
            else
            {
                propertyValue += prop.Price;
                if (prop is StreetSpace street)
                {
                    propertyValue += street.HouseCount * street.HousePrice;
                }
            }
        }
        return Balance + propertyValue;
    }

    public override string ToString() => $"{Name} (Saldo: {Balance:N0} kr., Felt: {Position})";
}
