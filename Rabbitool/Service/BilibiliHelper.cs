using System.Security.Cryptography;
using System.Text;
using Flurl.Http;
using Newtonsoft.Json.Linq;

namespace Rabbitool.Service;

/// <summary>
/// <see cref="https://github.com/SocialSisterYi/bilibili-API-collect/blob/master/docs/misc/sign/wbi.md"/>
/// </summary>
public class BilibiliHelper
{
    private static readonly int[] _mixinKeyEncTab = {
        46, 47, 18, 2, 53, 8, 23, 32, 15, 50, 10, 31, 58, 3, 45, 35, 27, 43, 5, 49,
        33, 9, 42, 19, 29, 28, 14, 39, 12, 38, 41, 13, 37, 48, 7, 16, 24, 55, 40,
        61, 26, 17, 0, 1, 60, 51, 30, 4, 22, 25, 54, 21, 56, 59, 6, 63, 57, 62, 11,
        36, 20, 34, 44, 52
    };

    public static async Task<string> GenerateQueryWithWbiAsync(Dictionary<string, string> commonParams)
    {
        (string imgKey, string subKey) = await GetWbiKeysAsync();
        Dictionary<string, string> signedParams = EncWbi(commonParams, imgKey, subKey);
        return string.Join("&", signedParams.Select(p => $"{p.Key}={p.Value}"));
    }

    public static async Task<string> GenerateQueryAsync(string commonParamKey, string commonParamValue)
    {
        (string imgKey, string subKey) = await GetWbiKeysAsync();
        Dictionary<string, string> signedParams = EncWbi(
            new Dictionary<string, string>
            {
                { commonParamKey, commonParamValue },
            },
            imgKey,
            subKey
        );
        return string.Join("&", signedParams.Select(p => $"{p.Key}={p.Value}"));
    }

    private static string GetMixinKey(string orig)
    {
        // 对 imgKey 和 subKey 进行字符顺序打乱编码
        return string.Concat(_mixinKeyEncTab.Select(i => orig[i]))[..32];
    }

    private static Dictionary<string, string> EncWbi(Dictionary<string, string> parameters, string imgKey, string subKey)
    {
        string mixinKey = GetMixinKey(imgKey + subKey);  //为请求参数进行 wbi 签名
        long currTime = DateTimeOffset.Now.ToUnixTimeSeconds();
        parameters["wts"] = currTime.ToString(); // 添加 wts 字段
        parameters = parameters.OrderBy(p => p.Key).ToDictionary(p => p.Key, p => p.Value); // 按照 key 排序参数
                                                                                            // 过滤 value 中的 "!'()*" 字符
        parameters = parameters.ToDictionary(
            p => p.Key,
            p => string.Concat(p.Value.Where(c => !"!'()*".Contains(c)))
        );
        string query = string.Join("&", parameters.Select(p => $"{p.Key}={p.Value}")); // 序列化参数
        string wbiSign = MD5Hash(query + mixinKey); // 计算 w_rid
        parameters["w_rid"] = wbiSign;
        return parameters;
    }

    private static async Task<(string, string)> GetWbiKeysAsync()
    {
        string resp = await "https://api.bilibili.com/x/web-interface/nav"
            .AllowAnyHttpStatus()
            .GetStringAsync();
        JObject body = JObject.Parse(resp);
        string imgUrl = (string)body["data"]!["wbi_img"]!["img_url"]!;
        string subUrl = (string)body["data"]!["wbi_img"]!["sub_url"]!;
        string imgKey = imgUrl.Split('/').Last().Split('.').First();
        string subKey = subUrl.Split('/').Last().Split('.').First();
        return (imgKey, subKey);
    }

    private static string MD5Hash(string input)
    {
        byte[] data = MD5.HashData(Encoding.UTF8.GetBytes(input));
        StringBuilder sb = new();
        for (int i = 0; i < data.Length; i++)
            sb.Append(data[i].ToString("x2"));
        return sb.ToString();
    }
}