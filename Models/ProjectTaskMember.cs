using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NongTimeAI.Models;

[Table("t_tmt_project_task_member", Schema = "tmt")]
public class ProjectTaskMember
{
    [Key]
    [Column("project_task_member_id")]
    public int ProjectTaskMemberId { get; set; }

    [Column("project_task_id")]
    public int ProjectTaskId { get; set; }

    [Column("project_header_id")]
    public int ProjectHeaderId { get; set; }

    [Column("user_id")]
    [MaxLength(40)]
    public string? UserId { get; set; }

    [Column("first_name")]
    [MaxLength(200)]
    public string? FirstName { get; set; }

    [Column("last_name")]
    [MaxLength(200)]
    public string? LastName { get; set; }

    [Column("manday")]
    [Precision(18, 5)]
    public decimal? Manday { get; set; }

    [Column("description")]
    [MaxLength(500)]
    public string? Description { get; set; }

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
