using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using Microsoft.EntityFrameworkCore;
using RentHubPro.Data;
using RentHubPro.Data.Entities;

namespace RentHubPro.Services;

public class ReportService
{
    private readonly IDbContextFactory<ApplicationDbContext> _factory;
    public ReportService(IDbContextFactory<ApplicationDbContext> factory) => _factory = factory;

    public async Task<byte[]> BuildFinancialReportAsync(string landlordId, string landlordName, DateTime from, DateTime to)
    {
        List<Invoice> invoices;
        await using (var db = await _factory.CreateDbContextAsync())
        {
            invoices = await db.Invoices
                .Include(i => i.Contract)!.ThenInclude(c => c!.Premise)
                .Include(i => i.Contract)!.ThenInclude(c => c!.Tenant)
                .Where(i => i.Contract!.Premise!.LandlordId == landlordId &&
                            i.IssuedAt >= from && i.IssuedAt <= to.AddDays(1))
                .OrderBy(i => i.IssuedAt)
                .ToListAsync();
        }

        decimal total = invoices.Sum(i => i.Amount);
        decimal paid = invoices.Where(i => i.Status == InvoiceStatus.Paid).Sum(i => i.Amount);
        decimal outstanding = total - paid;

        var rows = new List<string[]>
        {
            new[] { "№", "Дата", "Помещение", "Арендатор", "Сумма, BYN", "Статус" }
        };
        int n = 1;
        foreach (var i in invoices)
        {
            rows.Add(new[]
            {
                n++.ToString(),
                i.IssuedAt.ToString("dd.MM.yyyy"),
                i.Contract?.Premise?.Title ?? "—",
                i.Contract?.Tenant?.FullName ?? "—",
                i.Amount.ToString("0.00"),
                i.Status.Ru()
            });
        }

        var summary = new (string, string)[]
        {
            ("Период", $"{from:dd.MM.yyyy} — {to:dd.MM.yyyy}"),
            ("Всего выставлено счетов", invoices.Count.ToString()),
            ("Общая сумма", $"{total:0.00} BYN"),
            ("Оплачено", $"{paid:0.00} BYN"),
            ("Задолженность", $"{outstanding:0.00} BYN")
        };

        return BuildDocument(
            title: "Финансовый отчёт",
            subtitle: $"Арендодатель: {landlordName}",
            summary: summary,
            tableHeaderAndRows: rows);
    }

    public async Task<byte[]> BuildOccupancyReportAsync(string landlordId, string landlordName)
    {
        List<Premise> premises;
        await using (var db = await _factory.CreateDbContextAsync())
        {
            premises = await db.Premises
                .Where(p => p.LandlordId == landlordId)
                .Include(p => p.Contracts)
                .ToListAsync();
        }

        var today = DateTime.UtcNow.Date;
        double totalArea = premises.Sum(p => p.Area);
        double rentedArea = premises
            .Where(p => p.Contracts.Any(c => c.Status == ContractStatus.Active && c.StartDate <= today && today <= c.EndDate))
            .Sum(p => p.Area);
        double occupancy = totalArea > 0 ? rentedArea / totalArea * 100 : 0;

        var rows = new List<string[]>
        {
            new[] { "№", "Помещение", "Тип", "Площадь, м²", "Статус" }
        };
        int n = 1;
        foreach (var p in premises)
        {
            rows.Add(new[]
            {
                n++.ToString(), p.Title, p.Type.Ru(), p.Area.ToString("0.0"), p.Status.Ru()
            });
        }

        var summary = new (string, string)[]
        {
            ("Всего помещений", premises.Count.ToString()),
            ("Общая площадь", $"{totalArea:0.0} м²"),
            ("Сдано в аренду", $"{rentedArea:0.0} м²"),
            ("Заполняемость", $"{occupancy:0.0} %")
        };

        return BuildDocument(
            title: "Отчёт о заполняемости площадей",
            subtitle: $"Арендодатель: {landlordName}",
            summary: summary,
            tableHeaderAndRows: rows);
    }

    public async Task<byte[]?> BuildInvoiceAsync(int invoiceId, string requesterId)
    {
        Invoice? inv;
        await using (var db = await _factory.CreateDbContextAsync())
        {
            inv = await db.Invoices
                .Include(i => i.Contract)!.ThenInclude(c => c!.Premise)!.ThenInclude(p => p!.Landlord)
                .Include(i => i.Contract)!.ThenInclude(c => c!.Tenant)
                .FirstOrDefaultAsync(i => i.Id == invoiceId);
        }
        if (inv is null) return null;

        var tenantId = inv.Contract?.TenantId;
        var landlordId = inv.Contract?.Premise?.LandlordId;
        if (requesterId != tenantId && requesterId != landlordId) return null;

        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document();
            var body = main.Document.AppendChild(new Body());

            body.AppendChild(MakeHeading($"Счёт на оплату № {inv.Id}", 32, true));
            body.AppendChild(MakeParagraph($"Дата выставления: {inv.IssuedAt:dd.MM.yyyy}", 22));
            body.AppendChild(MakeParagraph($"Срок оплаты: {inv.DueDate:dd.MM.yyyy}", 22));
            body.AppendChild(MakeParagraph("", 20));

            body.AppendChild(MakeKeyValue("Помещение", inv.Contract?.Premise?.Title ?? "—"));
            body.AppendChild(MakeKeyValue("Адрес", inv.Contract?.Premise?.Address ?? "—"));
            body.AppendChild(MakeKeyValue("Арендодатель", inv.Contract?.Premise?.Landlord?.FullName ?? "—"));
            body.AppendChild(MakeKeyValue("Арендатор", inv.Contract?.Tenant?.FullName ?? "—"));
            body.AppendChild(MakeKeyValue("Назначение платежа",
                string.IsNullOrWhiteSpace(inv.Note) ? "Аренда помещения" : inv.Note));
            body.AppendChild(MakeKeyValue("Статус", inv.Status.Ru()));
            body.AppendChild(MakeParagraph("", 20));
            body.AppendChild(MakeHeading($"Сумма к оплате: {inv.Amount:0.00} BYN", 28, true));

            main.Document.Save();
        }
        return ms.ToArray();
    }

    private static byte[] BuildDocument(
        string title, string subtitle,
        (string Label, string Value)[] summary,
        List<string[]> tableHeaderAndRows)
    {
        using var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new Document();
            var body = main.Document.AppendChild(new Body());

            body.AppendChild(MakeHeading(title, 32, true));
            body.AppendChild(MakeHeading(subtitle, 24, false));
            body.AppendChild(MakeParagraph($"Сформировано: {DateTime.Now:dd.MM.yyyy HH:mm}", 20, italic: true));
            body.AppendChild(MakeParagraph("", 20));

            body.AppendChild(MakeHeading("Сводные показатели", 26, true));
            foreach (var (label, value) in summary)
                body.AppendChild(MakeKeyValue(label, value));

            body.AppendChild(MakeParagraph("", 20));

            body.AppendChild(MakeHeading("Детализация", 26, true));
            body.AppendChild(MakeTable(tableHeaderAndRows));

            main.Document.Save();
        }
        return ms.ToArray();
    }

    private static Paragraph MakeHeading(string text, int halfPointSize, bool bold)
    {
        var run = new Run(new RunProperties(
            new Bold { Val = OnOffValue.FromBoolean(bold) },
            new FontSize { Val = halfPointSize.ToString() },
            new RunFonts { Ascii = "Arial", HighAnsi = "Arial" }),
            new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return new Paragraph(new ParagraphProperties(new SpacingBetweenLines { Before = "120", After = "120" }), run);
    }

    private static Paragraph MakeParagraph(string text, int halfPointSize, bool italic = false)
    {
        var rp = new RunProperties(
            new FontSize { Val = halfPointSize.ToString() },
            new RunFonts { Ascii = "Arial", HighAnsi = "Arial" });
        if (italic) rp.AppendChild(new Italic());
        var run = new Run(rp, new Text(text) { Space = SpaceProcessingModeValues.Preserve });
        return new Paragraph(run);
    }

    private static Paragraph MakeKeyValue(string label, string value)
    {
        var p = new Paragraph();
        p.AppendChild(new Run(
            new RunProperties(new Bold(), new FontSize { Val = "22" }, new RunFonts { Ascii = "Arial", HighAnsi = "Arial" }),
            new Text($"{label}: ") { Space = SpaceProcessingModeValues.Preserve }));
        p.AppendChild(new Run(
            new RunProperties(new FontSize { Val = "22" }, new RunFonts { Ascii = "Arial", HighAnsi = "Arial" }),
            new Text(value) { Space = SpaceProcessingModeValues.Preserve }));
        return p;
    }

    private static Table MakeTable(List<string[]> rows)
    {
        var table = new Table();

        var borders = new TableBorders(
            new TopBorder { Val = BorderValues.Single, Size = 4, Color = "CCCCCC" },
            new BottomBorder { Val = BorderValues.Single, Size = 4, Color = "CCCCCC" },
            new LeftBorder { Val = BorderValues.Single, Size = 4, Color = "CCCCCC" },
            new RightBorder { Val = BorderValues.Single, Size = 4, Color = "CCCCCC" },
            new InsideHorizontalBorder { Val = BorderValues.Single, Size = 4, Color = "CCCCCC" },
            new InsideVerticalBorder { Val = BorderValues.Single, Size = 4, Color = "CCCCCC" });

        table.AppendChild(new TableProperties(
            new TableWidth { Width = "5000", Type = TableWidthUnitValues.Pct },
            borders));

        bool header = true;
        foreach (var cells in rows)
        {
            var tr = new TableRow();
            foreach (var cell in cells)
            {
                var rp = new RunProperties(
                    new FontSize { Val = "20" },
                    new RunFonts { Ascii = "Arial", HighAnsi = "Arial" });
                if (header) rp.AppendChild(new Bold());

                var tc = new TableCell(
                    new TableCellProperties(
                        new TableCellMargin(
                            new TopMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
                            new BottomMargin { Width = "60", Type = TableWidthUnitValues.Dxa },
                            new LeftMargin { Width = "100", Type = TableWidthUnitValues.Dxa },
                            new RightMargin { Width = "100", Type = TableWidthUnitValues.Dxa }),
                        header ? new Shading { Val = ShadingPatternValues.Clear, Fill = "FFE6D2" } : new Shading { Val = ShadingPatternValues.Clear, Fill = "FFFFFF" }),
                    new Paragraph(new Run(rp, new Text(cell) { Space = SpaceProcessingModeValues.Preserve })));
                tr.AppendChild(tc);
            }
            table.AppendChild(tr);
            header = false;
        }
        return table;
    }
}
