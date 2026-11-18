using System.Globalization;
using System.Net.Http.Json;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Threading;

namespace WoodFactoryErp.Desktop;

public partial class MainWindow
{
    private TextBox inventorySearch=null!;
    private TextBlock inventorySearchStatus=null!;
    private StackPanel inventorySearchCatalog=null!;
    private List<InventoryBalance> inventorySearchBalances=[];
    private List<InventoryMovement> inventorySearchMovements=[];
    private OrderShortageReport? inventorySearchReport;
    private readonly HashSet<string> expandedInventoryOrders=[];
    private DispatcherTimer? inventorySearchTimer;
    private int inventoryMovementSearchVersion;

    private void InitializeInventorySearch(StackPanel root)
    {
        var row=new Grid{ColumnDefinitions=new ColumnDefinitions("*,Auto"),ColumnSpacing=8,MaxWidth=620,HorizontalAlignment=HorizontalAlignment.Right};
        inventorySearch=new TextBox{Name="InventorySearchBox",PlaceholderText="بحث المخزن: صنف، خامة، إكسسوار، طلب، مخزن، مورد أو مرجع…",MaxLength=200,MinWidth=230};
        var clear=new Button{Content="مسح",VerticalAlignment=VerticalAlignment.Center};Grid.SetColumn(clear,1);clear.Click+=(_,_)=>inventorySearch.Text="";
        row.Children.Add(inventorySearch);row.Children.Add(clear);root.Children.Insert(1,row);
        inventorySearchStatus=ExecText("");root.Children.Insert(2,inventorySearchStatus);
        inventorySearchCatalog=new StackPanel{Spacing=8,IsVisible=false};root.Children.Insert(3,inventorySearchCatalog);
        inventorySearchTimer=new DispatcherTimer{Interval=TimeSpan.FromMilliseconds(300)};
        inventorySearchTimer.Tick+=async(_,_)=>{inventorySearchTimer.Stop();await SearchInventoryMovementsAsync();};
        inventorySearch.TextChanged+=(_,_)=>{++inventoryMovementSearchVersion;inventorySearchMovements=[];RenderInventorySearch();if(inventorySearchReport is not null)RenderInventoryShortages(inventorySearchReport);inventorySearchTimer.Stop();inventorySearchTimer.Start();};
        Closed+=(_,_)=>inventorySearchTimer.Stop();
    }

    private bool InventoryMatch(params object?[] values)
    {
        var terms=(inventorySearch?.Text??"").Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
        var text=string.Join(" ",values.Select(v=>Convert.ToString(v,CultureInfo.InvariantCulture)));
        return terms.All(t=>text.Contains(t,StringComparison.OrdinalIgnoreCase));
    }
    private string InventoryItemTerms(string itemId)
    {
        var item=currentMasterData?.Items.FirstOrDefault(i=>i.Id==itemId);if(item is null)return "";
        var unit=currentMasterData?.Units.FirstOrDefault(u=>u.Id==item.BaseUnitId);
        var type=item.ItemType switch{"raw_material"=>"خامة خامات","accessory"=>"إكسسوار اكسسوار إكسسوارات اكسسوارات","consumable"=>"مستهلك مستهلكات",_=>"منتج تام"};
        return $"{item.Id} {item.Code} {item.NameAr} {item.NameEn} {item.ItemType} {type} {unit?.Code} {unit?.NameAr} {unit?.NameEn}";
    }
    private bool InventoryRequirementMatch(OrderStockItem i)=>InventoryMatch(i.OrderId,i.OrderNo,i.OrderName,i.CustomerName,i.OrderDate,i.ItemCode,i.ItemName,i.ItemId,i.ItemType,i.UnitCode,i.RequiredQuantity,i.RemainingQuantity,i.ShortageQuantity,InventoryItemTerms(i.ItemId));

    private void RenderInventorySearch()
    {
        if(inventorySearch is null)return;
        var balances=inventorySearchBalances.Where(x=>InventoryMatch(x.ItemId,x.ItemCode,x.ItemNameAr,x.WarehouseId,x.WarehouseCode,x.WarehouseNameAr,x.UnitCode,x.QuantityOnHand,x.AverageUnitCost,x.InventoryValue,InventoryItemTerms(x.ItemId))).ToList();
        InventoryBalancesTextBlock.Text=balances.Count==0?"لا توجد أرصدة مطابقة.":string.Join(Environment.NewLine,balances.Select(x=>$"{x.ItemCode} — {x.ItemNameAr} | المخزن: {x.WarehouseCode} | الكمية: {x.QuantityOnHand:N4} {x.UnitCode} | متوسط تكلفة الوحدة: {x.AverageUnitCost:N4} ج.م | قيمة مخزون الصنف: {x.InventoryValue:N4} ج.م"));
        // Movement results are filtered by the server across the complete ledger,
        // before its newest-250 cap, rather than only searching the loaded page.
        InventoryMovementsTextBlock.Text=inventorySearchMovements.Count==0?"لا توجد حركات مطابقة.":string.Join(Environment.NewLine,inventorySearchMovements.Select(x=>$"{x.MovementDate:yyyy-MM-dd} | {InventoryMovementLabel(x.MovementType)} | {x.DocumentReference} | {x.ItemCode} — {x.ItemNameAr} | {x.WarehouseCode} | {x.SupplierCode} {x.SupplierNameAr} | كمية: {x.Quantity:N4} | تكلفة الوحدة: {x.UnitCost:N4} | الإجمالي: {x.TotalCost:N4} ج.م | {x.Notes}"));
        inventorySearchCatalog.Children.Clear();inventorySearchCatalog.IsVisible=!string.IsNullOrWhiteSpace(inventorySearch.Text);
        var catalog=(currentMasterData?.Items??[]).Where(i=>i.StockTracked&&i.Classification=="materials"&&InventoryMatch(InventoryItemTerms(i.Id))).ToList();
        if(inventorySearchCatalog.IsVisible){inventorySearchCatalog.Children.Add(ExecText($"الأصناف المطابقة: {catalog.Count} (تشمل الأصناف التي لم يُسجّل لها رصيد)",16));var table=new StackPanel();inventorySearchCatalog.Children.Add(table);FinanceTable(table,["الكود","الصنف","English","النوع"],catalog.Select(i=>new[]{i.Code,i.NameAr,i.NameEn??"",ItemTypeLabel(i.ItemType)}));}
        inventorySearchStatus.Text=$"الأرصدة المطابقة: {balances.Count} | الحركات: أحدث {inventorySearchMovements.Count} نتيجة (حتى 250) | البحث يشمل احتياجات الطلبات";
    }

    private async Task SearchInventoryMovementsAsync()
    {
        if(string.IsNullOrWhiteSpace(accessToken))return;
        var version=++inventoryMovementSearchVersion;var query=inventorySearch.Text??"";var token=accessToken;
        inventorySearchStatus.Text="جارٍ البحث في سجل الحركات…";
        try{
            using var request=new HttpRequestMessage(HttpMethod.Get,"inventory/movements?search="+Uri.EscapeDataString(query));request.Headers.Authorization=new("Bearer",token);
            using var response=await HttpClient.SendAsync(request);await EnsureApiSuccessAsync(response);
            var rows=await response.Content.ReadFromJsonAsync<List<InventoryMovement>>(JsonOptions)??[];
            if(version!=inventoryMovementSearchVersion||query!=inventorySearch.Text||token!=accessToken)return;
            inventorySearchMovements=rows;RenderInventorySearch();
        }catch(Exception ex){if(version==inventoryMovementSearchVersion&&query==inventorySearch.Text&&token==accessToken)inventorySearchStatus.Text="تعذر تحديث نتائج الحركات: "+FriendlyError(ex);}
    }

    private StackPanel InventoryOrderDetails(string key,string title,string subtitle)
    {
        var shell=Card(inventoryShortagesPanel,title);var details=new StackPanel{Spacing=8,IsVisible=expandedInventoryOrders.Contains(key)};
        var toggle=new Button{Content=details.IsVisible?"إخفاء التفاصيل":"عرض التفاصيل",HorizontalAlignment=HorizontalAlignment.Right};
        toggle.Click+=(_,_)=>{details.IsVisible=!details.IsVisible;if(details.IsVisible)expandedInventoryOrders.Add(key);else expandedInventoryOrders.Remove(key);toggle.Content=details.IsVisible?"إخفاء التفاصيل":"عرض التفاصيل";};
        shell.Children.Add(toggle);shell.Children.Add(details);if(!string.IsNullOrEmpty(subtitle))details.Children.Add(ExecText(subtitle));return details;
    }
}
