using System.Globalization;
using System.Net.Http.Json;

namespace WoodFactoryErp.Desktop;

public partial class MainWindow
{
    private int productionWorkOrderLoadVersion;
    private int linkedJobLoadVersion;
    private static string BuildLinkedJobSummary(ProductionWorkOrderDetails po)=>$"{po.OrderNo} — {po.JobName??po.OrderNo}\nطلب العميل: {po.CustomerOrderNo??"إنتاج مخزني"} | المنتج: {po.ProductCode} — {po.ProductName}\nالكمية: {po.PlannedQuantity} | البداية: {po.ProductionDate} | النهاية المخططة: {po.PlannedEndDate??"غير محددة"} | {ExecutionStatus(po.Status)}";
    private async Task<ProductionWorkOrderResponse?> FetchProductionWorkOrderAsync(string id)
    {
        using var request=new HttpRequestMessage(HttpMethod.Get,$"orders/production-orders/{id}/work-order");request.Headers.Authorization=new("Bearer",accessToken);using var response=await HttpClient.SendAsync(request);await EnsureApiSuccessAsync(response);return await response.Content.ReadFromJsonAsync<ProductionWorkOrderResponse>(JsonOptions);
    }

    private void PrepareJobHeaderForSelectedOrder()
    {
        ClearProductionWorkOrderEditor();
        var id = (SelectedOrderComboBox.SelectedItem as CustomerOrderChoice)?.Id;
        var order = currentOrders.FirstOrDefault(x => x.Id == id);
        ProductionOrderNumberTextBox.Text = string.Empty;
        ProductionProductCodeTextBox.Text = order?.ProductCode ?? string.Empty;
        ProductionProductNameTextBox.Text = order?.ProductName ?? string.Empty;
        ProductionPlannedQuantityTextBox.Text = "1";
        ProductionDateTextBox.Text = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        CreateProductionOrderButton.IsEnabled = order is not null && order.Status == "open";
        if(jobCreateDate is not null){jobCreateDate.Text=order?.OrderDate??DateTime.Today.ToString("yyyy-MM-dd");jobCreateEndDate.Text=order?.RequestedDeliveryDate??"";jobCreateQuantity.Text="1";jobCreateName.Text="";jobParentSummary.Text=order is null?"اختر طلب عميل أولاً.":$"{OrderLabel(order)}\nالمنتج: {order.ProductCode} — {order.ProductName}\nالبداية: {order.OrderDate} | التسليم: {order.RequestedDeliveryDate??"غير محدد"}";}
    }
    private async Task CreateLinkedJobCoreAsync()
    {
        try{
            if(SelectedOrderComboBox.SelectedItem is not CustomerOrderChoice selected)throw new InvalidOperationException("اختر طلب العميل أولاً.");
            var code=Nonempty(ProductionOrderNumberTextBox.Text,"اكتب كود أمر الشغل.");var name=Nonempty(jobCreateName.Text,"اكتب اسم أمر الشغل.");var start=ValidDate(jobCreateDate.Text);string? end=string.IsNullOrWhiteSpace(jobCreateEndDate.Text)?null:ValidDate(jobCreateEndDate.Text);if(end is not null&&string.CompareOrdinal(end,start)<0)throw new InvalidOperationException("نهاية التنفيذ لا تسبق بدايته.");var quantity=WorkspaceQuantity(jobCreateQuantity.Text);
            CreateProductionOrderButton.IsEnabled=false;using var request=new HttpRequestMessage(HttpMethod.Post,$"orders/{selected.Id}/production-orders"){Content=JsonContent.Create(new{orderNo=code,jobName=name,productionDate=start,plannedEndDate=end,plannedQuantity=quantity},options:JsonOptions)};request.Headers.Authorization=new("Bearer",accessToken);using var response=await HttpClient.SendAsync(request);await EnsureApiSuccessAsync(response);var created=await response.Content.ReadFromJsonAsync<CreatedProductionOrder>(JsonOptions)??throw new InvalidOperationException("لم تصل هوية أمر الشغل المحفوظ.");
            if((SelectedOrderComboBox.SelectedItem as CustomerOrderChoice)?.Id!=selected.Id)return;
            ProductionOrderNumberTextBox.Text=jobCreateName.Text="";await LoadLinkedJobOrdersAsync(created.Id);OrdersMessageTextBlock.Text=$"تم إنشاء أمر الشغل {created.OrderNo} وربطه بطلب العميل. يمكنك إضافة أمر آخر لنفس الطلب.";
        }catch(Exception ex){OrdersMessageTextBlock.Text=FriendlyError(ex);}finally{CreateProductionOrderButton.IsEnabled=(SelectedOrderComboBox.SelectedItem as CustomerOrderChoice) is not null;}
    }
    private async Task LoadLinkedJobOrdersAsync(string? selectedId=null)
    {
        if(string.IsNullOrWhiteSpace(accessToken))return;var version=++linkedJobLoadVersion;var parent=(SelectedOrderComboBox.SelectedItem as CustomerOrderChoice)?.Id;
        var desired=selectedId??(ProductionOrderComboBox.SelectedItem as ProductionOrderChoice)?.Id;isLoadingProductionOrders=true;
        try{
            using var request=new HttpRequestMessage(HttpMethod.Get,"orders/production-orders");request.Headers.Authorization=new("Bearer",accessToken);using var response=await HttpClient.SendAsync(request);await EnsureApiSuccessAsync(response);var jobs=await response.Content.ReadFromJsonAsync<List<ProductionOrderView>>(JsonOptions)??[];
            if(version!=linkedJobLoadVersion||parent!=(SelectedOrderComboBox.SelectedItem as CustomerOrderChoice)?.Id)return;
            currentProductionOrders=jobs;var relevant=jobs.Where(j=>j.CustomerOrderId==parent&&parent is not null).ToList();var choices=relevant.Select(j=>new ProductionOrderChoice(j.Id,$"{j.OrderNo} — {j.JobName??j.OrderNo} | {ExecutionStatus(j.Status)}")).ToList();ProductionOrderComboBox.ItemsSource=choices;ProductionOrderComboBox.SelectedIndex=choices.FindIndex(c=>c.Id==desired);
            var id=(ProductionOrderComboBox.SelectedItem as ProductionOrderChoice)?.Id;jobSearchPicker.Set(choices.Select(c=>new ExecChoice(c.Id,c.Label)),id);if(id is null)jobSearchPicker.Select(null);
            ProductionOrdersTextBlock.Text="";
            if(relevant.Count==0)ProductionWorkOrderMessageTextBlock.Text=parent is null?"اختر طلب العميل أولاً.":"لا توجد أوامر شغل لهذا الطلب بعد؛ أنشئ الأمر الأول من المربع أعلاه.";
        }catch(Exception ex){if(version==linkedJobLoadVersion)OrdersMessageTextBlock.Text=FriendlyError(ex);}
        finally{if(version==linkedJobLoadVersion)isLoadingProductionOrders=false;}
        if(version==linkedJobLoadVersion)await LoadProductionWorkOrderAsync();
    }
}
