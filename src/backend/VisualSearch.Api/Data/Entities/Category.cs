using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace VisualSearch.Api.Data.Entities;

/// <summary>
/// Represents a detection category (COCO/YOLO class) for object detection and product classification.
/// </summary>
[Table("categories")]
public sealed class Category
{
    /// <summary>
    /// Gets or sets the unique identifier for the category.
    /// </summary>
    [Key]
    [Column("id")]
    public int Id { get; set; }

    /// <summary>
    /// Gets or sets the name of the category (e.g., "chair", "sofa").
    /// </summary>
    [Required]
    [Column("name")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the COCO class ID used by the YOLO model.
    /// </summary>
    [Required]
    [Column("coco_class_id")]
    public int CocoClassId { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether detection is enabled for this category.
    /// When enabled, the YOLO model will detect objects of this class.
    /// </summary>
    [Column("detection_enabled")]
    public bool DetectionEnabled { get; set; } = true;

    /// <summary>
    /// Gets or sets the date and time when the category was created.
    /// </summary>
    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Gets or sets the collection of products associated with this category.
    /// </summary>
    public ICollection<Product> Products { get; set; } = [];
}
