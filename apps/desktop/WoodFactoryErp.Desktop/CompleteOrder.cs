using System.Globalization;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media.Imaging;
using Avalonia.Platform.Storage;

namespace WoodFactoryErp.Desktop;

public partial class MainWindow
{
    private readonly List<PendingOrderAttachment> pendingOrderAttachments = [];
    private readonly List<PendingOrderAccessory> pendingOrderAccessories = [];
    private bool returnToOrderAfterCustomer;
    private string? pendingOrderCustomerId;
    private readonly List<Bitmap> orderPreviewImages = [];
    private T OrderControl<T>(string name) where T : Control => this.FindControl<T>(name)!;

    private async void AddOrderCustomerAsync(object? sender, RoutedEventArgs e)
    {
        returnToOrderAfterCustomer = true;
        await LoadMasterDataAsync();
        HomePanel.IsVisible = InventoryPanel.IsVisible = OrdersPanel.IsVisible = false;
        MasterDataPanel.IsVisible = true;
        OrderControl<TabControl>("MasterDataTabs").SelectedIndex = 2;
        PartyTypeComboBox.SelectedIndex = 0;
        PartyCodeTextBox.Focus();
        MasterDataMessageTextBlock.Text = "أضف العميل ثم احفظه للعودة إلى الطلب. بيانات الطلب محفوظة في النموذج.";
    }

    private void RefreshLegacyOrderAccessoryChoices()
    {
        var combo = OrderControl<ComboBox>("OrderAccessoryComboBox");
        var selected = (combo.SelectedItem as Choice)?.Value;
        var choices = (currentMasterData?.Items ?? []).Where(x => x.ItemType == "accessory")
            .Select(x => new Choice(x.Id, $"{x.Code} — {x.NameAr}")).ToList();
        combo.ItemsSource = choices;
        combo.SelectedIndex = Math.Max(-1, choices.FindIndex(x => x.Value == selected));
        if (combo.SelectedIndex < 0 && choices.Count > 0) combo.SelectedIndex = 0;
    }

    private void AddOrderAccessory(object? sender, RoutedEventArgs e)
    {
        if (OrderControl<ComboBox>("OrderAccessoryComboBox").SelectedItem is not Choice item ||
            !decimal.TryParse(OrderControl<TextBox>("OrderAccessoryQuantityTextBox").Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var quantity) ||
            quantity <= 0 || quantity > 999999999 || decimal.Round(quantity, 4) != quantity)
        { OrdersMessageTextBlock.Text = "اختر إكسسواراً وأدخل عدده بقيمة أكبر من صفر (حتى 4 منازل عشرية)."; return; }
        var previous = pendingOrderAccessories.FindIndex(x => x.ItemId == item.Value);
        if (previous >= 0) pendingOrderAccessories[previous] = new(item.Value, item.Label, quantity);
        else pendingOrderAccessories.Add(new(item.Value, item.Label, quantity));
        OrderControl<TextBox>("OrderAccessoryQuantityTextBox").Text = string.Empty;
        RenderOrderDraft();
    }

    private async void AddOrderAttachmentAsync(object? sender, RoutedEventArgs e)
    {
        var name = OrderControl<TextBox>("OrderAttachmentNameTextBox").Text?.Trim();
        if (string.IsNullOrWhiteSpace(name) || name.Length > 240)
        { OrdersMessageTextBlock.Text = "أدخل اسم المرفق (حتى 240 حرفاً)."; return; }
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions { Title = "اختيار مرفق الطلب", AllowMultiple = false });
        if (files.Count == 0) return;
        try
        {
            if (pendingOrderAttachments.Count >= 100) throw new InvalidOperationException("الحد الأقصى 100 مرفق للطلب.");
            using var stream = await files[0].OpenReadAsync();
            using var memory = new MemoryStream();
            var buffer = new byte[81920];
            int read;
            while ((read = await stream.ReadAsync(buffer)) > 0)
            {
                if (memory.Length + read > 20 * 1024 * 1024) throw new InvalidOperationException("حجم كل مرفق لا يتجاوز 20 MB.");
                memory.Write(buffer, 0, read);
            }
            if (memory.Length == 0 || pendingOrderAttachments.Sum(x => (long)x.Content.Length) + memory.Length > 100 * 1024 * 1024)
                throw new InvalidOperationException("اختر ملفاً غير فارغ؛ إجمالي المرفقات لا يتجاوز 100 MB.");
            var extension = Path.GetExtension(files[0].Name).ToLowerInvariant();
            string[] allowed = [".jpg", ".jpeg", ".png", ".webp", ".gif", ".pdf", ".xlsx", ".xls", ".csv", ".dxf", ".dwg", ".skp"];
            if (!allowed.Contains(extension)) throw new InvalidOperationException("اختر صورة أو PDF أو جدولاً أو ملف تصميم.");
            var mime = extension switch { ".jpg" or ".jpeg" => "image/jpeg", ".png" => "image/png", ".gif" => "image/gif", ".webp" => "image/webp", ".pdf" => "application/pdf", _ => "application/octet-stream" };
            var attachmentType = OrderControl<ComboBox>("OrderAttachmentTypeComboBox").SelectedIndex switch { 1 => "product_reference", 2 => "other", _ => "cut_list" };
            pendingOrderAttachments.Add(new(name, OrderControl<TextBox>("OrderAttachmentDescriptionTextBox").Text?.Trim(),
                OrderControl<TextBox>("OrderAttachmentColorTextBox").Text?.Trim(), attachmentType, files[0].Name, mime, memory.ToArray()));
            OrderControl<TextBox>("OrderAttachmentNameTextBox").Text = string.Empty;
            OrderControl<TextBox>("OrderAttachmentDescriptionTextBox").Text = string.Empty;
            OrderControl<TextBox>("OrderAttachmentColorTextBox").Text = string.Empty;
            RenderOrderDraft();
            OrdersMessageTextBlock.Text = "أضيف المرفق للنموذج. اضغط تأكيد الطلب لحفظ البيانات والمرفقات.";
        }
        catch (Exception ex) { OrdersMessageTextBlock.Text = FriendlyError(ex); }
    }

    private void RenderLegacyOrderDraft()
    {
        var attachmentsPanel = OrderControl<StackPanel>("PendingOrderAttachmentsPanel");
        attachmentsPanel.Children.Clear();
        foreach (var file in pendingOrderAttachments.ToList())
        {
            var row = new StackPanel { Spacing = 4 };
            row.Children.Add(new TextBlock { Text = $"{file.Name} — {file.FileName}", TextWrapping = Avalonia.Media.TextWrapping.Wrap });
            row.Children.Add(new TextBlock { Text = $"{file.Description}" + (string.IsNullOrWhiteSpace(file.Color) ? "" : $" | اللون: {file.Color}"), TextWrapping = Avalonia.Media.TextWrapping.Wrap });
            var remove = new Button { Content = "إزالة المرفق" };
            remove.Click += (_, _) => { pendingOrderAttachments.Remove(file); RenderOrderDraft(); };
            row.Children.Add(remove);
            attachmentsPanel.Children.Add(row);
        }
        var accessoriesPanel = OrderControl<StackPanel>("PendingOrderAccessoriesPanel");
        accessoriesPanel.Children.Clear();
        foreach (var line in pendingOrderAccessories.ToList())
        {
            var row = new StackPanel { Orientation = Avalonia.Layout.Orientation.Horizontal, Spacing = 8 };
            row.Children.Add(new TextBlock { Text = $"{line.Label} | العدد: {line.Quantity}" });
            var remove = new Button { Content = "إزالة" };
            remove.Click += (_, _) => { pendingOrderAccessories.Remove(line); RenderOrderDraft(); };
            row.Children.Add(remove);
            accessoriesPanel.Children.Add(row);
        }
    }

    private async Task ConfirmCompleteOrderAsync() => await ConfirmWorkspaceOrderAsync();

    private async Task LoadCompleteOrderDetailsAsync() => await LoadWorkspaceOrderDetailsAsync();

    private async Task<byte[]> ReadOrderAttachmentAsync(string orderId, string fileId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"orders/{orderId}/attachments/{fileId}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await HttpClient.SendAsync(request);
        await EnsureApiSuccessAsync(response);
        return await response.Content.ReadAsByteArrayAsync();
    }

    private sealed record PendingOrderAttachment(string Name, string? Description, string? Color, string AttachmentType, string FileName, string MimeType, byte[] Content);
    private sealed record PendingOrderAccessory(string ItemId, string Label, decimal Quantity);
    private sealed record ConfirmedOrderResult(string Id, string OrderNo);
    private sealed record CompleteOrderDetail(string Id, string OrderNo, string? ProductCode, string? ProductName, string CustomerNameAr, int? TotalBoards, string? WastePercentage, string? Notes, List<OrderAttachmentDetail> Attachments, List<OrderAccessoryDetail> Accessories, List<OrderAccessoryDetail>? Materials, string? OrderName, string? OrderDate, string? RequestedDeliveryDate, List<OrderLinkedJob>? Jobs);
    private sealed record OrderAttachmentDetail(string Id, string Name, string? Description, string? Color, string FileName, string MimeType);
    private sealed record OrderAccessoryDetail(string ItemCode, string ItemNameAr, string Quantity, string UnitCode);
}
