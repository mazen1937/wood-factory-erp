using System.Diagnostics;
using System.Globalization;
using System.Net.Http.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace WoodFactoryErp.Desktop;

public partial class MainWindow
{
    private ProductionPicker orderSearchPicker=null!,jobCustomerOrderPicker=null!,jobSearchPicker=null!,orderMaterialPicker=null!,orderAccessoryPicker=null!;
    private TextBox orderMaterialQuantity=null!,jobCreateName=null!,jobCreateDate=null!,jobCreateEndDate=null!,jobCreateQuantity=null!;
    private StackPanel orderMaterialDraft=null!;
    private TabControl orderWorkspaceTabs=null!;
    private TextBlock jobParentSummary=null!;
    private bool orderDetailsVisible;
    private int orderDetailsVersion;
    private readonly List<PendingOrderAccessory> pendingOrderMaterials=[];
    private static string OrderLabel(CustomerOrderView o)=>$"{o.OrderNo} — {o.OrderName??o.ProductName??o.ProductCode??o.OrderNo} | {o.CustomerNameAr}";
    private void InitializeOrderWorkspace()
    {
        var previous=(Control)OrdersPanel.Content!;var root=new StackPanel{Spacing=12,Margin=new Thickness(8)};OrdersPanel.Content=root;previous.IsVisible=false;root.Children.Add(previous);
        Control Take(string name){var c=OrderControl<Control>(name);if(c.Parent is Panel p)p.Children.Remove(c);else if(c.Parent is Border b)b.Child=null;else if(c.Parent is ContentControl cc)cc.Content=null;return c;}
        void Reuse(StackPanel p,string label,string name){if(label!="")p.Children.Add(ExecText(label));p.Children.Add(Take(name));}
        root.Children.Add(ExecText("الطلبات والملف الفني",28));root.Children.Add(Take("RefreshOrdersButton"));RefreshOrdersButton.Content="تحديث الطلبات وأوامر الشغل";root.Children.Add(Take("OrdersMessageTextBlock"));
        var search=Card(root,"البحث عن طلب عميل","ابحث بكود الطلب أو اسمه أو العميل. اختيار الطلب يربط به أوامر الشغل؛ التفاصيل تظهر عند طلبها فقط.");orderSearchPicker=new("كود الطلب / اسم الطلب / العميل");search.Children.Add(orderSearchPicker);orderSearchPicker.Changed+=()=>SelectWorkspaceOrderAsync(orderSearchPicker);
        var tabs=orderWorkspaceTabs=new TabControl();root.Children.Add(tabs);var orders=new StackPanel{Spacing=12};
        var editor=OrderControl<StackPanel>("CompleteOrderEditor");editor.Children.Clear();if(editor.Parent is Panel parent)parent.Children.Remove(editor);orders.Children.Add(editor);
        var identity=Card(editor,"١. العميل وبيانات الطلب");Reuse(identity,"العميل","OrderCustomerComboBox");var addCustomer=new Button{Content="إضافة عميل"};addCustomer.Click+=AddOrderCustomerAsync;identity.Children.Add(addCustomer);
        Reuse(identity,"كود الطلب","OrderNumberTextBox");Reuse(identity,"كود المنتج / المواصفة","OrderProductCodeTextBox");Reuse(identity,"اسم الطلب / المنتج","OrderProductNameTextBox");Reuse(identity,"تاريخ البداية — YYYY-MM-DD","OrderDateTextBox");Reuse(identity,"تاريخ التسليم المطلوب — اختياري","OrderDeliveryDateTextBox");
        var files=Card(editor,"٢. المرفقات والكَت لِست","تُحفظ الملفات مع تأكيد الطلب. اسم ووصف ولون اختياري لكل مرفق.");Reuse(files,"نوع المرفق","OrderAttachmentTypeComboBox");var types=OrderControl<ComboBox>("OrderAttachmentTypeComboBox");types.Items.Clear();types.ItemsSource=new[]{"كِت لِست","مرجع المنتج","مرفق آخر"};types.SelectedIndex=0;
        Reuse(files,"اسم المرفق","OrderAttachmentNameTextBox");Reuse(files,"الوصف — اختياري","OrderAttachmentDescriptionTextBox");Reuse(files,"اللون — اختياري","OrderAttachmentColorTextBox");var addFile=new Button{Content="اختيار ملف وإضافته للطلب"};addFile.Click+=AddOrderAttachmentAsync;files.Children.Add(addFile);Reuse(files,"حتى 100 مرفق؛ 20 MB للملف و100 MB للطلب.","PendingOrderAttachmentsPanel");
        var materials=Card(editor,"٣. الخامات المطلوبة للطلب","اختر الصنف وحدد إجمالي كميته للطلب. الصرف الفعلي يتم من الإنتاج.");orderMaterialPicker=new("ابحث بكود الخامة أو اسمها");materials.Children.Add(orderMaterialPicker);orderMaterialQuantity=ExecInput("العدد / الكمية");Field(materials,"الكمية بوحدة الخامة",orderMaterialQuantity);var addMaterial=new Button{Content="إضافة / تحديث الخامة"};addMaterial.Click+=(_,_)=>AddWorkspaceItem(false);materials.Children.Add(addMaterial);orderMaterialDraft=new(){Spacing=6};materials.Children.Add(orderMaterialDraft);
        var accessories=Card(editor,"٤. الإكسسوارات المطلوبة");orderAccessoryPicker=new("ابحث بكود الإكسسوار أو اسمه");accessories.Children.Add(orderAccessoryPicker);Reuse(accessories,"العدد بوحدة الإكسسوار","OrderAccessoryQuantityTextBox");var addAccessory=new Button{Content="إضافة / تحديث الإكسسوار"};addAccessory.Click+=(_,_)=>AddWorkspaceItem(true);accessories.Children.Add(addAccessory);Reuse(accessories,"","PendingOrderAccessoriesPanel");
        var notes=Card(editor,"٥. الهالك والوصف والتأكيد");Reuse(notes,"نسبة الهالك حسب المهندس (%)","OrderWasteTextBox");Reuse(notes,"الوصف العام للطلب / المنتج","OrderNotesTextBox");Reuse(notes,"","CreateOrderButton");CreateOrderButton.Content="تأكيد الطلب";
        var details=Card(orders,"عرض الطلب المحدد","لا تظهر التفاصيل والمرفقات تلقائياً.");var show=new Button{Content="عرض التفاصيل"};show.Click+=async(_,_)=>{orderDetailsVisible=true;await LoadCompleteOrderDetailsAsync();};details.Children.Add(show);var hide=new Button{Content="إخفاء التفاصيل"};hide.Click+=(_,_)=>HideWorkspaceDetails();details.Children.Add(hide);Reuse(details,"","ConfirmedOrderDetailsPanel");OrderControl<StackPanel>("ConfirmedOrderDetailsPanel").IsVisible=false;
        var jobs=new StackPanel{Spacing=12};var linked=Card(jobs,"الطلب المرتبط بأوامر الشغل","يمكن إنشاء عدة أوامر شغل للطلب نفسه. لكل أمر كود واسم وملف PDF مستقل.");jobCustomerOrderPicker=new("ابحث بكود طلب العميل أو اسمه");linked.Children.Add(jobCustomerOrderPicker);jobCustomerOrderPicker.Changed+=()=>SelectWorkspaceOrderAsync(jobCustomerOrderPicker);jobParentSummary=ExecText("");linked.Children.Add(jobParentSummary);
        var create=Card(jobs,"إنشاء أمر شغل جديد","بيانات المنتج موروثة من الطلب المحدد، ولا تحتاج لإدخالها مرة أخرى.");Reuse(create,"كود أمر الشغل — فريد","ProductionOrderNumberTextBox");jobCreateName=ExecInput("مثال: تقطيع الدفعة الأولى");jobCreateDate=ExecInput("YYYY-MM-DD");jobCreateEndDate=ExecInput("YYYY-MM-DD — اختياري");jobCreateQuantity=ExecInput("الكمية المطلوبة");Field(create,"اسم أمر الشغل",jobCreateName);Field(create,"بداية التنفيذ",jobCreateDate);Field(create,"نهاية التنفيذ المخططة",jobCreateEndDate);Field(create,"كمية المنتجات",jobCreateQuantity);Reuse(create,"","CreateProductionOrderButton");CreateProductionOrderButton.Content="إنشاء أمر الشغل";
        var sheet=Card(jobs,"اختيار أمر شغل وتعبئة النموذج وتنزيل PDF","اختر أحد أوامر الشغل التابعة للطلب المحدد.");jobSearchPicker=new("كود أمر الشغل / اسم أمر الشغل");sheet.Children.Add(jobSearchPicker);jobSearchPicker.Changed+=()=>{var choices=(ProductionOrderComboBox.ItemsSource as IEnumerable<ProductionOrderChoice>)?.ToList()??[];ProductionOrderComboBox.SelectedIndex=choices.FindIndex(c=>c.Id==jobSearchPicker.Value);return Task.CompletedTask;};
        Reuse(sheet,"","ProductionWorkOrderSummaryTextBlock");
        var checklistHeader=new Grid{ColumnDefinitions=new ColumnDefinitions("95,105,4*,2*,38"),ColumnSpacing=6};var headings=new[]{"العدد / الكمية","الوحدة","المواصفات / المقاس / اللون","نوع الخامة / البند","اختر"};for(var i=0;i<headings.Length;i++){var text=ExecText(headings[i]);text.FontWeight=Avalonia.Media.FontWeight.SemiBold;Grid.SetColumn(text,i);checklistHeader.Children.Add(text);}sheet.Children.Add(checklistHeader);
        Reuse(sheet,"","ProductionWorkOrderLinesPanel");Reuse(sheet,"","SaveProductionWorkOrderButton");Reuse(sheet,"","DownloadProductionWorkOrderPdfButton");Reuse(sheet,"","ProductionWorkOrderMessageTextBlock");
        tabs.ItemsSource=new[]{new TabItem{Header="إنشاء وعرض الطلبات",Content=orders},new TabItem{Header="أوامر الشغل وملف PDF",Content=jobs}};
        var newMaterial=new Button{Content="إضافة صنف خامة غير موجود"};newMaterial.Click+=async(_,_)=>await AddOrderMasterItemAsync(false);materials.Children.Insert(1,newMaterial);
        var newAccessory=new Button{Content="إضافة صنف إكسسوار غير موجود"};newAccessory.Click+=async(_,_)=>await AddOrderMasterItemAsync(true);accessories.Children.Insert(1,newAccessory);
        InitializeOrderStockAvailability(notes);
    }
    private Task SelectWorkspaceOrderAsync(ProductionPicker picker){var choices=(SelectedOrderComboBox.ItemsSource as IEnumerable<CustomerOrderChoice>)?.ToList()??[];SelectedOrderComboBox.SelectedIndex=choices.FindIndex(c=>c.Id==picker.Value);return Task.CompletedTask;}
    private void SyncWorkspaceOrderPickers(){var id=(SelectedOrderComboBox.SelectedItem as CustomerOrderChoice)?.Id;var choices=currentOrders.Select(o=>new ExecChoice(o.Id,OrderLabel(o))).ToList();orderSearchPicker.Set(choices,id);jobCustomerOrderPicker.Set(choices,id);if(id is null){orderSearchPicker.Select(null);jobCustomerOrderPicker.Select(null);}}
    private void HideWorkspaceDetails(){orderDetailsVisible=false;++orderDetailsVersion;var p=OrderControl<StackPanel>("ConfirmedOrderDetailsPanel");p.Children.Clear();p.IsVisible=false;}
    private void RefreshOrderAccessoryChoices()
    {
        RefreshLegacyOrderAccessoryChoices();if(orderMaterialPicker is null)return;
        IEnumerable<ExecChoice> Choices(bool accessories)=>(currentMasterData?.Items??[]).Where(i=>accessories?i.ItemType=="accessory":i.Classification=="materials"&&(i.ItemType is "raw_material" or "consumable")).Select(i=>new ExecChoice(i.Id,$"{i.Code} — {i.NameAr} ({currentMasterData?.Units.FirstOrDefault(u=>u.Id==i.BaseUnitId)?.Code})"));
        orderMaterialPicker.Set(Choices(false));orderAccessoryPicker.Set(Choices(true));
    }
    private void AddWorkspaceItem(bool accessory)
    {
        try{var picker=accessory?orderAccessoryPicker:orderMaterialPicker;var box=accessory?OrderControl<TextBox>("OrderAccessoryQuantityTextBox"):orderMaterialQuantity;var id=Required(picker);var q=WorkspaceQuantity(box.Text);var item=currentMasterData!.Items.First(i=>i.Id==id);var unit=currentMasterData.Units.First(u=>u.Id==item.BaseUnitId);if(decimal.Round(q,Math.Min(unit.PrecisionDigits,4))!=q)throw new InvalidOperationException("الكمية لا توافق دقة وحدة الصنف.");var list=accessory?pendingOrderAccessories:pendingOrderMaterials;list.RemoveAll(l=>l.ItemId==id);list.Add(new(id,picker.Label,q));box.Text="";RenderOrderDraft();OrdersMessageTextBlock.Text="تم تحديث مسودة الطلب.";}catch(Exception ex){OrdersMessageTextBlock.Text=FriendlyError(ex);}
    }
    private static decimal WorkspaceQuantity(string? text)
    {
        if(!System.Text.RegularExpressions.Regex.IsMatch(text?.Trim()??"",@"^\d{1,9}(\.\d{1,4})?$")||!decimal.TryParse(text?.Trim(),NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var q)||q<=0||q>999999999)throw new InvalidOperationException("أدخل كمية أكبر من صفر حتى ٤ منازل عشرية. استخدم النقطة للكسور، دون فواصل آلاف.");return q;
    }
    private void RenderOrderDraft(){RenderLegacyOrderDraft();if(orderMaterialDraft is null)return;orderMaterialDraft.Children.Clear();foreach(var line in pendingOrderMaterials.ToList()){var row=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8};row.Children.Add(ExecText($"{line.Label} | الكمية: {line.Quantity}"));var remove=new Button{Content="إزالة"};remove.Click+=(_,_)=>{pendingOrderMaterials.Remove(line);RenderOrderDraft();};row.Children.Add(remove);orderMaterialDraft.Children.Add(row);}}
    private async Task ConfirmWorkspaceOrderAsync()
    {
        if(string.IsNullOrWhiteSpace(accessToken))return;var editor=OrderControl<StackPanel>("CompleteOrderEditor");try{
            if(OrderCustomerComboBox.SelectedItem is not Choice customer)throw new InvalidOperationException("اختر العميل.");
            var code=Nonempty(OrderNumberTextBox.Text,"اكتب كود الطلب.");var productCode=Nonempty(OrderControl<TextBox>("OrderProductCodeTextBox").Text,"اكتب كود المنتج.");var name=Nonempty(OrderControl<TextBox>("OrderProductNameTextBox").Text,"اكتب اسم الطلب / المنتج.");
            if(!decimal.TryParse(OrderControl<TextBox>("OrderWasteTextBox").Text,NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var waste)||waste<0||waste>100||decimal.Round(waste,2)!=waste)throw new InvalidOperationException("نسبة الهالك من صفر إلى 100 حتى منزلتين عشريتين. استخدم النقطة للكسور.");
            var date=ValidDate(OrderDateTextBox.Text);string? end=string.IsNullOrWhiteSpace(OrderDeliveryDateTextBox.Text)?null:ValidDate(OrderDeliveryDateTextBox.Text);if(end is not null&&string.CompareOrdinal(end,date)<0)throw new InvalidOperationException("التسليم لا يسبق بداية الطلب.");
            editor.IsEnabled=false;OrdersMessageTextBlock.Text="جارٍ حفظ الطلب ومكوناته…";using var content=new MultipartFormDataContent();content.Add(new StringContent(System.Text.Json.JsonSerializer.Serialize(new{orderNo=code,orderName=name,customerId=customer.Value,orderDate=date,requestedDeliveryDate=end,productCode,productName=name,wastePercentage=waste,notes=OrderNotesTextBox.Text?.Trim(),materials=pendingOrderMaterials.Select(l=>new{itemId=l.ItemId,quantity=l.Quantity}),accessories=pendingOrderAccessories.Select(l=>new{itemId=l.ItemId,quantity=l.Quantity}),attachments=pendingOrderAttachments.Select(f=>new{name=f.Name,description=f.Description,color=f.Color,attachmentType=f.AttachmentType})},JsonOptions)),"payload");
            foreach(var f in pendingOrderAttachments){var file=new ByteArrayContent(f.Content);file.Headers.ContentType=new(f.MimeType);content.Add(file,"files",f.FileName);}using var request=new HttpRequestMessage(HttpMethod.Post,"orders/confirm"){Content=content};request.Headers.Authorization=new("Bearer",accessToken);using var response=await HttpClient.SendAsync(request);await EnsureApiSuccessAsync(response);var created=await response.Content.ReadFromJsonAsync<ConfirmedOrderResult>(JsonOptions)??throw new InvalidOperationException("لم تصل هوية الطلب المحفوظ.");
            pendingOrderMaterials.Clear();pendingOrderAccessories.Clear();pendingOrderAttachments.Clear();RenderOrderDraft();foreach(var n in new[]{"OrderProductCodeTextBox","OrderProductNameTextBox","OrderWasteTextBox"})OrderControl<TextBox>(n).Text="";OrderNumberTextBox.Text=OrderNotesTextBox.Text=OrderDeliveryDateTextBox.Text="";await LoadOrdersAsync(created.Id);ShowOrderAvailability(created);OrdersMessageTextBlock.Text=created.Availability?.HasShortages==true?$"تم حفظ الطلب {created.OrderNo} مع وجود نواقص بالمخزون. راجع جدول النواقص أسفل زر التأكيد وفي المخزون.":$"تم حفظ الطلب {created.OrderNo}. نتيجة فحص المخزون ظاهرة أسفل التأكيد.";
        }catch(Exception ex){OrdersMessageTextBlock.Text=FriendlyError(ex);}finally{editor.IsEnabled=true;}
    }
    private async Task LoadWorkspaceOrderDetailsAsync()
    {
        var panel=OrderControl<StackPanel>("ConfirmedOrderDetailsPanel");panel.Children.Clear();panel.IsVisible=orderDetailsVisible;if(!orderDetailsVisible)return;var version=++orderDetailsVersion;
        if(SelectedOrderComboBox.SelectedItem is not CustomerOrderChoice selected){OrdersMessageTextBlock.Text="اختر طلباً من نتائج البحث أولاً.";return;}
        try{using var request=new HttpRequestMessage(HttpMethod.Get,$"orders/{selected.Id}/details");request.Headers.Authorization=new("Bearer",accessToken);using var response=await HttpClient.SendAsync(request);await EnsureApiSuccessAsync(response);var d=await response.Content.ReadFromJsonAsync<CompleteOrderDetail>(JsonOptions);if(d is null||version!=orderDetailsVersion||!orderDetailsVisible||(SelectedOrderComboBox.SelectedItem as CustomerOrderChoice)?.Id!=selected.Id)return;
            var identity=Card(panel,d.OrderNo+" — "+(d.OrderName??d.ProductName),$"العميل: {d.CustomerNameAr}\nالمنتج: {d.ProductCode} — {d.ProductName}\nالبداية: {d.OrderDate} | التسليم: {d.RequestedDeliveryDate??"غير محدد"}\nالهالك: {d.WastePercentage}%\n{d.Notes}");if(d.TotalBoards is >0)identity.Children.Add(ExecText($"إجمالي ألواح قديم غير مرتبط بصنف: {d.TotalBoards}."));
            var materials=Card(panel,"الخامات المطلوبة");foreach(var l in d.Materials??[])materials.Children.Add(ExecText($"{l.ItemCode} — {l.ItemNameAr} | {l.Quantity} {l.UnitCode}"));if(d.Materials?.Count is null or 0)materials.Children.Add(ExecText("لا توجد خامات مرتبطة بأصناف لهذا الطلب."));var accessories=Card(panel,"الإكسسوارات");foreach(var l in d.Accessories)accessories.Children.Add(ExecText($"{l.ItemCode} — {l.ItemNameAr} | {l.Quantity} {l.UnitCode}"));
            var jobs=Card(panel,"أوامر الشغل المرتبطة بهذا الطلب");foreach(var job in d.Jobs??[]){var open=new Button{Content=$"{job.OrderNo} — {job.JobName??job.OrderNo} | {job.Quantity} | {ExecutionStatus(job.Status)}"};open.Click+=async(_,_)=>{if((SelectedOrderComboBox.SelectedItem as CustomerOrderChoice)?.Id!=selected.Id)return;orderWorkspaceTabs.SelectedIndex=1;await LoadLinkedJobOrdersAsync(job.Id);};jobs.Children.Add(open);}if(d.Jobs?.Count is null or 0)jobs.Children.Add(ExecText("لم تنشأ أوامر شغل لهذا الطلب بعد."));
            var files=Card(panel,"المرفقات — اضغط على الملف لفتحه");foreach(var f in d.Attachments){var row=new StackPanel{Spacing=5};var open=new Button{Content=f.Name+" — "+f.FileName};open.Click+=async(_,_)=>{open.IsEnabled=false;try{await OpenWorkspaceAttachmentAsync(selected.Id,f);}catch(Exception ex){OrdersMessageTextBlock.Text=FriendlyError(ex);}finally{open.IsEnabled=true;}};row.Children.Add(open);if(!string.IsNullOrWhiteSpace(f.Description)||!string.IsNullOrWhiteSpace(f.Color))row.Children.Add(ExecText(f.Description+(string.IsNullOrWhiteSpace(f.Color)?"":" | اللون: "+f.Color)));files.Children.Add(row);}if(d.Attachments.Count==0)files.Children.Add(ExecText("لا توجد مرفقات لهذا الطلب."));
        }catch(Exception ex){if(version==orderDetailsVersion)OrdersMessageTextBlock.Text=FriendlyError(ex);}
    }
    private async Task OpenWorkspaceAttachmentAsync(string orderId,OrderAttachmentDetail file)
    {
        string[] allowed=[".jpg",".jpeg",".png",".webp",".gif",".pdf",".xlsx",".xls",".csv",".dxf",".dwg",".skp"];var name=Path.GetFileName(file.FileName);if(!allowed.Contains(Path.GetExtension(name).ToLowerInvariant()))throw new InvalidOperationException("نوع الملف غير مدعوم للفتح المباشر.");name=string.Concat(name.Select(c=>Path.GetInvalidFileNameChars().Contains(c)?'_':c));var folder=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WoodFactoryErp","order-attachments",Guid.Parse(orderId).ToString(),Guid.Parse(file.Id).ToString());Directory.CreateDirectory(folder);var path=Path.Combine(folder,name);
        // Reauthorize/read the immutable attachment, but do not overwrite a file
        // that may already be locked by its PDF/image viewer.
        var bytes=await ReadOrderAttachmentAsync(orderId,file.Id);
        if(!File.Exists(path)){var temporary=Path.Combine(folder,Guid.NewGuid().ToString("N")+".tmp");try{await File.WriteAllBytesAsync(temporary,bytes);try{File.Move(temporary,path);}catch(IOException)when(File.Exists(path)){} }finally{if(File.Exists(temporary))File.Delete(temporary);}}
        Process.Start(new ProcessStartInfo(path){UseShellExecute=true});
    }
    private sealed record OrderLinkedJob(string Id,string OrderNo,string? JobName,string Quantity,string Status,string? StartDate,string? EndDate);
}
