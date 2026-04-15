using Microsoft.EntityFrameworkCore;
using NongTimeAI.Models;

namespace NongTimeAI.Data;

public class TimesheetDbContext : DbContext
{
    public TimesheetDbContext(DbContextOptions<TimesheetDbContext> options)
        : base(options)
    {
    }

    // TMT Schema Tables
    public DbSet<Customer> Customers { get; set; }
    public DbSet<ProjectHeader> ProjectHeaders { get; set; }
    public DbSet<ProjectTask> ProjectTasks { get; set; }
    public DbSet<ProjectTaskMember> ProjectTaskMembers { get; set; }
    public DbSet<ProjectTaskTracking> ProjectTaskTrackings { get; set; }

    // SEC Schema Tables
    public DbSet<User> Users { get; set; }
    public DbSet<ComboboxItem> ComboboxItems { get; set; }

    // Legacy tables (ถ้ายังต้องการ)
    public DbSet<Timesheet> Timesheets { get; set; }
    public DbSet<Project> Projects { get; set; }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Customer configuration
        modelBuilder.Entity<Customer>(entity =>
        {
            entity.HasKey(e => e.CustomerId);
            entity.HasIndex(e => e.CustomerCode);
        });

        // ProjectHeader configuration
        modelBuilder.Entity<ProjectHeader>(entity =>
        {
            entity.HasKey(e => e.ProjectHeaderId);
            entity.HasIndex(e => e.ProjectNo);
            entity.HasIndex(e => e.CustomerId);

            entity.HasOne(e => e.Customer)
                .WithMany(c => c.ProjectHeaders)
                .HasForeignKey(e => e.CustomerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ProjectTask configuration
        modelBuilder.Entity<ProjectTask>(entity =>
        {
            entity.HasKey(e => e.ProjectTaskId);
            entity.HasIndex(e => e.ProjectHeaderId);
            entity.HasIndex(e => e.TaskStatus);
            entity.HasIndex(e => new { e.ProjectHeaderId, e.TaskStatus });

            entity.HasOne(e => e.ProjectHeader)
                .WithMany(p => p.ProjectTasks)
                .HasForeignKey(e => e.ProjectHeaderId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ProjectTaskMember configuration
        modelBuilder.Entity<ProjectTaskMember>(entity =>
        {
            entity.HasKey(e => e.ProjectTaskMemberId);
            entity.HasIndex(e => e.ProjectTaskId);
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => new { e.ProjectTaskId, e.UserId });

            entity.HasOne(e => e.ProjectTask)
                .WithMany(t => t.ProjectTaskMembers)
                .HasForeignKey(e => e.ProjectTaskId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // ProjectTaskTracking configuration
        modelBuilder.Entity<ProjectTaskTracking>(entity =>
        {
            entity.HasKey(e => e.ProjectTaskTrackingId);
            entity.HasIndex(e => e.ProjectTaskId);
            entity.HasIndex(e => e.Assignee);
            entity.HasIndex(e => e.ActualDate);

            entity.HasOne(e => e.ProjectTask)
                .WithMany(t => t.ProjectTaskTrackings)
                .HasForeignKey(e => e.ProjectTaskId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        // User configuration
        modelBuilder.Entity<User>(entity =>
        {
            entity.HasKey(e => e.UserId);
            entity.HasIndex(e => e.LineUserId).IsUnique();
            entity.HasIndex(e => e.EmailAddress);
        });

        // ComboboxItem configuration
        modelBuilder.Entity<ComboboxItem>(entity =>
        {
            entity.HasKey(e => e.ComboBoxId);
            entity.HasIndex(e => new { e.GroupName, e.IsActive });
        });

        // Legacy Timesheet configuration (if still needed)
        modelBuilder.Entity<Timesheet>(entity =>
        {
            entity.HasIndex(e => e.UserId);
            entity.HasIndex(e => e.Date);
            entity.HasIndex(e => new { e.UserId, e.Date });

            entity.HasOne(e => e.Project)
                .WithMany(p => p.Timesheets)
                .HasForeignKey(e => e.ProjectId)
                .OnDelete(DeleteBehavior.SetNull);
        });

        // Legacy Project configuration
        modelBuilder.Entity<Project>(entity =>
        {
            entity.HasIndex(e => e.Name).IsUnique();
        });
    }

    public override int SaveChanges()
    {
        UpdateTimestamps();
        return base.SaveChanges();
    }

    public override async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        UpdateTimestamps();
        return await base.SaveChangesAsync(cancellationToken);
    }

    private void UpdateTimestamps()
    {
        var entries = ChangeTracker.Entries()
            .Where(e => e.State == EntityState.Added || e.State == EntityState.Modified);

        foreach (var entry in entries)
        {
            if (entry.State == EntityState.Added)
            {
                if (entry.Entity is Timesheet timesheet)
                {
                    timesheet.CreatedAt = DateTime.UtcNow;
                }
                else if (entry.Entity is Project project)
                {
                    project.CreatedAt = DateTime.UtcNow;
                }
                else if (entry.Entity is User user)
                {
                    user.CreateDate = DateTime.UtcNow;
                }
                else if (entry.Entity is ProjectTaskTracking tracking)
                {
                    tracking.CreateDate = DateTime.UtcNow;
                }
            }
            else if (entry.State == EntityState.Modified)
            {
                if (entry.Entity is Timesheet timesheet)
                {
                    timesheet.UpdatedAt = DateTime.UtcNow;
                }
                else if (entry.Entity is Project project)
                {
                    project.UpdatedAt = DateTime.UtcNow;
                }
                else if (entry.Entity is User user)
                {
                    user.UpdateDate = DateTime.UtcNow;
                }
                else if (entry.Entity is ProjectTaskTracking tracking)
                {
                    tracking.UpdateDate = DateTime.UtcNow;
                }
            }
        }
    }

    // Function to get pending tasks
    public async Task<List<PendingTaskDto>> GetPendingTasksAsync(string userId)
    {
        var sql = @"
            SELECT 
                t.project_task_id AS ProjectTaskId,
                t.task_no AS TaskNo,
                t.task_name AS TaskName,
                t.task_status AS TaskStatus,
                t.task_description AS TaskDescription,
                t.start_date::DATE AS StartDate,
                t.end_date::DATE AS EndDate,
                t.end_date_extend::DATE AS EndDateExtend,
                t.priority AS Priority,
                CASE t.priority 
                    WHEN 'High' THEN 1 
                    WHEN 'Medium' THEN 2 
                    WHEN 'Low' THEN 3 
                    ELSE 4 
                END AS PriorityOrder,
                t.manday AS Manday,
                t.issue_type AS IssueType,
                t.remark AS Remark,
                prjHD.project_header_id AS ProjectHeaderId,
                prjHD.project_no AS ProjectNo,
                prjHD.project_name AS ProjectName,
                prjHD.project_type AS ProjectType,
                prjHD.application_type AS ApplicationType,
                cust.customer_name AS CustomerName,
                (u.first_name || ' ' || u.last_name) AS CreateBy,
                t.create_date AS CreateDate,
                t.update_by AS UpdateBy,
                t.update_date AS UpdateDate
            FROM tmt.t_tmt_project_task t
            INNER JOIN tmt.t_tmt_project_header prjHD 
                ON t.project_header_id = prjHD.project_header_id AND prjHD.is_active = 'YES'
            LEFT JOIN tmt.t_tmt_customer cust 
                ON prjHD.customer_id = cust.customer_id
            LEFT JOIN sec.t_com_user u 
                ON u.user_id = t.create_by
            WHERE EXISTS (
                SELECT 1 
                FROM tmt.t_tmt_project_task_member tm 
                WHERE tm.project_task_id = t.project_task_id 
                  AND t.task_status <> 'close'
                  AND tm.user_id = {0}
            )
            AND NOT EXISTS (
		            SELECT 1
		            FROM tmt.t_tmt_project_task_tracking trk
		            WHERE trk.project_task_id = t.project_task_id
		              AND trk.create_by = {0}
	            )
            ORDER BY 
                CASE t.priority 
                    WHEN 'High' THEN 1 
                    WHEN 'Medium' THEN 2 
                    WHEN 'Low' THEN 3 
                    ELSE 4 
                END,
                t.end_date";

        return await Database.SqlQueryRaw<PendingTaskDto>(sql, userId).ToListAsync();
    }
}
