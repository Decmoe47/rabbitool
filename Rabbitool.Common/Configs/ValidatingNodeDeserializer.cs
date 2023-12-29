using System.ComponentModel.DataAnnotations;
using YamlDotNet.Core;
using YamlDotNet.Serialization;

namespace Rabbitool.Common.Configs;

/// <summary>
///     https://github.com/aaubry/YamlDotNet/issues/202#issuecomment-830712803
/// </summary>
/// <param name="nodeDeserializer"></param>
public class ValidatingDeserializer(INodeDeserializer nodeDeserializer) : INodeDeserializer
{
    public bool Deserialize(IParser parser, Type expectedType,
        Func<IParser, Type, object?> nestedObjectDeserializer, out object? value)
    {
        if (!nodeDeserializer.Deserialize(parser, expectedType, nestedObjectDeserializer, out value) ||
            value == null)
            return false;

        ValidationContext context = new(value, null, null);

        try
        {
            Validator.ValidateObject(value, context, true);
        }
        catch (ValidationException e)
        {
            if (parser.Current == null)
                throw;
            throw new YamlException(parser.Current.Start, parser.Current.End, e.Message);
        }

        return true;
    }
}