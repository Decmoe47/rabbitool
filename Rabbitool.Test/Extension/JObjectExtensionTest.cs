using Newtonsoft.Json.Linq;
using Rabbitool.Common.Extension;

namespace Rabbitool.Test.Extension;

public class JObjectExtensionTest
{
    [Theory]
    [InlineData("""{"data":{"card":{"item":{"name":null}}}}""")]
    public void RemoveNullAndEmptyPropertiesTest(string json)
    {
        JObject body = JObject.Parse(json).RemoveNullAndEmptyProperties();
        dynamic? name = body["data"]?["card"]?["item"]?["name"];
        Assert.Null(name);
    }
}