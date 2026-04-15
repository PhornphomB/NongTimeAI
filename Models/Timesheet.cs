using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NongTimeAI.Models;

[Table("timesheets")]
public class Timesheet
{
    [Key]
    [Column("id")]
    public int Id { get; set; }

    [Required]
    [Column("user_id")]
    [MaxLength(100)]
    public string UserId { get; set; } = string.Empty;

    [Required]
    [Column("detail")]
    [MaxLength(500)]
    public string Detail { get; set; } = string.Empty;

    [Column("hours")]
    public float Hours { get; set; }

    [Column("issue_type")]
    [MaxLength(50)]
    public string IssueType { get; set; } = "Task";

    [Column("date")]
    public DateTime Date { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; }

    [Column("updated_at")]
    public DateTime? UpdatedAt { get; set; }

    [Column("project_id")]
    public int? ProjectId { get; set; }

    [ForeignKey("ProjectId")]
    public Project? Project { get; set; }
}
