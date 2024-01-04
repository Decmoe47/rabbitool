using Autofac.Annotation;
using Rabbitool.Common.Constant;

namespace Rabbitool.Common.Configs;

[PropertySource(Constants.ConfigFilename)]
[Component(AutofacScope = AutofacScope.SingleInstance)]
public class CosConfig
{
    [Value("${cos:secretId}")] public string SecretId { get; init; } = null!;

    [Value("${cos:secretKey}")] public string SecretKey { get; init; } = null!;

    [Value("${cos:bucketName}")] public string BucketName { get; init; } = null!;

    [Value("${cos:region}")] public string Region { get; init; } = null!;
}