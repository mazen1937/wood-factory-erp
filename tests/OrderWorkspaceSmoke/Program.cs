using System.Collections;
using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.LogicalTree;
using WoodFactoryErp.Desktop;

// No real session, server connection, file viewer or visible window.
AppBuilder.Configure<Application>().UsePlatformDetect().SetupWithoutStarting();
var flags=BindingFlags.NonPublic|BindingFlags.Instance;
var window=(MainWindow)Activator.CreateInstance(typeof(MainWindow),flags,null,new object[]{false},null)!;
object Field(string n)=>typeof(MainWindow).GetField(n,flags)!.GetValue(window)!;
object? Call(string n,params object?[] args)=>typeof(MainWindow).GetMethod(n,flags)!.Invoke(window,args);
void Check(bool condition,string text){if(!condition)throw new Exception(text);}
var panel=(ScrollViewer)Field("OrdersPanel");var root=(StackPanel)panel.Content!;
var tabs=root.Children.OfType<TabControl>().Single();var items=((IEnumerable<TabItem>)tabs.ItemsSource!).ToList();
Check(items.Count==2,"Only order creation/view and linked job sheets are visible");
var jobs=(StackPanel)items[1].Content!;var jobControls=jobs.GetLogicalDescendants().OfType<Control>().ToList();
Check(!jobControls.Contains((Control)Field("ProductionProductCodeTextBox"))&&!jobControls.Contains((Control)Field("ProductionProductNameTextBox")),"Create-job form does not ask for inherited product identity");
Check(jobControls.OfType<Border>().Count()>=3,"Job sections use distinct cards");
var details=window.FindControl<StackPanel>("ConfirmedOrderDetailsPanel")!;Check(!details.IsVisible&&details.Children.Count==0,"Order details start hidden");
await (Task)Call("LoadCompleteOrderDetailsAsync")!;Check(details.Children.Count==0,"Refreshing a closed details panel never fetches or displays attachments");
var orderType=typeof(MainWindow).GetNestedType("CustomerOrderView",BindingFlags.NonPublic)!;
var listType=typeof(List<>).MakeGenericType(orderType);var opts=new JsonSerializerOptions(JsonSerializerDefaults.Web);
var id=Guid.NewGuid().ToString();var orders=JsonSerializer.Deserialize(JsonSerializer.Serialize(new[]{new{id,orderNo="SO-NEW",orderName="غرفة المعيشة",productCode="P-1",productName="منتج الأب",customerNameAr="عميل",orderDate="2026-11-17",requestedDeliveryDate="2026-11-25",status="open"}}),listType,opts)!;
typeof(MainWindow).GetField("currentOrders",flags)!.SetValue(window,orders);
var choiceType=typeof(MainWindow).GetNestedType("CustomerOrderChoice",BindingFlags.NonPublic)!;var choice=Activator.CreateInstance(choiceType,id,"SO-NEW")!;
var backing=(ComboBox)Field("SelectedOrderComboBox");backing.ItemsSource=new[]{choice};backing.SelectedIndex=0;
Call("SyncWorkspaceOrderPickers");Call("PrepareJobHeaderForSelectedOrder");
var picker=(Control)Field("orderSearchPicker");var box=picker.GetLogicalDescendants().OfType<AutoCompleteBox>().Single();
Check(box.FilterMode==AutoCompleteFilterMode.Contains&&box.Text!.Contains("SO-NEW")&&box.Text.Contains("غرفة المعيشة"),"Order search contains both code and order name");
Check(((TextBox)Field("jobCreateDate")).Text=="2026-11-17"&&((TextBox)Field("jobCreateEndDate")).Text=="2026-11-25","New job inherits the parent start/delivery dates");
Check(!ReferenceEquals(Field("jobCreateDate"),Field("ProductionDateTextBox")),"Loading an existing PDF cannot overwrite new-job schedule inputs");
var masterType=typeof(MainWindow).GetNestedType("MasterDataResponse",BindingFlags.NonPublic)!;
var master=JsonSerializer.Deserialize("""
{"units":[{"id":"unit","code":"PCS","nameAr":"وحدة","nameEn":"Unit","precisionDigits":0}],"warehouses":[],"parties":[],"items":[{"id":"raw","code":"MDF-X","nameAr":"خامة","itemType":"raw_material","baseUnitId":"unit","stockTracked":true,"classification":"materials"},{"id":"acc","code":"ACC-X","nameAr":"مفصلة","itemType":"accessory","baseUnitId":"unit","stockTracked":true,"classification":"materials"},{"id":"fg","code":"FG-X","nameAr":"منتج","itemType":"finished_good","baseUnitId":"unit","stockTracked":true,"classification":"materials"}]}
""",masterType,opts)!;
typeof(MainWindow).GetField("currentMasterData",flags)!.SetValue(window,master);Call("RefreshOrderAccessoryChoices");
var material=(Control)Field("orderMaterialPicker");var materialBox=material.GetLogicalDescendants().OfType<AutoCompleteBox>().Single();var rawChoices=((IEnumerable)materialBox.ItemsSource!).Cast<object>().ToList();
Check(rawChoices.Count==1&&rawChoices[0].ToString()!.Contains("MDF-X"),"Material picker excludes accessories and finished goods");
material.GetType().GetMethod("Select")!.Invoke(material,new object[]{"raw"});((TextBox)Field("orderMaterialQuantity")).Text="4";Call("AddWorkspaceItem",false);
Check(((IList)Field("pendingOrderMaterials")).Count==1,"A selected material is saved as a quantity line");
((TextBox)Field("orderMaterialQuantity")).Text="6";Call("AddWorkspaceItem",false);Check(((IList)Field("pendingOrderMaterials")).Count==1,"Updating a material does not create duplicate rows");
((TextBox)Field("orderMaterialQuantity")).Text="0.5";Call("AddWorkspaceItem",false);Check(((TextBlock)Field("OrdersMessageTextBlock")).Text!.Contains("دقة وحدة"),"Fractional quantities are checked against the unit");
((TextBox)Field("orderMaterialQuantity")).Text="1,5";Call("AddWorkspaceItem",false);Check(((TextBlock)Field("OrdersMessageTextBlock")).Text!.Contains("فواصل آلاف"),"A decimal comma cannot silently become a larger whole quantity");
Console.WriteLine("PASS: Two card-based tabs, searchable parent identity, hidden details, inherited schedule, independent job editor and item-based material quantities.");
