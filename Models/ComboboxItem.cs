using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NongTimeAI.Models;

[Table("t_com_combobox_item", Schema = "sec")]
public class ComboboxItem
{
    [Key]
    [Column("combo_box_id")]
    public int ComboBoxId { get; set; }

    [Column("app_id")]
    public int AppId { get; set; }

    [Column("group_name")]
    [MaxLength(50)]
    public string? GroupName { get; set; }

    [Column("value_member")]
    [MaxLength(30)]
    public string? ValueMember { get; set; }

    [Column("display_member")]
    [MaxLength(200)]
    public string? DisplayMember { get; set; }

    [Column("value_member1")]
    [MaxLength(30)]
    public string? ValueMember1 { get; set; }

    [Column("value_member2")]
    [MaxLength(30)]
    public string? ValueMember2 { get; set; }

    [Column("value_member3")]
    [MaxLength(30)]
    public string? ValueMember3 { get; set; }

    [Column("description")]
    [MaxLength(200)]
    public string? Description { get; set; }

    [Column("display_sequence")]
    public int? DisplaySequence { get; set; }

    [Column("is_active")]
    [MaxLength(3)]
    public string IsActive { get; set; } = "YES";

    [Column("micro_service_name")]
    [MaxLength(15)]
    public string? MicroServiceName { get; set; }

    [Column("create_by")]
    [MaxLength(40)]
    public string? CreateBy { get; set; }

    [Column("create_date")]
    public DateTime CreateDate { get; set; }
}
