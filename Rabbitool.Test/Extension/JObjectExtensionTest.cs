using Newtonsoft.Json.Linq;

namespace Rabbitool.Common.Extension.Test;

public class JObjectExtensionTest
{
    [Theory()]
    [InlineData(@"{""data"":{""card"":{""item"":{""name"":null}}}}")]
    public void RemoveNullAndEmptyPropertiesTest(string json)
    {
        JObject body = JObject.Parse(json).RemoveNullAndEmptyProperties();
        dynamic? name = body["data"]?["card"]?["item"]?["name"];
        Assert.Null(name);
    }
}
