using System.Globalization;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace WoodFactoryErp.Desktop.Documents;

internal sealed record ProductionWorkOrderPdfModel(
    string OrderNo,
    string ProductionDate,
    string ProductCode,
    string ProductName,
    string Quantity,
    string? CustomerOrderNo,
    int? EngineeringRevision,
    IReadOnlyList<ProductionWorkOrderPdfLine> Lines);

internal sealed record ProductionWorkOrderPdfLine(
    string? CatalogCode,
    string ItemNameAr,
    string Specification,
    string UnitCode,
    string? Quantity,
    bool IsSelected,
    bool IsCustom);

internal static class ProductionWorkOrderPdf
{
    public static byte[] Generate(ProductionWorkOrderPdfModel model)
    {
        // QuestPDF Community license is intended for qualifying organizations/individuals.
        // See PATCH-README.md and confirm eligibility before commercial deployment.
        QuestPDF.Settings.License = LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts = true;

        var catalogLines = model.Lines.Where(x => !x.IsCustom).ToList();
        var customLines = model.Lines.Where(x => x.IsCustom).ToList();
        var customLineOne = customLines.ElementAtOrDefault(0)
            ?? new ProductionWorkOrderPdfLine(null, string.Empty, string.Empty, string.Empty, null, false, true);
        var customLineTwo = customLines.ElementAtOrDefault(1)
            ?? new ProductionWorkOrderPdfLine(null, string.Empty, string.Empty, string.Empty, null, false, true);

        return Document.Create(document =>
        {
            document.Page(page =>
            {
                ConfigurePage(page);
                page.Content().Column(column =>
                {
                    column.Spacing(7);
                    column.Item().Element(c => ComposeHeader(c, model));
                    column.Item().Element(c => ComposeOrderHeader(c, model));
                    column.Item().Element(c => ComposeCatalogTable(c, catalogLines));
                    column.Item().Element(c => ComposeCustomItemsTable(c, new[] { customLineOne }, header: true, rowNumber: 1));
                });
                ComposeFooter(page);
            });

            document.Page(page =>
            {
                ConfigurePage(page);
                page.Content().Column(column =>
                {
                    column.Spacing(14);
                    column.Item().Element(c => ComposeCustomItemsTable(c, new[] { customLineTwo }, header: true, rowNumber: 2));
                    column.Item().Element(ComposeSignatures);
                });
                ComposeFooter(page);
            });
        }).GeneratePdf();
    }

    private static void ConfigurePage(PageDescriptor page)
    {
        page.Size(PageSizes.A3);
        page.Margin(14, Unit.Millimetre);
        page.DefaultTextStyle(style => style.FontFamily("Arial").FontSize(9).FontColor(Colors.Grey.Darken4));
        page.ContentFromRightToLeft();
    }

    private static void ComposeHeader(IContainer container, ProductionWorkOrderPdfModel model)
    {
        var year = DateTime.TryParse(model.ProductionDate, out var parsedDate) ? parsedDate.Year : DateTime.Today.Year;
        container
            .Background(Colors.Blue.Darken4)
            .Border(1)
            .BorderColor(Colors.Blue.Darken4)
            .Padding(12)
            .Row(row =>
            {
                row.RelativeItem(3).Column(column =>
                {
                    column.Item().AlignRight().Text("MHM Furniture & Manufacturing")
                        .FontSize(19).SemiBold().FontColor(Colors.White);
                    column.Item().AlignRight().Text("أمر شغل وتجهيز إنتاج (Job Order)")
                        .FontSize(16).SemiBold().FontColor(Colors.Blue.Lighten3);
                });
                row.RelativeItem(1).AlignCenter().AlignMiddle()
                    .Border(1).BorderColor(Colors.Blue.Lighten2).Padding(8)
                    .AlignCenter().Text($"قسم الإنتاج والجودة\nنموذج رقم: JO-{year}")
                    .FontSize(10).FontColor(Colors.White).SemiBold();
            });
    }

    private static void ComposeOrderHeader(IContainer container, ProductionWorkOrderPdfModel model)
    {
        container
            .Background(Colors.Grey.Lighten5)
            .Border(1).BorderColor(Colors.Blue.Lighten3)
            .Padding(10)
            .Column(column =>
            {
                column.Spacing(7);
                column.Item().Row(row =>
                {
                    HeaderField(row.RelativeItem(), "رقم الأمر", model.OrderNo, ltrValue: true);
                    HeaderField(row.RelativeItem(), "كود المنتج", model.ProductCode, ltrValue: true);
                    HeaderField(row.RelativeItem(), "التاريخ", model.ProductionDate, ltrValue: true);
                });
                column.Item().Row(row =>
                {
                    HeaderField(row.RelativeItem(3), "اسم المنتج", model.ProductName);
                    HeaderField(row.RelativeItem(), "الكمية", model.Quantity, ltrValue: true);
                });
                if (!string.IsNullOrWhiteSpace(model.CustomerOrderNo) || model.EngineeringRevision is not null)
                {
                    var reference = string.Join("  |  ", new[]
                    {
                        string.IsNullOrWhiteSpace(model.CustomerOrderNo) ? null : $"طلب العميل: {model.CustomerOrderNo}",
                        model.EngineeringRevision is null ? null : $"النسخة الفنية: R{model.EngineeringRevision}"
                    }.Where(x => x is not null));
                    column.Item().AlignRight().Text(reference).FontSize(8).FontColor(Colors.Grey.Darken2);
                }
            });
    }

    private static void HeaderField(IContainer container, string label, string value, bool ltrValue = false)
    {
        container.Row(row =>
        {
            row.AutoItem().Text($"{label}: ").SemiBold();
            var text = row.RelativeItem().BorderBottom(0.6f).BorderColor(Colors.Grey.Lighten1).PaddingBottom(2);
            if (ltrValue)
                text.AlignLeft().Text(string.IsNullOrWhiteSpace(value) ? "—" : value).FontSize(9);
            else
                text.AlignRight().Text(string.IsNullOrWhiteSpace(value) ? "—" : value).FontSize(9);
        });
    }

    private static void ComposeCatalogTable(IContainer container, IReadOnlyList<ProductionWorkOrderPdfLine> lines)
    {
        container.Table(table =>
        {
            DefineWorkOrderColumns(table);
            table.Header(header => ComposeItemHeader(header, Colors.Blue.Darken2));
            for (var index = 0; index < lines.Count; index++)
            {
                var line = lines[index];
                var alternate = index % 2 == 1;
                table.Cell().Element(c => Cell(c, alternate)).AlignCenter().Element(c => DrawCheckbox(c, line.IsSelected));
                table.Cell().Element(c => Cell(c, alternate)).AlignRight().Text(line.ItemNameAr).SemiBold();
                table.Cell().Element(c => Cell(c, alternate)).AlignRight().Text(line.Specification ?? string.Empty);
                table.Cell().Element(c => Cell(c, alternate)).AlignCenter().Text(line.UnitCode);
                table.Cell().Element(c => Cell(c, alternate)).AlignCenter().Text(line.Quantity ?? string.Empty);
            }
        });
    }

    private static void ComposeCustomItemsTable(IContainer container, IReadOnlyList<ProductionWorkOrderPdfLine> lines, bool header, int rowNumber)
    {
        container.Table(table =>
        {
            DefineCustomColumns(table);
            if (header)
                table.Header(tableHeader => ComposeItemHeader(tableHeader, Colors.Teal.Darken2, custom: true));
            foreach (var line in lines)
            {
                table.Cell().Element(c => Cell(c, false)).AlignCenter().Text(rowNumber.ToString(CultureInfo.InvariantCulture));
                table.Cell().Element(c => Cell(c, false)).AlignRight().Text(line.ItemNameAr ?? string.Empty);
                table.Cell().Element(c => Cell(c, false)).AlignRight().Text(line.Specification ?? string.Empty);
                table.Cell().Element(c => Cell(c, false)).AlignCenter().Text(line.UnitCode);
                table.Cell().Element(c => Cell(c, false)).AlignCenter().Text(line.Quantity ?? string.Empty);
            }
        });
    }

    private static void DefineWorkOrderColumns(TableDescriptor table)
    {
        // RTL flow: checkbox, item, description/specification, unit, quantity.
        table.ColumnsDefinition(columns =>
        {
            columns.ConstantColumn(34);
            columns.RelativeColumn(2.0f);
            columns.RelativeColumn(4.1f);
            columns.RelativeColumn(1.15f);
            columns.RelativeColumn(1.05f);
        });
    }

    private static void DefineCustomColumns(TableDescriptor table)
    {
        // The paper template continuation row runs from right to left: number, custom description, unit, quantity.
        table.ColumnsDefinition(columns =>
        {
            columns.ConstantColumn(42);
            columns.RelativeColumn(1.2f);
            columns.RelativeColumn(4.4f);
            columns.RelativeColumn(1.2f);
            columns.ConstantColumn(36);
        });
    }

    private static void ComposeItemHeader(TableCellDescriptor header, string color, bool custom = false)
    {
        if (custom)
        {
            header.Cell().Element(c => HeaderCell(c, color)).AlignCenter().Text("م").FontColor(Colors.White).SemiBold();
            header.Cell().Element(c => HeaderCell(c, color)).AlignRight().Text("إضافات أخرى (اكتب البند حسب الحاجة)").FontColor(Colors.White).SemiBold();
            header.Cell().Element(c => HeaderCell(c, color)).AlignRight().Text("المواصفة / البيان").FontColor(Colors.White).SemiBold();
            header.Cell().Element(c => HeaderCell(c, color)).AlignCenter().Text("الوحدة").FontColor(Colors.White).SemiBold();
            header.Cell().Element(c => HeaderCell(c, color)).AlignCenter().Text("العدد").FontColor(Colors.White).SemiBold();
            return;
        }

        header.Cell().Element(c => HeaderCell(c, color)).AlignCenter().Text("اختيار").FontColor(Colors.White).SemiBold();
        header.Cell().Element(c => HeaderCell(c, color)).AlignRight().Text("اسم البند / الخامة").FontColor(Colors.White).SemiBold();
        header.Cell().Element(c => HeaderCell(c, color)).AlignRight().Text("المواصفات / المقاس / البيان").FontColor(Colors.White).SemiBold();
        header.Cell().Element(c => HeaderCell(c, color)).AlignCenter().Text("الوحدة").FontColor(Colors.White).SemiBold();
        header.Cell().Element(c => HeaderCell(c, color)).AlignCenter().Text("العدد / الكمية").FontColor(Colors.White).SemiBold();
    }

    private static IContainer Cell(IContainer container, bool alternate) => container
        .Background(alternate ? Colors.Grey.Lighten4 : Colors.White)
        .BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2)
        .PaddingVertical(4).PaddingHorizontal(4).AlignMiddle();

    private static IContainer HeaderCell(IContainer container, string color) => container
        .Background(color).PaddingVertical(6).PaddingHorizontal(4).AlignMiddle();

    private static void DrawCheckbox(IContainer container, bool selected)
    {
        container
            .Width(18).Height(18)
            .Border(1).BorderColor(Colors.Blue.Medium)
            .AlignCenter().AlignMiddle()
            .Text(selected ? "✓" : string.Empty)
            .FontSize(10).FontColor(Colors.Blue.Darken2).SemiBold();
    }

    private static void ComposeSignatures(IContainer container)
    {
        container.Row(row =>
        {
            row.RelativeItem().Element(c => SignatureBox(c, "مسؤول التقطيع والشريط"));
            row.ConstantItem(8);
            row.RelativeItem().Element(c => SignatureBox(c, "مسؤول التجميع"));
            row.ConstantItem(8);
            row.RelativeItem().Element(c => SignatureBox(c, "مسؤول الجودة"));
        });
    }

    private static void SignatureBox(IContainer container, string title)
    {
        container.Border(1).BorderColor(Colors.Blue.Lighten3).Background(Colors.Grey.Lighten5).Padding(10).Column(column =>
        {
            column.Item().AlignCenter().Text(title).FontSize(11).FontColor(Colors.Blue.Darken3).SemiBold();
            column.Item().PaddingTop(5).LineHorizontal(0.6f).LineColor(Colors.Grey.Lighten2);
            column.Item().PaddingTop(8).AlignRight().Text("الاسم: __________________").FontSize(9);
            column.Item().PaddingTop(6).AlignRight().Text("التوقيع: ________________").FontSize(9);
        });
    }

    private static void ComposeFooter(PageDescriptor page)
    {
        page.Footer()
            .AlignCenter()
            .DefaultTextStyle(style => style.FontSize(8).FontColor(Colors.Grey.Darken1))
            .Text(text =>
            {
                text.Span("صفحة ");
                text.CurrentPageNumber();
                text.Span(" / ");
                text.TotalPages();
            });
    }
}
