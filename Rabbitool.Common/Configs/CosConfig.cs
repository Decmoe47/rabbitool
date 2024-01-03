using Autofac.Annotation;
using Rabbitool.Common.Constant;

namespace Rabbitool.Common.Configs;

[PropertySource(Constants.ConfigFilename)]
[Component]
public class CosConfig
{
    [Value("${cos.secretId}")] public required string SecretId { get; init; }

    [Value("${cos.secretKey}")] public required string SecretKey { get; init; }

    [Value("${cos.bucketName}")] public required string BucketName { get; init; }

    [Value("${cos.region}")] public required string Region { get; init; }
}