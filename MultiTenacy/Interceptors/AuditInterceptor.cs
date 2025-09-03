

namespace MultiTenacy.Interceptors
{
    public class AuditInterceptor : SaveChangesInterceptor
    {
        private readonly ITenantService _tenantService;
        private readonly IHttpContextAccessor _httpContextAccessor;

        private readonly ConcurrentDictionary<DbContext, List<AuditLog>> _pendingAudits = new();

        public AuditInterceptor(ITenantService tenantService, IHttpContextAccessor httpContextAccessor)
        {
            _tenantService = tenantService;
            _httpContextAccessor = httpContextAccessor;
        }

        public override InterceptionResult<int> SavingChanges(
            DbContextEventData eventData,
            InterceptionResult<int> result)
        {
            CollectAuditEntries(eventData.Context as ApplicationDbContext);
            return base.SavingChanges(eventData, result);
        }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            CollectAuditEntries(eventData.Context as ApplicationDbContext);
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
        {
            PersistAuditEntries(eventData.Context as ApplicationDbContext);
            return base.SavedChanges(eventData, result);
        }

        public override async ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData,
            int result,
            CancellationToken cancellationToken = default)
        {
            await PersistAuditEntriesAsync(eventData.Context as ApplicationDbContext, cancellationToken);
            return await base.SavedChangesAsync(eventData, result, cancellationToken);
        }


        private void CollectAuditEntries(ApplicationDbContext? context)
        {
            if (context == null) return;

            var tenantId = _tenantService.GetCurrentTenant()?.TId ?? "unknown";
            var userId = _httpContextAccessor.HttpContext?.User?.Identity?.Name;

            var auditEntries = new List<AuditLog>();

            foreach (var entry in context.ChangeTracker.Entries()
                         .Where(e => e.State == EntityState.Added ||
                                     e.State == EntityState.Modified ||
                                     e.State == EntityState.Deleted))
            {
                var audit = new AuditLog
                {
                    TableName = entry.Metadata.GetTableName() ?? entry.Entity.GetType().Name,
                    Action = entry.State.ToString(),
                    TenantId = tenantId,
                    UserId = userId,
                    CreatedAt = DateTime.UtcNow
                };

                audit.KeyValues = JsonSerializer.Serialize(entry.Properties
                    .Where(p => p.Metadata.IsPrimaryKey())
                    .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue));

                if (entry.State == EntityState.Modified)
                {
                    audit.OldValues = JsonSerializer.Serialize(entry.Properties
                        .Where(p => p.IsModified)
                        .ToDictionary(p => p.Metadata.Name, p => p.OriginalValue));
                    audit.NewValues = JsonSerializer.Serialize(entry.Properties
                        .Where(p => p.IsModified)
                        .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue));
                }
                else if (entry.State == EntityState.Added)
                {
                    audit.NewValues = JsonSerializer.Serialize(entry.Properties
                        .ToDictionary(p => p.Metadata.Name, p => p.CurrentValue));
                    audit.OldValues = "{}"; 
                }
                else if (entry.State == EntityState.Deleted)
                {
                    audit.OldValues = JsonSerializer.Serialize(entry.Properties
                        .ToDictionary(p => p.Metadata.Name, p => p.OriginalValue));
                    audit.NewValues = "{}"; 
                }

                audit.OldValues ??= "{}";
                audit.NewValues ??= "{}";

                auditEntries.Add(audit);
            }

            if (auditEntries.Any())
                _pendingAudits[context] = auditEntries;
        }

        private void PersistAuditEntries(ApplicationDbContext? context)
        {
            if (context == null) return;

            if (_pendingAudits.TryRemove(context, out var auditEntries) && auditEntries.Any())
            {
                var connString = context.Database.GetDbConnection().ConnectionString;

                var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
                optionsBuilder.UseSqlServer(connString);

                using var auditContext = new ApplicationDbContext(optionsBuilder.Options, _tenantService);
                auditContext.AuditLogs.AddRange(auditEntries);
                auditContext.SaveChanges();
            }
        }

        private async Task PersistAuditEntriesAsync(ApplicationDbContext? context, CancellationToken cancellationToken)
        {
            if (context == null) return;

            if (_pendingAudits.TryRemove(context, out var auditEntries) && auditEntries.Any())
            {
                var connString = context.Database.GetDbConnection().ConnectionString;

                var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
                optionsBuilder.UseSqlServer(connString);

                await using var auditContext = new ApplicationDbContext(optionsBuilder.Options, _tenantService);
                auditContext.AuditLogs.AddRange(auditEntries);
                await auditContext.SaveChangesAsync(cancellationToken);
            }
        }
    }
}
