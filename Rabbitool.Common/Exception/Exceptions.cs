namespace Rabbitool.Common.Exception;

public class InvalidPropertyTypeException(string message) : System.Exception(message);

public class NotFoundException(string message) : System.Exception(message);

public class JsonUnmarshalException(string message) : System.Exception(message);

public class UninitializedException(string message) : System.Exception(message);

public class UnsupportedException(string message) : System.Exception(message);

public class LoadConfigsException(string message) : System.Exception(message);