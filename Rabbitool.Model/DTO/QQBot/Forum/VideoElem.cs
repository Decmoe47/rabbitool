﻿using Newtonsoft.Json;

namespace Rabbitool.Model.DTO.QQBot;

public class VideoElem
{
    [JsonProperty("third_url")]
    public string ThirdUrl { get; set; } = string.Empty;
}
