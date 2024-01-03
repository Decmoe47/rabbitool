namespace Rabbitool.Repository;

public class DataBaseRecordNotExistException : Exception
{
    public DataBaseRecordNotExistException(string message) : base(message)
    {
    }
}