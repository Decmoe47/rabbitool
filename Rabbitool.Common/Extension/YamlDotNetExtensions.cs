using Rabbitool.Common.Configs;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NodeDeserializers;

namespace Rabbitool.Common.Extension;

/// <summary>
///     https://github.com/aaubry/YamlDotNet/issues/202#issuecomment-830712803
/// </summary>
public static class YamlDotNetExtensions
{
    public static DeserializerBuilder WithRequiredPropertyValidation(this DeserializerBuilder builder)
    {
        return builder
            .WithNodeDeserializer(inner => new ValidatingDeserializer(inner),
                s => s.InsteadOf<ObjectNodeDeserializer>());
    }
}