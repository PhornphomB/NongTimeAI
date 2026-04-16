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

	// Function to get pending tasks - Cross-database compatible using LINQ
	public async Task<List<PendingTaskDto>> GetPendingTasksAsync(string userId)
	{
		var query = from task in ProjectTasks
					join header in ProjectHeaders on task.ProjectHeaderId equals header.ProjectHeaderId
					join customer in Customers on header.CustomerId equals customer.CustomerId into custGroup
					from cust in custGroup.DefaultIfEmpty()
					join user in Users on task.CreateBy equals user.UserId into userGroup
					from u in userGroup.DefaultIfEmpty()
					where header.IsActive == "YES"
						&& task.TaskStatus != "close"
						&& ProjectTaskMembers.Any(m => m.ProjectTaskId == task.ProjectTaskId && m.UserId == userId)
						&& !ProjectTaskTrackings.Any(t => t.ProjectTaskId == task.ProjectTaskId && t.CreateBy == userId)
					select new PendingTaskDto
					{
						ProjectTaskId = task.ProjectTaskId,
						TaskNo = task.TaskNo,
						TaskName = task.TaskName,
						TaskStatus = task.TaskStatus,
						TaskDescription = task.TaskDescription,
						StartDate = task.StartDate.Date,
						EndDate = task.EndDate.Date,
						EndDateExtend = task.EndDateExtend.HasValue ? task.EndDateExtend.Value.Date : (DateTime?)null,
						Priority = task.Priority,
						PriorityOrder = task.Priority == "High" ? 1 : 
									   task.Priority == "Medium" ? 2 : 
									   task.Priority == "Low" ? 3 : 4,
						Manday = task.Manday,
						IssueType = task.IssueType,
						Remark = task.Remark,
						ProjectHeaderId = header.ProjectHeaderId,
						ProjectNo = header.ProjectNo,
						ProjectName = header.ProjectName,
						ProjectType = header.ProjectType,
						ApplicationType = header.ApplicationType,
						CustomerName = cust != null ? cust.CustomerName : null,
						CreateBy = u != null ? u.FirstName + " " + u.LastName : null,
						CreateDate = task.CreateDate,
						UpdateBy = task.UpdateBy,
						UpdateDate = task.UpdateDate
					};

		var result = await query
			.OrderBy(t => t.PriorityOrder)
			.ThenBy(t => t.EndDate)
			.ToListAsync();

		return result;
	}
}
