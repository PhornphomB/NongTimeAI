using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NongTimeAI.Models;

[Table("t_tmt_customer", Schema = "tmt")]
public class Customer
{
    [Key]
    [Column("customer_id")]
    public int CustomerId { get; set; }

    [Column("customer_code")]
    [MaxLength(40)]
    public string? CustomerCode { get; set; }

    [Column("customer_name")]
    [MaxLength(200)]
    public string? CustomerName { get; set; }

    [Column("description")]
    [MaxLength(255)]
    public string? Description { get; set; }

    [Column("addr_line_1")]
    [MaxLength(300)]
    public string? AddrLine1 { get; set; }

    [Column("addr_line_2")]
    [MaxLength(300)]
    public string? AddrLine2 { get; set; }

    [Column("addr_line_3")]
    [MaxLength(300)]
    public string? AddrLine3 { get; set; }

    [Column("province")]
    [MaxLength(50)]
    public string? Province { get; set; }

    [Column("postal_code")]
    [MaxLength(10)]
    public string? PostalCode { get; set; }

    [Column("country_name")]
    [MaxLength(50)]
    public string? CountryName { get; set; }

    [Column("phone")]
    [MaxLength(50)]
    public string? Phone { get; set; }

    [Column("email")]
    [MaxLength(50)]
    public string? Email { get; set; }

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

    public ICollection<ProjectHeader> ProjectHeaders { get; set; } = new List<ProjectHeader>();
}
