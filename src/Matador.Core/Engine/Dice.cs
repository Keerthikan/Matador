namespace Matador.Core.Engine;

public interface IDice
{
    (int Die1, int Die2) Roll();
}

public class StandardDice : IDice
{
    private readonly Random _random;

    public StandardDice(Random? random = null)
    {
        _random = random ?? new Random();
    }

    public (int Die1, int Die2) Roll()
    {
        return (_random.Next(1, 7), _random.Next(1, 7));
    }
}
