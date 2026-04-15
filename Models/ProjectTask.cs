using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NongTimeAI.Models;

[Table("t_tmt_project_task", Schema = "tmt")]
public class ProjectTask
{
    [Key]
    [Column("project_task_id")]
    public long ProjectTaskId { get; set; }

    [Column("project_task_phase_id")]
    public int ProjectTaskPhaseId { get; set; }

    [Column("project_header_id")]
    public long ProjectHeaderId { get; set; }

    [Column("task_no")]
    [MaxLength(25)]
    public string? TaskNo { get; set; }

    [Column("task_name")]
    [MaxLength(255)]
    public string? TaskName { get; set; }

    [Column("task_description")]
    public string? TaskDescription { get; set; }

    [Column("task_status")]
    [MaxLength(25)]
    public string? TaskStatus { get; set; }

    [Column("issue_type")]
    [MaxLength(30)]
    public string? IssueType { get; set; }

    [Column("priority")]
    [MaxLength(25)]
    public string? Priority { get; set; }

    [Column("manday")]
    [Precision(18, 5)]
    public decimal? Manday { get; set; }

    [Column("start_date")]
    public DateTime StartDate { get; set; }

    [Column("end_date")]
    public DateTime EndDate { get; set; }

    [Column("end_date_extend")]
    public DateTime? EndDateExtend { get; set; }

    [Column("sequence")]
    public int Sequence { get; set; }

    [Column("remark")]
    [MaxLength(500)]
    public string? Remark { get; set; }

    [Column("close_by")]
    [MaxLength(40)]
    public string? CloseBy { get; set; }

    [Column("close_date")]
    public DateTime? CloseDate { get; set; }

    [Column("close_remark")]
    [MaxLength(255)]
    public string? CloseRemark { get; set; }

    [Column("is_incident")]
    [MaxLength(3)]
    public string? IsIncident { get; set; }

    [Column("incident_no")]
    [MaxLength(25)]
    public string? IncidentNo { get; set; }

    [Column("response_time")]
    public int? ResponseTime { get; set; }

    [Column("resolve_duration")]
    public int? ResolveDuration { get; set; }

    [Column("start_incident_date")]
    public DateTime? StartIncidentDate { get; set; }

    [Column("response_date")]
    public DateTime? ResponseDate { get; set; }

    [Column("resolve_duration_date")]
    public DateTime? ResolveDurationDate { get; set; }

    [Column("plan_response_date")]
    public DateTime? PlanResponseDate { get; set; }

    [Column("plan_resolve_duration_date")]
    public DateTime? PlanResolveDurationDate { get; set; }

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

    [ForeignKey("ProjectHeaderId")]
    public ProjectHeader? ProjectHeader { get; set; }

    public ICollection<ProjectTaskMember> ProjectTaskMembers { get; set; } = new List<ProjectTaskMember>();
    public ICollection<ProjectTaskTracking> ProjectTaskTrackings { get; set; } = new List<ProjectTaskTracking>();
}
