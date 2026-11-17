using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

namespace WoodFactoryErp.Desktop.Documents;

internal static class FinanceDocumentPdf
{
    public static byte[] Generate(string title,string reference,string serial,string date,string party,string net,string tax,string delivery,string total,string notes,IReadOnlyList<string[]> rows)
    {
        QuestPDF.Settings.License=LicenseType.Community;
        QuestPDF.Settings.UseSystemFonts=true;
        return Document.Create(document=>document.Page(page=>{
            page.Size(PageSizes.A4);page.Margin(30);page.ContentFromRightToLeft();page.DefaultTextStyle(x=>x.FontSize(11));
            page.Content().Column(column=>{
                column.Spacing(12);column.Item().Text(title).Bold().FontSize(22);
                column.Item().Text("مستند داخلي — ليس فاتورة ضريبية أو إلكترونية قانونية").FontColor(Colors.Red.Darken2);
                column.Item().Text($"المرجع: {reference} | الرقم الداخلي: {serial}\nالتاريخ: {date}\nالطرف: {party}");
                if(rows.Count>0)column.Item().Table(table=>{
                    table.ColumnsDefinition(c=>{c.RelativeColumn(3);for(var i=0;i<5;i++)c.RelativeColumn();});
                    table.Header(h=>{foreach(var label in new[]{"البند","العدد","سعر الوحدة","خصم","ضريبة","الإجمالي"})h.Cell().Background(Colors.Grey.Lighten3).Padding(5).Text(label).Bold();});
                    foreach(var row in rows)foreach(var cell in row)table.Cell().BorderBottom(0.5f).BorderColor(Colors.Grey.Lighten2).Padding(5).Text(cell);
                });
                column.Item().Text($"الصافي بعد الخصم: {net}\nالضريبة المنفصلة: {tax}\nرسوم التوصيل: {delivery}\nالإجمالي: {total} جنيه مصري").Bold();
                column.Item().Text("الأثر المالي يحدد حسب نوع المستند. البيع والفاتورة مستقلان عن التحصيل؛ راجع سندات الدفع والتحصيل والمرتجعات والتصحيحات المرتبطة في السجل.");
                if(!string.IsNullOrWhiteSpace(notes))column.Item().Text("ملاحظات: "+notes);
            });page.Footer().AlignCenter().Text(t=>{t.Span("صفحة ");t.CurrentPageNumber();});
        })).GeneratePdf();
    }
}
