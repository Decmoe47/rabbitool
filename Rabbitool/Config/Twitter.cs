﻿using System.ComponentModel.DataAnnotations;

namespace Rabbitool.Config;

public class Twitter
{
    [Required]
    public int Interval { get; set; }

    [Required]
    public string Token { get; set; } = null!;
}
