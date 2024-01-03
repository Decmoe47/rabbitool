using System.Diagnostics;
using Autofac.Annotation;
using COSXML;
using COSXML.Auth;
using COSXML.Model.Object;
using Flurl.Http;
using Rabbitool.Common.Configs;

namespace Rabbitool.Api;

[Component]
public class CosApi
{
    private readonly string _baseUrl;
    private readonly CosXmlServer _client;
    private readonly CosConfig _cosConfig;

    public CosApi(CosConfig cosConfig)
    {
        _cosConfig = cosConfig;
        _baseUrl = $"https://{cosConfig.BucketName}.cos.{cosConfig.Region}.myqcloud.com";

        CosXmlConfig? config = new CosXmlConfig.Builder()
            .IsHttps(true)
            .SetRegion(cosConfig.Region)
            .Build();
        DefaultQCloudCredentialProvider credential = new(cosConfig.SecretId, cosConfig.SecretKey, 6000);
        _client = new CosXmlServer(config, credential);
    }

    public async Task<string> UploadImageAsync(string url, CancellationToken ct = default)
    {
        string partOfUrl = url.Split("/").Last();
        string filename = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        if (partOfUrl.IndexOf('?') is var i and not -1)
            filename = partOfUrl[..i];

        if (!filename.EndsWith(".jpg") && !filename.EndsWith(".png"))
            filename += ".jpg";

        byte[] resp = await url.WithTimeout(30).GetBytesAsync(cancellationToken: ct);
        return Upload(filename, resp, "/data/images/");
    }

    public async Task<string> UploadVideoAsync(string url, DateTime pubTime, CancellationToken ct = default)
    {
        Directory.CreateDirectory("./tmp");

        string fileName = pubTime.ToString(@"yyyyMMdd_HHmmsszz") + ".mp4";
        string filePath = "./tmp/" + fileName;

        using Process p = new();
        p.StartInfo = new ProcessStartInfo
        {
            FileName = "youtube-dl",
            Arguments = $"-o {filePath} {url}",
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardInput = true,
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };

        p.Start();
        string error = await p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);
        if (error != "")
            throw new CosFileUploadException($"Failed to download the video!\nUrl: {url}\nErrMsg: {error}");

        return Upload(fileName, filePath, "/data/videos/");
    }

    private string Upload(string fileName, string filePath, string pathInCos)
    {
        using FileStream file = File.OpenRead(filePath);
        try
        {
            PutObjectRequest request = new(_cosConfig.BucketName, pathInCos + fileName, file, 0, file.Length);
            _client.PutObject(request);
            return _baseUrl + pathInCos + fileName;
        }
        finally
        {
            file.Close();
        }
    }

    private string Upload(string fileName, byte[] file, string pathInCos)
    {
        PutObjectRequest request = new(_cosConfig.BucketName, pathInCos + fileName, file);
        _ = _client.PutObject(request);
        return _baseUrl + pathInCos + fileName;
    }
}