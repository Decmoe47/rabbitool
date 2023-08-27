using Newtonsoft.Json.Linq;

namespace Rabbitool.Common.Extension;

public static class JObjectExtension
{
    public static JObject RemoveNullAndEmptyProperties(this JObject jObject)
    {
        while (jObject.Descendants().Any(NullOrUndefinedPredicate))
            foreach (JToken jt in jObject.Descendants().Where(NullOrUndefinedPredicate).ToArray())
                jt.Remove();

        return jObject;
    }

    private static bool NullOrUndefinedPredicate(JToken jt)
    {
        return jt.Type == JTokenType.Property
               && (jt.Values().All(a => a.Type is JTokenType.Null or JTokenType.Undefined) || !jt.Values().Any());
    }
}