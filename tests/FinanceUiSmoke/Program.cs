using System.Collections;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using WoodFactoryErp.Desktop;

// Construct forms without restoring any real session, showing a window or using a server.
AppBuilder.Configure<Application>().UsePlatformDetect().SetupWithoutStarting();
var flags=BindingFlags.Instance|BindingFlags.NonPublic;
var window=(MainWindow)Activator.CreateInstance(typeof(MainWindow),flags,null,new object[]{false},null)!;
object Field(string n)=>typeof(MainWindow).GetField(n,flags)!.GetValue(window)!;
object? Invoke(string n,params object?[] args)=>typeof(MainWindow).GetMethod(n,flags)!.Invoke(window,args);
void Check(bool b,string m){if(!b)throw new Exception(m);}
Invoke("BuildFinance");
var root=(StackPanel)Field("financeRoot");var tabs=root.GetLogicalDescendants().OfType<TabControl>().Single();
var items=((IEnumerable<TabItem>)tabs.ItemsSource!).ToList();
Check(items.Count==3,"Finance defaults to guided selling, the monthly book and essential payments");
Check(Equals(items[1].Header,"دفتر الشهر"),"Monthly book remains directly accessible beside guided selling");
var all=root.GetLogicalDescendants().OfType<Control>().Concat(((IEnumerable<TabItem>)Field("allSalesFinanceTabs")).SelectMany(i=>((Control)i.Content!).GetLogicalDescendants().OfType<Control>())).Distinct().ToList();
Check(all.OfType<AutoCompleteBox>().Count()>=25,"Financial master/source selectors are searchable");
Check(all.OfType<AutoCompleteBox>().All(b=>b.FilterMode==AutoCompleteFilterMode.Contains),"All finance selectors search names/codes");
var save=all.OfType<Button>().First(b=>Equals(b.Content,"مراجعة وحفظ المستند"));save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Dispatcher.UIThread.RunJobs();
Check(all.OfType<TextBlock>().Any(t=>t.Text?.Contains("اكتب مرجعاً فريداً") == true),"Invalid financial action shows inline feedback before posting");
var form=((IEnumerable)Field("financeForms")).Cast<object>().First();
var monthlyRows=Enumerable.Range(0,121).Select(i=>new{key="finance:"+i,date="2026-01-15",domain="finance",kind="sale",reference="SALE-"+i,orderIds=new[]{"order-1"},orderNo=i<61?"SO-001":"SO-002",jobId="job-1",jobNo="JOB-1",title="Monthly activity",quantity="1",unit="PCS",amount="2700.0000",cash="0",detail=new{tax="378.0000"}}).ToList();
using var monthFixture=JsonDocument.Parse(JsonSerializer.Serialize(new{month="2026-01",snapshotAt="2026-11-18",summary=new{netSales="2700.0000",salesTax="378.0000",grossProfit="700.0000",manufacturingIncurred="200.0000",periodExpenses="150.0000",cashIn="1000.0000",cashOut="200.0000",closingTreasury="10800.0000",cogs="2000.0000",grossMargin="25.9259"},activities=monthlyRows,exceptions=Array.Empty<object>(),stockExceptions=Array.Empty<object>()}));
var monthBox=(TextBox)Field("monthlyMonth");monthBox.Text="2026-01";Dispatcher.UIThread.RunJobs();typeof(MainWindow).GetField("monthlyData",flags)!.SetValue(window,monthFixture.RootElement.Clone());Invoke("RenderMonthlySummary");Invoke("RenderMonthlyActivities");
var cards=(StackPanel)Field("monthlyCards");Check(cards.GetLogicalDescendants().OfType<TextBlock>().Any(t=>t.Text=="2,700.0000 ج.م"),"Monthly revenue shows the exact source total");
var table=(StackPanel)Field("monthlyTable");Check(table.GetLogicalDescendants().OfType<Button>().Count()==75,"Monthly activity renders a bounded page without dropping loaded rows");
((Button)Field("monthlyNext")).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Dispatcher.UIThread.RunJobs();Check(table.GetLogicalDescendants().OfType<Button>().Count()==46,"The next page exposes the rest of the month");
((TextBox)Field("monthlySearch")).Text="SO-001";Dispatcher.UIThread.RunJobs();Check(table.GetLogicalDescendants().OfType<Button>().Count()==61,"Monthly search covers rows beyond the visible page and filters by order code");Check(cards.GetLogicalDescendants().OfType<TextBlock>().Any(t=>t.Text=="2,700.0000 ج.م"),"Filtering rows never recalculates or changes month totals");
using var treeFixture=JsonDocument.Parse(JsonSerializer.Serialize(new{order=new{id="order-1",orderNo="SO-001",orderName="Order",customer="Customer",date="2026-01-10",status="open"},jobs=new[]{new{id="job-1",jobNo="JOB-1",name="Job",status="in_progress",quantity="2",directCost="300.0000",pooledCost="0.0000"}},requirements=new[]{new{code="MDF",name="Wood",quantity="4",unit="PCS"}},activities=monthlyRows.Take(1),attachments=Array.Empty<object>()}));
var tree=(StackPanel)Invoke("BuildMonthlyOrderTree",treeFixture.RootElement)!;Check(tree.GetLogicalDescendants().OfType<Expander>().Count()==5&&tree.GetLogicalDescendants().OfType<Expander>().All(e=>!e.IsExpanded),"Order details drill into collapsed requirements, jobs, finance, all-history and attachments");
monthBox.Text="2026-02";Dispatcher.UIThread.RunJobs();Check(cards.Children.Count==0&&table.Children.Count==0,"Changing month immediately clears the preceding month's figures");
Console.WriteLine("PASS: Monthly finance workspace, exact source totals, complete pagination/search, stable totals, order-tree drill-down and month isolation.");
var reference=(TextBox)form.GetType().GetField("Reference")!.GetValue(form)!;
var user=Guid.NewGuid().ToString();var token=Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{sub=user}))).TrimEnd('=').Replace('+','-').Replace('/','_')+".test";
typeof(MainWindow).GetField("accessToken",flags)!.SetValue(window,token);
var directory=Path.Combine(Path.GetTempPath(),"erp-finance-ui-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);
var outboxType=typeof(MainWindow).Assembly.GetType("WoodFactoryErp.Desktop.ProductionOutbox")!;
using var outbox=(IDisposable)Activator.CreateInstance(outboxType,Path.Combine(directory,"finance.sqlite"))!;
typeof(MainWindow).GetField("financeOutbox",flags)!.SetValue(window,outbox);
typeof(MainWindow).GetField("financeDraftUser",flags)!.SetValue(window,user);
reference.Text="AR-DRAFT-001";Invoke("SaveFinanceDrafts");Invoke("ClearFinanceSession");Check(reference.Text=="","Sign out clears the visible financial draft");Invoke("RestoreFinanceDrafts");Check(reference.Text=="AR-DRAFT-001","Draft survives SQLite save and restore for the same user");
Invoke("ClearFinanceSession");typeof(MainWindow).GetField("accessToken",flags)!.SetValue(window,Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new{sub=Guid.NewGuid().ToString()}))).TrimEnd('=').Replace('+','-').Replace('/','_')+".test");Invoke("RestoreFinanceDrafts");Check(reference.Text=="","Another account cannot restore the previous user's financial draft");
var pdfType=typeof(MainWindow).Assembly.GetType("WoodFactoryErp.Desktop.Documents.FinanceDocumentPdf")!;
var pdf=(byte[])pdfType.GetMethod("Generate")!.Invoke(null,new object[]{"فاتورة داخلية","TEST-1","1","2026-11-17","عميل","2700.0000","378.0000","0.0000","3078.0000","اختبار",new List<string[]>{new[]{"منتج","1","3000","300","378","3078"}}})!;
Check(Encoding.ASCII.GetString(pdf,0,4)=="%PDF"&&pdf.Length>1000,"Arabic internal document renders as a real PDF");
Console.WriteLine("PASS: Finance form construction, searchable tables/selectors, inline validation, durable account-scoped drafts, and Arabic PDF generation.");

using var guidedFixture=JsonDocument.Parse("""
{"capabilities":["guided_sales_v1"],"orders":[{"id":"order","order_no":"SO-1","customer_id":"customer","product_name":"Product"}],"stock":[{"item_id":"fg","warehouse_id":"wh","quantity":"10.0000","unit_cost":"2000.0000","global_quantity":"10.0000","inventory_value":"20000.0000"}],"policies":[{"id":"tax","code":"TEST","rate":"14","effective_from":"2026-01-01"}],"documents":[],"readyJobs":[],"accounts":[],"savedPrices":[]}
""");
var masterType=typeof(MainWindow).GetNestedType("MasterDataResponse",BindingFlags.NonPublic)!;
var master=JsonSerializer.Deserialize("""
{"units":[{"id":"unit","code":"PCS","nameAr":"Unit","nameEn":"Unit","precisionDigits":0}],"warehouses":[{"id":"wh","code":"FG","nameAr":"Finished","nameEn":"Finished","warehouseType":"finished_goods"}],"parties":[],"items":[{"id":"fg","code":"FG-1","nameAr":"Product","itemType":"finished_good","baseUnitId":"unit","stockTracked":true,"classification":"materials"}]}
""",masterType,new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
typeof(MainWindow).GetField("currentMasterData",flags)!.SetValue(window,master);typeof(MainWindow).GetField("financeData",flags)!.SetValue(window,guidedFixture.RootElement.Clone());((TextBox)Field("sellingDate")).Text="2026-01-15";Invoke("RefreshGuidedSalesContext");
void SelectSelling(string name,string id)=>Field(name).GetType().GetMethod("Select")!.Invoke(Field(name),new object[]{id});
SelectSelling("sellingOrder","order");SelectSelling("sellingItem","fg");SelectSelling("sellingWarehouse","wh");SelectSelling("sellingTax","tax");((TextBox)Field("sellingQuantity")).Text="1";((TextBox)Field("sellingPrice")).Text="3000";((TextBox)Field("sellingDiscount")).Text="300";((TextBox)Field("sellingDelivery")).Text="0";Dispatcher.UIThread.RunJobs();Invoke("RefreshSellingPreview");
var preview=((TextBlock)Field("sellingPreview")).Text!;Check(new[]{"2700","378","3078","2000","700"}.All(preview.Contains),"Guided price preview separates sale, tax, customer total, product cost and gross profit: "+preview);
Check(((TextBlock)Field("sellingReadiness")).Text!.Contains("لم يُسجل بيع"),"A zero revenue total explains that a price/cost entry is not a posted sale");
((CheckBox)Field("sellingAdvanced")).IsChecked=true;Dispatcher.UIThread.RunJobs();Check(((IEnumerable<TabItem>)tabs.ItemsSource!).Count()>3,"Advanced procedures remain available without cluttering the normal workflow");
((CheckBox)Field("sellingAdvanced")).IsChecked=false;Dispatcher.UIThread.RunJobs();Check(((IEnumerable<TabItem>)tabs.ItemsSource!).Count()==3,"Simple finance navigation can be restored");
Console.WriteLine("PASS: Simplified selling flow, exact cost/price/tax/profit preview, zero-sales explanation and optional advanced procedures.");
