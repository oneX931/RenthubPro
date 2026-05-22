namespace RentHubPro.Data.Entities;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Landlord = "Landlord";
    public const string Tenant = "Tenant";

    public static readonly string[] All = { Admin, Landlord, Tenant };
}

public enum PremiseType
{
    Office,
    Retail,
    Warehouse,
    Industrial,
    Other
}

public enum PremiseStatus
{
    Available,
    Rented,
    UnderRepair
}

public enum InvoiceStatus
{
    Pending,
    Paid,
    Overdue
}

public enum ContractStatus
{
    PendingSignature,
    Active,
    Terminated
}

public enum AppTheme
{
    Light,
    Dark
}

public static class EnumDisplay
{
    public static string Ru(this PremiseType t) => t switch
    {
        PremiseType.Office => "Офис",
        PremiseType.Retail => "Торговое",
        PremiseType.Warehouse => "Склад",
        PremiseType.Industrial => "Производственное",
        _ => "Иное"
    };

    public static string Ru(this PremiseStatus s) => s switch
    {
        PremiseStatus.Available => "Свободно",
        PremiseStatus.Rented => "Сдано",
        PremiseStatus.UnderRepair => "На ремонте",
        _ => s.ToString()
    };

    public static string Ru(this InvoiceStatus s) => s switch
    {
        InvoiceStatus.Pending => "Ожидает оплаты",
        InvoiceStatus.Paid => "Оплачен",
        InvoiceStatus.Overdue => "Просрочен",
        _ => s.ToString()
    };

    public static string Ru(this ContractStatus s) => s switch
    {
        ContractStatus.PendingSignature => "Ожидает подписания",
        ContractStatus.Active => "Действует",
        ContractStatus.Terminated => "Расторгнут",
        _ => s.ToString()
    };
}
