using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

namespace NongTimeAI.Models;

[Table("t_tmt_project_header", Schema = "tmt")]
public class ProjectHeader
{
    [Key]
    [Column("project_header_id")]
    public int ProjectHeaderId { get; set; }

    [Column("master_project_id")]
    public int? MasterProjectId { get; set; }

    [Column("project_no")]
    [MaxLength(25)]
    public string? ProjectNo { get; set; }

    [Column("project_name")]
    [MaxLength(200)]
    public string? ProjectName { get; set; }

    [Column("project_status")]
    [MaxLength(25)]
    public string? ProjectStatus { get; set; }

    [Column("application_type")]
    [MaxLength(30)]
    public string? ApplicationType { get; set; }

    [Column("project_type")]
    [MaxLength(30)]
    public string? ProjectType { get; set; }

    [Column("iso_type_id")]
    public int? IsoTypeId { get; set; }

    [Column("po_number")]
    [MaxLength(50)]
    public string? PoNumber { get; set; }

    [Column("sale_id")]
    public int SaleId { get; set; }

    [Column("customer_id")]
    public int CustomerId { get; set; }

    [Column("manday")]
    [Precision(18, 5)]
    public decimal? Manday { get; set; }

    [Column("management_cost")]
    [Precision(18, 5)]
    public decimal? ManagementCost { get; set; }

    [Column("travel_cost")]
    [Precision(18, 5)]
    public decimal? TravelCost { get; set; }

    [Column("plan_project_start")]
    public DateTime PlanProjectStart { get; set; }

    [Column("plan_project_end")]
    public DateTime PlanProjectEnd { get; set; }

    [Column("revise_project_start")]
    public DateTime? ReviseProjectStart { get; set; }

    [Column("revise_project_end")]
    public DateTime? ReviseProjectEnd { get; set; }

    [Column("actual_project_start")]
    public DateTime? ActualProjectStart { get; set; }

    [Column("actual_project_end")]
    public DateTime? ActualProjectEnd { get; set; }

    [Column("remark")]
    [MaxLength(500)]
    public string? Remark { get; set; }

    [Column("record_type")]
    [MaxLength(10)]
    public string? RecordType { get; set; }

    [Column("year")]
    public int? Year { get; set; }

    [Column("is_active")]
    [MaxLength(3)]
    public string IsActive { get; set; } = "YES";

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

    [ForeignKey("CustomerId")]
    public Customer? Customer { get; set; }

    public ICollection<ProjectTask> ProjectTasks { get; set; } = new List<ProjectTask>();
}
