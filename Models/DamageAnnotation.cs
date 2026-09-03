using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using DamageMaker.Models;

public class DamageAnnotation
{
    public long AnnotationId { get; set; }

    public long ImageId { get; set; }

    public float X { get; set; }

    public float Y { get; set; }

    public float Width { get; set; }

    public float Height { get; set; }

    public float DamageType { get; set; } 

    public float Confidence { get; set; }

    public DateTime CreatedTime { get; set; } = DateTime.Now;

    public DateTime UpdatedTime { get; set; } = DateTime.Now;

    public string ImagePath { get; set; } = string.Empty;
}