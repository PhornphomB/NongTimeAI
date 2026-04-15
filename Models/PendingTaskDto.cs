namespace NongTimeAI.Models;

public class PendingTaskDto
{
    public long ProjectTaskId { get; set; }
    public string? TaskNo { get; set; }
    public string? TaskName { get; set; }
    public string? TaskStatus { get; set; }
    public string? TaskDescription { get; set; }
    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public DateTime? EndDateExtend { get; set; }
    public string? Priority { get; set; }
    public int PriorityOrder { get; set; }
    public decimal? Manday { get; set; }
    public string? IssueType { get; set; }
    public string? Remark { get; set; }
    public long ProjectHeaderId { get; set; }
    public string? ProjectNo { get; set; }
    public string? ProjectName { get; set; }
    public string? ProjectType { get; set; }
    public string? ApplicationType { get; set; }
    public string? CustomerName { get; set; }
    public string? CreateBy { get; set; }
    public DateTime CreateDate { get; set; }
    public string? UpdateBy { get; set; }
    public DateTime? UpdateDate { get; set; }
}
