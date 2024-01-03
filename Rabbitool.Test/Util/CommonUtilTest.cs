using Rabbitool.Common.Util;

namespace Rabbitool.Test.Util;

public class CommonUtilTest
{
    [Fact]
    public void ExistUrlTest()
    {
        Assert.True(
            CommonUtil.ExistUrl("https://docs.microsoft.com/zh-cn/dotnet/core/testing/unit-testing-with-dotnet-test"));
    }

    [Fact]
    public void UpdatePropertiesTest()
    {
        TestClassForUpdateProperties actual = new()
        {
            Name = "haha",
            Age = 10
        };

        TestClassForUpdateProperties expected = new()
        {
            Name = "Jack",
            Age = 12,
            Identity = "aaa"
        };

        CommonUtil.UpdateProperties(actual, new Dictionary<string, dynamic>
        {
            { "Name", "Jack" },
            { "Age", 12 },
            { "Identity", "aaa" }
        });

        Assert.Equal(expected.Name, actual.Name);
        Assert.Equal(expected.Age, actual.Age);
        Assert.Equal(expected.Identity, actual.Identity);
    }
}

internal class TestClassForUpdateProperties
{
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public string Identity { get; set; } = string.Empty;
}