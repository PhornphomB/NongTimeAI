using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace NongTimeAI.Models;

[Table("t_com_user", Schema = "sec")]
public class User
{
    [Key]
    [Column("user_id")]
    [MaxLength(40)]
    public string UserId { get; set; } = string.Empty;

    [Column("user_group_id")]
    public int UserGroupId { get; set; }

    [Column("first_name")]
    [MaxLength(200)]
    public string? FirstName { get; set; }

    [Column("last_name")]
    [MaxLength(200)]
    public string? LastName { get; set; }

    [Column("password")]
    [MaxLength(200)]
    public string? Password { get; set; }

    [Column("locale_id")]
    [MaxLength(5)]
    public string? LocaleId { get; set; }

    [Column("department")]
    [MaxLength(120)]
    public string? Department { get; set; }

    [Column("supervisor")]
    [MaxLength(120)]
    public string? Supervisor { get; set; }

    [Column("email_address")]
    [MaxLength(120)]
    public string? EmailAddress { get; set; }

    [Column("domain")]
    [MaxLength(120)]
    public string? Domain { get; set; }

    [Column("is_active")]
    [MaxLength(3)]
    public string IsActive { get; set; } = "YES";

    [Column("access_failed_count")]
    public int AccessFailedCount { get; set; }

    [Column("birth_date")]
    public DateTime? BirthDate { get; set; }

    [Column("image")]
    public string? Image { get; set; }

    [Column("line_user_id")]
    [MaxLength(225)]
    public string? LineUserId { get; set; }

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
}
