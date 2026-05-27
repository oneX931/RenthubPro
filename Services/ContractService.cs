using Microsoft.EntityFrameworkCore;
using RentHubPro.Data;
using RentHubPro.Data.Entities;

namespace RentHubPro.Services;

public record OperationResult(bool Success, string? Error = null)
{
    public static OperationResult Ok() => new(true);
    public static OperationResult Fail(string error) => new(false, error);
}

public class ContractService
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;

    public ContractService(IDbContextFactory<ApplicationDbContext> factory) => _factory = factory;

    public static decimal CalculateMonthlyAmount(Premise premise) =>
        Math.Round((decimal)premise.Area * premise.PricePerSquareMeter, 2);

    public async Task<OperationResult> CreateContractAsync(
        int premiseId, string tenantId, DateTime start, DateTime end, decimal? manualAmount)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var premise = await db.Premises.FirstOrDefaultAsync(p => p.Id == premiseId);
        if (premise is null)
            return OperationResult.Fail("Помещение не найдено.");

        if (end <= start)
            return OperationResult.Fail("Дата окончания аренды должна быть строго позже даты начала.");

        if (premise.Status == PremiseStatus.UnderRepair)
            return OperationResult.Fail("Нельзя оформить договор на помещение со статусом «на ремонте».");

        var overlaps = await db.Contracts
            .AnyAsync(c => c.PremiseId == premiseId && c.Status != ContractStatus.Terminated
                && start <= c.EndDate && c.StartDate <= end);
        if (overlaps)
            return OperationResult.Fail("На выбранный период помещение уже сдано (сроки пересекаются с другим договором).");

        var amount = manualAmount ?? CalculateMonthlyAmount(premise);
        if (amount < 0)
            return OperationResult.Fail("Сумма платежа не может быть отрицательной.");

        var contract = new Contract
        {
            PremiseId = premiseId,
            TenantId = tenantId,
            StartDate = start,
            EndDate = end,
            MonthlyAmount = amount,
            Status = ContractStatus.PendingSignature
        };
        db.Contracts.Add(contract);

        await db.SaveChangesAsync();
        return OperationResult.Ok();
    }

    public async Task<OperationResult> SignContractAsync(int contractId, string tenantId)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var contract = await db.Contracts
            .Include(c => c.Premise)
            .FirstOrDefaultAsync(c => c.Id == contractId);
        if (contract is null) return OperationResult.Fail("Договор не найден.");
        if (contract.TenantId != tenantId)
            return OperationResult.Fail("Этот договор оформлен не на вас.");
        if (contract.Status == ContractStatus.Terminated)
            return OperationResult.Fail("Договор расторгнут и не может быть подписан.");
        if (contract.Status == ContractStatus.Active)
            return OperationResult.Fail("Договор уже подписан.");

        contract.Status = ContractStatus.Active;
        contract.SignedAt = DateTime.UtcNow;

        if (contract.Premise is not null &&
            contract.StartDate <= DateTime.UtcNow.Date && DateTime.UtcNow.Date <= contract.EndDate &&
            contract.Premise.Status == PremiseStatus.Available)
        {
            contract.Premise.Status = PremiseStatus.Rented;
        }

        await db.SaveChangesAsync();
        return OperationResult.Ok();
    }

    public async Task<OperationResult> ExtendContractAsync(int contractId, DateTime newEnd)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var contract = await db.Contracts.FirstOrDefaultAsync(c => c.Id == contractId);
        if (contract is null) return OperationResult.Fail("Договор не найден.");
        if (newEnd <= contract.EndDate) return OperationResult.Fail("Новая дата окончания должна быть позже текущей.");

        var overlaps = await db.Contracts.AnyAsync(c =>
            c.Id != contractId && c.PremiseId == contract.PremiseId && c.Status != ContractStatus.Terminated &&
            contract.StartDate <= c.EndDate && c.StartDate <= newEnd);
        if (overlaps)
            return OperationResult.Fail("Продление пересекается со сроками другого договора на это помещение.");

        contract.EndDate = newEnd;
        await db.SaveChangesAsync();
        return OperationResult.Ok();
    }

    public async Task<OperationResult> TerminateContractAsync(int contractId)
    {
        await using var db = await _factory.CreateDbContextAsync();

        var contract = await db.Contracts
            .Include(c => c.Premise)
            .FirstOrDefaultAsync(c => c.Id == contractId);
        if (contract is null) return OperationResult.Fail("Договор не найден.");
        if (contract.Status == ContractStatus.Terminated)
            return OperationResult.Fail("Договор уже расторгнут.");

        contract.Status = ContractStatus.Terminated;

        var premiseId = contract.PremiseId;
        var today = DateTime.UtcNow.Date;
        if (contract.Premise is not null && contract.Premise.Status == PremiseStatus.Rented)
        {
            var stillRented = await db.Contracts.AnyAsync(c =>
                c.Id != contractId && c.PremiseId == premiseId && c.Status == ContractStatus.Active &&
                c.StartDate <= today && today <= c.EndDate);
            if (!stillRented)
                contract.Premise.Status = PremiseStatus.Available;
        }

        await db.SaveChangesAsync();
        return OperationResult.Ok();
    }

    public async Task<List<Contract>> GetContractsForLandlordAsync(string landlordId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Contracts
            .Include(c => c.Premise)
            .Include(c => c.Tenant)
            .Where(c => c.Premise!.LandlordId == landlordId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<Contract>> GetContractsForTenantAsync(string tenantId)
    {
        await using var db = await _factory.CreateDbContextAsync();
        return await db.Contracts
            .Include(c => c.Premise)!.ThenInclude(p => p!.Landlord)
            .Where(c => c.TenantId == tenantId)
            .OrderByDescending(c => c.CreatedAt)
            .ToListAsync();
    }
}
