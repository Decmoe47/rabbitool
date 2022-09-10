using System.Diagnostics;
using System.Text.RegularExpressions;
using COSXML;
using COSXML.Auth;
using COSXML.Model.Object;
using Flurl.Http;

namespace Rabbitool.Service;

public class CosService
{
    private readonly CosXml _client;
    private readonly string _bucket;
    private readonly string _baseUrl;

    public CosService(string bucket, string region, string secretId, string secretKey)
    {
        CosXmlConfig? config = new CosXmlConfig.Builder()
            .IsHttps(true)
            .SetRegion(region)
            .Build();
        DefaultQCloudCredentialProvider credential = new(secretId, secretKey, 6000);
        _client = new CosXmlServer(config, credential);
        _bucket = bucket;
        _baseUrl = $"https://{bucket}.cos.{region}.myqcloud.com";
    }

    public async Task<string> UploadImageAsync(string url, CancellationToken cancellationToken = default)
    {
        string format = "";
        string filename = url.Split("/").Last();

        Match match = Regex.Match(filename, @"(?<=format=).+?(?=&)");
        if (match.Success)
            format = match.Groups[0].Value;

        if (filename.IndexOf("?") is int j and not -1)
            filename = filename[..j];

        if (format != "")
            filename += "." + format;

        byte[] resp = await url.GetBytesAsync(cancellationToken);
        return Upload(filename, resp, "/data/images/");
    }

    public async Task<string> UploadVideoAsync(string url, DateTime pubTime, CancellationToken cancellationToken = default)
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
        string error = await p.StandardError.ReadToEndAsync();
        await p.WaitForExitAsync(cancellationToken);
        if (error is not "")
            throw new CosFileUploadException($"Failed to download the video!\nUrl: {url}\nErrMsg: {error}");

        using FileStream file = File.OpenRead(filePath);
        return Upload(fileName, file, "/data/videos/");
    }

    public string Upload(string fileName, FileStream file, string pathInCos)
    {
        try
        {
            PutObjectRequest request = new(_bucket, pathInCos + fileName, file, 0, file.Length);
            request.SetCosProgressCallback(
                (long completed, long total) =>
                    Console.WriteLine(string.Format("progress = {0:##.##}%", completed * 100.0 / total)));
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
        PutObjectRequest request = new(_bucket, pathInCos + fileName, file);
        request.SetCosProgressCallback(
            (long completed, long total) =>
                Console.WriteLine(string.Format("progress = {0:##.##}%", completed * 100.0 / total)));
        PutObjectResult result = _client.PutObject(request);

        return _baseUrl + pathInCos + fileName;
    }
}
