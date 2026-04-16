using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NongTimeAI.Models;

[Table("t_tmt_project_task_tracking", Schema = "tmt")]
public class ProjectTaskTracking
{
    [Key]
    [Column("project_task_tracking_id")]
    [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // ✅ บอก EF Core ว่า DB จะสร้างค่าให้
    public int ProjectTaskTrackingId { get; set; }

    [Column("project_task_id")]
    public int ProjectTaskId { get; set; }

    [Column("project_header_id")]
    public int ProjectHeaderId { get; set; }

    [Required]
    [Column("process_update")]
    public string ProcessUpdate { get; set; } = string.Empty;

    [Column("issue_type")]
    [MaxLength(50)]
    public string? IssueType { get; set; }

    [Column("actual_date")]
    public DateTime ActualDate { get; set; }

    [Column("actual_work")]
    [Precision(18, 5)]
    public decimal? ActualWork { get; set; }

    [Column("assignee")]
    [MaxLength(40)]
    public string? Assignee { get; set; }

    [Column("assignee_first_name")]
    [MaxLength(200)]
    public string? AssigneeFirstName { get; set; }

    [Column("assignee_last_name")]
    [MaxLength(200)]
    public string? AssigneeLastName { get; set; }

    [Column("create_by")]
    [MaxLength(40)]
    public string? CreateBy { get; set; }

    [Column("create_date")]
    public DateTime CreateDate { get; set; }

    [Column("update_by")]
    [MaxLength(40)]
    public string? UpdateBy { get; set; }

    [Column("update_date")]
    public DateTime? UpdateDate { get; set; }

    [ForeignKey("ProjectTaskId")]
    public ProjectTask? ProjectTask { get; set; }
}
