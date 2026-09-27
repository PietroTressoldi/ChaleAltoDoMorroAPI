namespace ChaleAltoDoMorroAPI.Interfaces
{
    public interface IClock
    {

        DateOnly Today();

        DateTime Now();
    }
}
