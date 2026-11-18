using System.Net.Http.Json;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;

namespace WoodFactoryErp.Desktop;

public partial class MainWindow
{
    private StackPanel orderAvailabilityPanel=null!,inventoryShortagesPanel=null!;
    private TextBlock inventoryShortagesMessage=null!;
    private DispatcherTimer? shortagesTimer;
    private int shortagesLoadVersion;
    private void InitializeOrderStockAvailability(StackPanel confirmationCard)
    {
        orderAvailabilityPanel=Card(confirmationCard,"نتيجة فحص المخزون للطلب");orderAvailabilityPanel.IsVisible=false;
        var host=new StackPanel{Spacing=8};var root=(StackPanel)InventoryPanel.Content!;root.Children.Insert(Math.Min(3,root.Children.Count),host);InitializeInventorySearch(root);
        inventoryShortagesPanel=Card(host,"نواقص احتياجات الطلبات — مطلوب توفيرها","النقص محسوب للخامات والإكسسوارات حسب الطلبات المفتوحة، بأولوية تاريخ الطلب ثم وقت إنشائه. الكميات بوحدة الصنف. المتاح تخطيطي حتى اعتماد الصرف الفعلي.");
        inventoryShortagesMessage=ExecText("افتح المخزون أو اضغط تحديث لعرض النواقص الحالية.");host.Children.Add(inventoryShortagesMessage);
        FinanceButton(host,"تحديث نواقص الطلبات",async m=>{await LoadOrderShortagesAsync();m.Text="راجع نتيجة التحديث أعلاه.";});
        shortagesTimer=new DispatcherTimer{Interval=TimeSpan.FromSeconds(10)};shortagesTimer.Tick+=async(_,_)=>{if(InventoryPanel.IsVisible&&!string.IsNullOrWhiteSpace(accessToken))await LoadOrderShortagesAsync();};shortagesTimer.Start();Closed+=(_,_)=>shortagesTimer?.Stop();
    }
    private void ShowOrderAvailability(ConfirmedOrderResult order)
    {
        orderAvailabilityPanel.IsVisible=true;orderAvailabilityPanel.Children.Clear();
        orderAvailabilityPanel.Children.Add(ExecText($"الطلب {order.OrderNo} — تم الحفظ وفحص احتياجات المخزون",18));
        var data=order.Availability;if(data is null){orderAvailabilityPanel.Children.Add(ExecText("تعذر استلام نتيجة الفحص. راجع نواقص الطلبات من المخزون."));return;}
        var shortages=data.Items.Where(i=>i.Insufficient).ToList();
        if(shortages.Count>0){var warning=ExecText("المخزون غير كافٍ لهذا الطلب. مطلوب توفير الأصناف والكميات التالية:",17);warning.Foreground=Avalonia.Media.Brushes.IndianRed;orderAvailabilityPanel.Children.Add(warning);RenderShortageTable(orderAvailabilityPanel,shortages);}
        else orderAvailabilityPanel.Children.Add(ExecText(data.Items.Count==0?"لم تسجل احتياجات أصناف لفحصها.":data.UncheckedItems>0?"لا يوجد نقص في الأصناف المتابعة بالمخزون؛ توجد أصناف غير مخزنية لا يمكن تأكيد توافرها.":"المتاح يغطي الاحتياجات المتبقية حسب وضع المخزون والطلبات وقت الفحص."));
        if(data.UncheckedItems>0)orderAvailabilityPanel.Children.Add(ExecText($"أصناف غير متابعة مخزنياً: {data.UncheckedItems}. راجع بياناتها قبل الاعتماد على الفحص."));
        orderAvailabilityPanel.Children.Add(ExecText("تظهر هذه الاحتياجات أيضاً في قسم المخزون، وتتحدث عند الاستلام أو الصرف أو الرد. الفحص لا يحجز الكمية ولا يغير رصيد المخزن."));
    }
    private async Task LoadOrderShortagesAsync()
    {
        if(inventoryShortagesPanel is null||string.IsNullOrWhiteSpace(accessToken))return;var version=++shortagesLoadVersion;var token=accessToken;
        try{using var request=new HttpRequestMessage(HttpMethod.Get,"inventory/order-shortages");request.Headers.Authorization=new("Bearer",token);using var response=await HttpClient.SendAsync(request);await EnsureApiSuccessAsync(response);var report=await response.Content.ReadFromJsonAsync<OrderShortageReport>(JsonOptions)??throw new InvalidOperationException("بيانات فحص النواقص فارغة.");if(version!=shortagesLoadVersion||token!=accessToken)return;
            RenderInventoryShortages(report);inventoryShortagesMessage.Text="آخر تحديث للنواقص: "+DateTime.Now.ToString("HH:mm:ss");
        }catch(Exception ex){if(version==shortagesLoadVersion)inventoryShortagesMessage.Text="تعذر تحديث النواقص؛ أي بيانات معروضة قديمة: "+FriendlyError(ex);}
    }
    private void RenderInventoryShortages(OrderShortageReport report)
    {
        inventorySearchReport=report;
        inventoryShortagesPanel.Children.Clear();inventoryShortagesPanel.Children.Add(ExecText("نقص احتياجات الطلبات — المطلوب توفيره",21));
        inventoryShortagesPanel.Children.Add(ExecText("الأولوية حسب تاريخ الطلب ثم وقت إنشائه. التخطيط للشراء؛ حجز المخزون الفعلي يتم عند الصرف. اعرض تفاصيل أي طلب عند الحاجة."));
        var missing=report.Items.Where(i=>i.Insufficient&&InventoryRequirementMatch(i)).ToList();
        if(missing.Count==0)inventoryShortagesPanel.Children.Add(ExecText(string.IsNullOrWhiteSpace(inventorySearch.Text)?"لا يوجد نقص تخطيطي في احتياجات الأصناف المخزنية للطلبات المفتوحة.":"لا توجد احتياجات ناقصة مطابقة للبحث."));
        else{
            var totals=Card(inventoryShortagesPanel,"إجمالي الكميات المطلوب توفيرها لكل صنف");
            var table=new StackPanel();totals.Children.Add(table);
            FinanceTable(table,["كود الصنف","الصنف","إجمالي النقص","الوحدة"],missing.GroupBy(i=>i.ItemId).Select(g=>new[]{g.First().ItemCode,g.First().ItemName,g.Sum(i=>Number(i.ShortageQuantity)).ToString("0.####",System.Globalization.CultureInfo.InvariantCulture),g.First().UnitCode}));
            foreach(var group in missing.GroupBy(i=>i.OrderId)){var first=group.First();var detail=InventoryOrderDetails(first.OrderId,$"طلب {first.OrderNo} — {first.OrderName}",$"العميل: {first.CustomerName} | تاريخ الطلب: {first.OrderDate}\nمعرّف الطلب: {first.OrderId}");RenderShortageTable(detail,group);}
        }
        var uncheckedItems=report.Items.Where(i=>!i.Checkable&&Number(i.RemainingQuantity)>0&&InventoryRequirementMatch(i)).ToList();
        foreach(var group in uncheckedItems.GroupBy(i=>i.OrderId)){var first=group.First();var detail=InventoryOrderDetails("unchecked:"+first.OrderId,$"{first.OrderNo} — أصناف تحتاج مراجعة مخزنية","هذه الأصناف غير متتبعة بالمخزون أو ليست مواد مخزنية. لا تُعتبر متاحة تلقائياً.");foreach(var i in group)detail.Children.Add(ExecText($"{i.ItemCode} — {i.ItemName} | الكمية المتبقية: {i.RemainingQuantity} {i.UnitCode}"));}
        foreach(var o in report.LegacyOrders.Where(o=>InventoryMatch(o.OrderId,o.OrderNo,o.OrderName,o.LegacyBoards))){var detail=InventoryOrderDetails("legacy:"+o.OrderId,$"{o.OrderNo} — {o.OrderName} (طلب قديم)","الألواح القديمة غير مرتبطة بأصناف محددة؛ يجب تحديد الصنف قبل حساب النقص.");detail.Children.Add(ExecText($"معرّف الطلب: {o.OrderId} | عدد ألواح قديم: {o.LegacyBoards}"));}
    }
    private static void RenderShortageTable(StackPanel parent,IEnumerable<OrderStockItem> rows)
    {
        var table=new StackPanel();parent.Children.Add(table);FinanceTable(table,["كود الصنف","الصنف","المطلوب للطلب","مصروف صالح للاستخدام","المتبقي","المخزون الفعلي","متاح لهذا الطلب","مطلوب توفيره","الوحدة"],rows.Select(i=>new[]{i.ItemCode,i.ItemName,i.RequiredQuantity,i.UsableIssuedQuantity,i.RemainingQuantity,i.OnHandQuantity,i.AvailableQuantity??"—",i.ShortageQuantity??"—",i.UnitCode}));
    }
    private async Task AddOrderMasterItemAsync(bool accessory)
    {
        try{var dialog=BuildOrderItemDialog(accessory);var created=await dialog.ShowDialog<MasterDataItem?>(this);if(created is null)return;
            // Keep the acknowledged item usable even if a subsequent list refresh fails.
            if(currentMasterData is not null&&!currentMasterData.Items.Any(i=>i.Id==created.Id))currentMasterData.Items.Add(created);
            await LoadMasterDataAsync();RefreshOrderAccessoryChoices();(accessory?orderAccessoryPicker:orderMaterialPicker).Select(created.Id);
            OrdersMessageTextBlock.Text=$"تم حفظ الصنف {created.Code} بالبيانات الأساسية واختياره للطلب. أدخل الكمية ثم أضفه للطلب.";
        }catch(Exception ex){OrdersMessageTextBlock.Text=FriendlyError(ex);}
    }
    private Window BuildOrderItemDialog(bool accessory)
    {
        var picker=accessory?orderAccessoryPicker:orderMaterialPicker;var query=picker.Value is null?picker.Label.Trim():"";
        var dialog=new Window{Title=accessory?"إضافة صنف إكسسوار":"إضافة صنف خامة",Width=540,Height=720,MinWidth=420,MinHeight=500,WindowStartupLocation=WindowStartupLocation.CenterOwner,ShowInTaskbar=false};
        var root=new StackPanel{Spacing=9,Margin=new Avalonia.Thickness(18)};dialog.Content=new ScrollViewer{Content=root};root.Children.Add(ExecText(dialog.Title,23));root.Children.Add(ExecText("يحفظ الصنف في البيانات الأساسية. تظل مسودة الطلب مفتوحة دون فقد بياناتها."));
        var code=ExecInput("كود الصنف");code.MaxLength=40;if(!query.Any(char.IsWhiteSpace)&&query.Length<=40)code.Text=query;
        var arabic=ExecInput("الاسم بالعربية");arabic.MaxLength=160;arabic.Text=query;var english=ExecInput("الاسم بالإنجليزية — اختياري");english.MaxLength=160;
        var type=new ComboBox{ItemsSource=accessory?new[]{new ExecChoice("accessory","إكسسوار")}:new[]{new ExecChoice("raw_material","خامة"),new ExecChoice("consumable","مستهلك")},SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch};
        var unitsPicker=new ProductionPicker("ابحث عن وحدة القياس");unitsPicker.Set((currentMasterData?.Units??[]).Select(u=>new ExecChoice(u.Id,u.Code+" — "+u.NameAr)),firstIfSingle:true);
        var classification=new ComboBox{ItemsSource=new[]{new ExecChoice("materials","خامات"),new ExecChoice("industrial_expense","مصاريف صناعية")},SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch};
        var tracked=new CheckBox{Content="متابعة الكمية بالمخزون",IsChecked=true};
        Field(root,"كود الصنف",code);Field(root,"الاسم بالعربية",arabic);Field(root,"الاسم بالإنجليزية",english);Field(root,"نوع الصنف",type);Field(root,"وحدة القياس",unitsPicker);Field(root,"التصنيف",classification);root.Children.Add(tracked);
        root.Children.Add(ExecText("إضافة الصنف لاحتياجات هذا الطلب تتطلب تصنيف خامات. الصنف الجديد لا يملك رصيداً حتى تسجيل استلام فعلي بالمخزون."));
        var save=new Button{Content="حفظ الصنف",HorizontalAlignment=HorizontalAlignment.Right};root.Children.Add(save);var feedback=ExecText("");root.Children.Add(feedback);var cancel=new Button{Content="إلغاء والعودة للطلب",HorizontalAlignment=HorizontalAlignment.Right};cancel.Click+=(_,_)=>dialog.Close();root.Children.Add(cancel);
        save.Click+=async(_,_)=>{
            try{
                var itemCode=Nonempty(code.Text,"اكتب كود الصنف.");var nameAr=Nonempty(arabic.Text,"اكتب اسم الصنف بالعربية.");var unit=Required(unitsPicker);var itemType=EnumValue(type);if(EnumValue(classification)!="materials")throw new InvalidOperationException("اختر تصنيف خامات حتى يمكن استخدام الصنف في احتياجات الطلب.");
                save.IsEnabled=cancel.IsEnabled=false;feedback.Text="جارٍ حفظ الصنف…";
                using var request=new HttpRequestMessage(HttpMethod.Post,"master-data/items"){Content=JsonContent.Create(new{code=itemCode,nameAr,nameEn=english.Text?.Trim(),itemType,baseUnitId=unit,stockTracked=tracked.IsChecked==true,classification="materials"},options:JsonOptions)};request.Headers.Authorization=new("Bearer",accessToken);using var response=await HttpClient.SendAsync(request);await EnsureApiSuccessAsync(response);var created=await response.Content.ReadFromJsonAsync<MasterDataItem>(JsonOptions)??throw new InvalidOperationException("لم تصل بيانات الصنف المحفوظ.");dialog.Close(created);
            }catch(Exception ex){Notice(feedback,FriendlyError(ex),false);}finally{save.IsEnabled=cancel.IsEnabled=true;}
        };
        return dialog;
    }
    private sealed record OrderStockItem(string OrderId,string OrderNo,string OrderName,string OrderDate,string CustomerName,string ItemId,string ItemCode,string ItemName,string ItemType,string UnitCode,bool Checkable,string RequiredQuantity,string UsableIssuedQuantity,string RemainingQuantity,string OnHandQuantity,string? AvailableQuantity,string? ShortageQuantity,bool Insufficient);
    private sealed record OrderStockAvailability(List<OrderStockItem> Items,bool HasShortages,int UncheckedItems);
    private sealed record LegacyOrderStock(string OrderId,string OrderNo,string OrderName,int LegacyBoards);
    private sealed record OrderShortageReport(List<OrderStockItem> Items,List<LegacyOrderStock> LegacyOrders);
}
