using Microsoft.EntityFrameworkCore;
using RentHubPro.Data;
using RentHubPro.Data.Entities;

namespace RentHubPro.Services;

public class InvoiceService
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;
    public InvoiceService(IDbContextFactory<ApplicationDbContext> factory) => _factory = factory;

    public async Task<OperationResult> IssueInvoiceAsync(int contractId, decimal amount, DateTime dueDate, string note)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var contract = await db.Contracts.FirstOrDefaultAsync(c => c.Id == contractId);
        if (contract is null) return OperationResult.Fail("Договор не найден.");
        if (contract.Status != ContractStatus.Active)
            return OperationResult.Fail("Счёт можно выставить только по подписанному (действующему) договору.");
        if (amount <= 0) return OperationResult.Fail("Сумма счёта должна быть положительной.");

        db.Invoices.Add(new Invoice
        {
            ContractId = contractId,
            Amount = amount,
            DueDate = dueDate,
            Note = note,
            IssuedAt = DateTime.UtcNow,
            Status = InvoiceStatus.Pending
        });
        await db.SaveChangesAsync();
        return OperationResult.Ok();
    }

    public async Task<OperationResult> MarkPaidAsync(int invoiceId)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var inv = await db.Invoices.FindAsync(invoiceId);
        if (inv is null) return OperationResult.Fail("Счёт не найден.");
        inv.Status = InvoiceStatus.Paid;
        await db.SaveChangesAsync();
        return OperationResult.Ok();
    }

    public async Task RefreshOverdueAsync()
    {
        await using var db = await _factory.CreateDbContextAsync();

        var today = DateTime.UtcNow.Date;
        var pending = await db.Invoices
            .Where(i => i.Status == InvoiceStatus.Pending && i.DueDate < today)
            .ToListAsync();
        foreach (var i in pending) i.Status = InvoiceStatus.Overdue;
        if (pending.Count > 0) await db.SaveChangesAsync();
    }

    public async Task<OperationResult> AttachReceiptAsync(int invoiceId, string tenantId, string relativePath)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var inv = await db.Invoices
            .Include(i => i.Contract)
            .FirstOrDefaultAsync(i => i.Id == invoiceId);
        if (inv is null) return OperationResult.Fail("Счёт не найден.");
        if (inv.Contract is null || inv.Contract.TenantId != tenantId)
            return OperationResult.Fail("Этот счёт выставлен не вам.");

        inv.ReceiptPath = relativePath;
        inv.ReceiptUploadedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();
        return OperationResult.Ok();
    }

    public async Task<List<Invoice>> GetInvoicesForLandlordAsync(string landlordId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Invoices
            .Include(i => i.Contract)!.ThenInclude(c => c!.Premise)
            .Include(i => i.Contract)!.ThenInclude(c => c!.Tenant)
            .Where(i => i.Contract!.Premise!.LandlordId == landlordId)
            .OrderByDescending(i => i.IssuedAt)
            .ToListAsync();
    }

    public async Task<List<Invoice>> GetInvoicesForTenantAsync(string tenantId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Invoices
            .Include(i => i.Contract)!.ThenInclude(c => c!.Premise)
            .Where(i => i.Contract!.TenantId == tenantId)
            .OrderByDescending(i => i.IssuedAt)
            .ToListAsync();
    }

    public async Task<decimal> GetOutstandingForLandlordAsync(string landlordId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        var sum = await db.Invoices
            .Where(i => i.Contract!.Premise!.LandlordId == landlordId && i.Status != InvoiceStatus.Paid)
            .SumAsync(i => (decimal?)i.Amount);
        return sum ?? 0m;
    }
}

public class PremiseService
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;
    public PremiseService(IDbContextFactory<ApplicationDbContext> factory) => _factory = factory;

    public record CatalogFilter(
        double? MinArea = null, double? MaxArea = null,
        decimal? MinPrice = null, decimal? MaxPrice = null,
        PremiseType? Type = null, bool OnlyAvailable = true);

    public async Task<List<Premise>> GetCatalogAsync(CatalogFilter f)
    {
        await using var db = await _factory.CreateDbContextAsync();

        IQueryable<Premise> q = db.Premises.Include(p => p.Landlord).Where(p => !p.IsRemovedByAdmin);

        if (f.OnlyAvailable) q = q.Where(p => p.Status == PremiseStatus.Available);
        if (f.Type is not null) q = q.Where(p => p.Type == f.Type);
        if (f.MinArea is not null) q = q.Where(p => p.Area >= f.MinArea);
        if (f.MaxArea is not null) q = q.Where(p => p.Area <= f.MaxArea);
        if (f.MinPrice is not null) q = q.Where(p => p.PricePerSquareMeter >= f.MinPrice);
        if (f.MaxPrice is not null) q = q.Where(p => p.PricePerSquareMeter <= f.MaxPrice);

        return await q.OrderByDescending(p => p.CreatedAt).ToListAsync();
    }

    public async Task<Premise?> GetByIdAsync(int id)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Premises.Include(p => p.Landlord).FirstOrDefaultAsync(p => p.Id == id);
    }

    public async Task<List<Premise>> GetByLandlordAsync(string landlordId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Premises.Where(p => p.LandlordId == landlordId)
            .OrderByDescending(p => p.CreatedAt).ToListAsync();
    }

    public async Task<int> CreateAsync(Premise premise)
    {
        await using var db = await _factory.CreateDbContextAsync();
        db.Premises.Add(premise);
        await db.SaveChangesAsync();
        return premise.Id;
    }

    public record PremiseEdit(
        string Title, PremiseType Type, double Area, int Floor,
        decimal PricePerSquareMeter, string Address, string Description, string? PhotoPaths);

    public async Task<OperationResult> UpdateAsync(int premiseId, string landlordId, PremiseEdit edit)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var p = await db.Premises.FirstOrDefaultAsync(x => x.Id == premiseId);
        if (p is null) return OperationResult.Fail("Помещение не найдено.");
        if (p.LandlordId != landlordId) return OperationResult.Fail("Это помещение принадлежит другому пользователю.");

        p.Title = edit.Title.Trim();
        p.Type = edit.Type;
        p.Area = edit.Area;
        p.Floor = edit.Floor;
        p.PricePerSquareMeter = edit.PricePerSquareMeter;
        p.Address = edit.Address.Trim();
        p.Description = edit.Description?.Trim() ?? string.Empty;
        if (edit.PhotoPaths is not null) p.PhotoPaths = edit.PhotoPaths;

        if (p.IsRemovedByAdmin) p.EditedAfterRemoval = true;

        await db.SaveChangesAsync();
        return OperationResult.Ok();
    }

    public async Task<OperationResult> ChangeStatusAsync(int premiseId, PremiseStatus status)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var p = await db.Premises.FindAsync(premiseId);
        if (p is null) return OperationResult.Fail("Помещение не найдено.");

        if (status == PremiseStatus.UnderRepair)
        {
            var active = await db.Contracts.AnyAsync(c =>
                c.PremiseId == premiseId && c.Status == ContractStatus.Active &&
                c.StartDate <= DateTime.UtcNow.Date && DateTime.UtcNow.Date <= c.EndDate);
            if (active)
                return OperationResult.Fail("Нельзя перевести помещение на ремонт: есть действующий договор аренды.");
        }

        p.Status = status;
        await db.SaveChangesAsync();
        return OperationResult.Ok();
    }

    public async Task<OperationResult> DeleteAsync(int premiseId)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var p = await db.Premises.Include(x => x.Contracts).FirstOrDefaultAsync(x => x.Id == premiseId);
        if (p is null) return OperationResult.Fail("Помещение не найдено.");
        if (p.Contracts.Any(c => c.Status != ContractStatus.Terminated))
            return OperationResult.Fail("Нельзя удалить помещение, по которому есть действующие договоры.");
        db.Premises.Remove(p);
        await db.SaveChangesAsync();
        return OperationResult.Ok();
    }

    public async Task<OperationResult> AdminRemoveAsync(int premiseId, string reason)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var p = await db.Premises.FindAsync(premiseId);
        if (p is null) return OperationResult.Fail("Помещение не найдено.");
        if (string.IsNullOrWhiteSpace(reason))
            return OperationResult.Fail("Укажите причину снятия с публикации.");

        p.IsRemovedByAdmin = true;
        p.AdminRemovalReason = reason.Trim();
        p.RemovedAt = DateTime.UtcNow;
        p.EditedAfterRemoval = false;
        if (p.Status == PremiseStatus.Available)
            p.Status = PremiseStatus.UnderRepair;
        await db.SaveChangesAsync();
        return OperationResult.Ok();
    }

    public async Task<OperationResult> AdminRestoreAsync(int premiseId)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var p = await db.Premises.FindAsync(premiseId);
        if (p is null) return OperationResult.Fail("Помещение не найдено.");

        p.IsRemovedByAdmin = false;
        p.AdminRemovalReason = null;
        p.RemovedAt = null;
        p.EditedAfterRemoval = false;
        await db.SaveChangesAsync();
        return OperationResult.Ok();
    }
}
