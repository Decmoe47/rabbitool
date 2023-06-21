using System.Diagnostics;
using COSXML;
using COSXML.Auth;
using COSXML.Model.Object;
using Flurl.Http;
using Rabbitool.Conf;

namespace Rabbitool.Service;

public class CosService
{
    private readonly CosXml _client;
    private readonly string _baseUrl;

    public CosService()
    {
        CosXmlConfig? config = new CosXmlConfig.Builder()
            .IsHttps(true)
            .SetRegion(Configs.R.Cos.Region)
            .Build();
        DefaultQCloudCredentialProvider credential = new(Configs.R.Cos.SecretId, Configs.R.Cos.SecretKey, 6000);
        _client = new CosXmlServer(config, credential);
        _baseUrl = $"https://{Configs.R.Cos.BucketName}.cos.{Configs.R.Cos.Region}.myqcloud.com";
    }

    public async Task<string> UploadImageAsync(string url, CancellationToken ct = default)
    {
        string partOfUrl = url.Split("/").Last();
        string filename = DateTime.Now.ToString("yyyyMMdd_HHmmss");

        if (partOfUrl.IndexOf("?") is int i and not -1)
            filename = partOfUrl[..i];

        if (!filename.EndsWith(".jpg") && !filename.EndsWith(".png"))
            filename += ".jpg";

        byte[] resp = await url.WithTimeout(30).GetBytesAsync(ct);
        return Upload(filename, resp, "/data/images/");
    }

    public async Task<string> UploadVideoAsync(string url, DateTime pubTime, CancellationToken ct = default)
    {
        Directory.CreateDirectory("./tmp");

        string fileName = pubTime.ToString(@"yyyyMMdd_HHmmsszz") + ".mp4";
        string filePath = "./tmp/" + fileName;

        using Process p = new()
        {
            StartInfo = new ProcessStartInfo()
            {
                FileName = "youtube-dl",
                Arguments = $"-o {filePath} {url}",
                CreateNoWindow = true,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
            }
        };

        p.Start();
        string error = await p.StandardError.ReadToEndAsync(ct);
        await p.WaitForExitAsync(ct);
        if (error != "")
            throw new CosFileUploadException($"Failed to download the video!\nUrl: {url}\nErrMsg: {error}");

        return Upload(fileName, filePath, "/data/videos/");
    }

    public string Upload(string fileName, string filePath, string pathInCos)
    {
        using FileStream file = File.OpenRead(filePath);
        try
        {
            PutObjectRequest request = new(Configs.R.Cos.BucketName, pathInCos + fileName, file, 0, file.Length);
            PutObjectResult result = _client.PutObject(request);
            return _baseUrl + pathInCos + fileName;
        }
        finally
        {
            file.Close();
        }
    }

    public string Upload(string fileName, byte[] file, string pathInCos)
    {
        PutObjectRequest request = new(Configs.R.Cos.BucketName, pathInCos + fileName, file);
        _ = _client.PutObject(request);
        return _baseUrl + pathInCos + fileName;
    }
}
