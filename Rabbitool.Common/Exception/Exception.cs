namespace Rabbitool.Common.Exception;

public class InvalidProperityTypeException : System.Exception
{
    public InvalidProperityTypeException(string message) : base(message)
    {
    }
}

public class NotFoundException : System.Exception
{
    public NotFoundException(string message) : base(message)
    {
    }
}

public class JsonUnmarshalException : System.Exception
{
    public JsonUnmarshalException(string message) : base(message)
    {
    }
}
