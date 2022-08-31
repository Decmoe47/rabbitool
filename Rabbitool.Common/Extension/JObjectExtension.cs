using Newtonsoft.Json.Linq;

namespace Rabbitool.Common.Extension;

public static class JObjectExtension
{
    public static JObject RemoveNullAndEmptyProperties(this JObject jObject)
    {
        while (jObject.Descendants().Any(NullOrUndefindedPredicate))
        {
            foreach (JToken jt in jObject.Descendants().Where(NullOrUndefindedPredicate).ToArray())
                jt.Remove();
        }

        return jObject;
    }

    private static bool NullOrUndefindedPredicate(JToken jt)
    {
        return jt.Type == JTokenType.Property
            && (jt.Values().All(a => a.Type == JTokenType.Null || a.Type == JTokenType.Undefined) || !jt.Values().Any());
    }
}
