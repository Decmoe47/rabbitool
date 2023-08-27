namespace Rabbitool.Common.Exception;

public class InvalidPropertyTypeException : System.Exception
{
    public InvalidPropertyTypeException(string message) : base(message)
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

public class UninitializedException : System.Exception
{
    public UninitializedException(string message) : base(message)
    {
    }
}

public class UnsupportedException : System.Exception
{
    public UnsupportedException(string message) : base(message)
    {
    }
}