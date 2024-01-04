﻿using Autofac.Annotation;
using Autofac.Annotation.Condition;
using Rabbitool.Common.Constant;

namespace Rabbitool.Common.Configs;

[ConditionalOnProperty("bilibili:enabled", "True")]
[PropertySource(Constants.ConfigFilename)]
[Component(AutofacScope = AutofacScope.SingleInstance)]
public class BilibiliConfig
{
    [Value("${bilibili:enabled}")] public bool Enable { get; set; }
}