using System.Reflection;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Threading;
using WoodFactoryErp.Desktop;

// No window is shown, no saved session is restored, and no server request is sent.
AppBuilder.Configure<Application>().UsePlatformDetect().SetupWithoutStarting();
var flags=BindingFlags.Instance|BindingFlags.NonPublic;
var window=(MainWindow)Activator.CreateInstance(typeof(MainWindow),flags,null,new object[]{false},null)!;
object Field(string name)=>typeof(MainWindow).GetField(name,flags)!.GetValue(window)!;
void Check(bool condition,string message){if(!condition)throw new Exception(message);}
typeof(MainWindow).GetMethod("BuildProductionExecution",flags)!.Invoke(window,null);
var root=(StackPanel)Field("executionRoot");
// Tab contents are not attached to the logical tree until their tab is selected.
// Inspect each form explicitly so this check covers the inactive tabs too.
var tabs=root.Children.OfType<TabControl>().Single();
var tabItems=((IEnumerable<TabItem>)tabs.ItemsSource!).ToList();
var all=root.GetLogicalDescendants().OfType<Control>()
    .Concat(tabItems.SelectMany(t=>((Control)t.Content!).GetLogicalDescendants().OfType<Control>()))
    .Distinct().ToList();
var wood=Field("woodIssue");var accessories=Field("accessoryIssue");
object Property(object value,string name)=>value.GetType().GetProperty(name)!.GetValue(value)!;
Check(!ReferenceEquals(Property(wood,"Quantity"),Property(accessories,"Quantity")),"Wood and accessories must have independent quantity inputs");
Check(!ReferenceEquals(Property(wood,"Warehouse"),Property(accessories,"Warehouse")),"Each issue form has its own warehouse selection");
Check(all.OfType<AutoCompleteBox>().Count()>=10,"Issue/return/worker/order selectors must be searchable");
Check(all.OfType<AutoCompleteBox>().All(b=>b.FilterMode==AutoCompleteFilterMode.Contains&&b.MinimumPrefixLength==0),"Selectors must search contained names/codes and allow opening without a prefix");
Check(((IEnumerable<TabItem>)tabs.ItemsSource!).Any(t=>Equals(t.Header,"الفحص والقبول")),"Quality checks have their own tab");
Check(!((Expander)Field("queuePanel")).IsVisible,"Technical retry controls are hidden without pending/rejected commands");
typeof(MainWindow).GetMethod("UpdateQueueButtons",flags)!.Invoke(window,null);
Check(!((Button)Field("retryQueue")).IsEnabled,"Retry cannot silently do nothing with no selected failed command");
Check(all.OfType<TextBox>().All(t=>t.PlaceholderText!="المبلغ الفعلي"),"WIP report has no unexplained actual-amount input");
Console.WriteLine("PASS: Runtime form creation, searchable selectors, independent quantities, quality tab, wage basis, safe retry controls and inline action feedback.");

var progressContent=(StackPanel)tabItems[0].Content!;var costTabs=progressContent.Children.OfType<TabControl>().Single();
var sections=((IEnumerable<TabItem>)costTabs.ItemsSource!).ToList();Check(sections.Count==4,"Cost workspace has cutting, banding, assembly and packaging sections");
var oldProgress=all.OfType<Button>().Single(b=>Equals(b.Content,"حفظ إنجاز المرحلة"));
Check(oldProgress.GetLogicalAncestors().OfType<StackPanel>().Any(p=>!p.IsVisible),"Phase-progress forms are hidden");
Check(progressContent.GetLogicalDescendants().OfType<Button>().Any(b=>Equals(b.Content,"بدء العمل وحساب المصاريف")),"New start needs no route checklist");
var cutting=(StackPanel)sections[0].Content!;var assembly=(StackPanel)sections[2].Content!;
Check(cutting.GetLogicalDescendants().OfType<ComboBox>().Single().ItemCount==3,"Machine basis is unit/minute/hour");
Check(assembly.GetLogicalDescendants().OfType<ComboBox>().Single().ItemCount==6,"Worker basis includes time and daily/weekly/monthly allocations");
var inputs=cutting.GetLogicalDescendants().OfType<TextBox>().ToList();inputs[1].Text="1.2345";inputs[2].Text="10";Dispatcher.UIThread.RunJobs();
Check(cutting.GetLogicalDescendants().OfType<TextBlock>().Any(t=>t.Text?.Contains("12.345") == true),"Cost preview multiplies decimals without production progress");
var strict=typeof(MainWindow).GetMethod("StrictManufacturingNumber",BindingFlags.NonPublic|BindingFlags.Static)!;
try{strict.Invoke(null,new object[]{"1,5"});throw new Exception("Decimal comma must reject");}catch(TargetInvocationException ex){Check(ex.InnerException is InvalidOperationException,"Ambiguous quantity rejects");}
var workerType=typeof(MainWindow).GetNestedType("ManufacturingWorker",BindingFlags.NonPublic)!;
var workers=JsonSerializer.Deserialize("[{\"id\":\"w1\",\"code\":\"W-01\",\"nameAr\":\"Ahmed\"},{\"id\":\"w2\",\"code\":\"W-02\",\"nameAr\":\"Ahmed\"}]",typeof(List<>).MakeGenericType(workerType),new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
typeof(MainWindow).GetField("manufacturingWorkers",flags)!.SetValue(window,workers);typeof(MainWindow).GetMethod("RefreshManufacturingWorkers",flags)!.Invoke(window,null);
var workerPicker=(Control)Field("manufacturingWorker");Check(((System.Collections.IEnumerable)workerPicker.GetLogicalDescendants().OfType<AutoCompleteBox>().Single().ItemsSource!).Cast<object>().Count()==2,"Workers sharing a name remain separate by ID/code");
Console.WriteLine("PASS: Cost-focused subtabs, no phase checklists, machine/worker bases, exact preview, safe numeric input and unique worker selection.");
var detailType=typeof(MainWindow).GetNestedType("ExecDetail",BindingFlags.NonPublic)!;
var sample="""
{"costs":[{"type":"manufacturing_work","amount":"100","treatment":"direct","date":"2026-11-18"},{"type":"packaging","amount":"5","treatment":"direct","date":"2026-11-18"},{"type":"wage_accrual","amount":"10","treatment":"direct","date":"2026-11-18"},{"type":"wage_accrual","amount":"99","treatment":"pooled","date":"2026-11-18"},{"type":"material_issue","amount":"200","treatment":"direct","date":"2026-11-18"}],"workCosts":[{"stageCode":"cutting","amount":"30"},{"stageCode":"ashareet","amount":"20"},{"stageCode":"assembly","amount":"50"}],"wip":{"month":"2026-11"}}
""";
var detail=JsonSerializer.Deserialize(sample,detailType,new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
typeof(MainWindow).GetMethod("RenderManufacturingCosts",flags)!.Invoke(window,new[]{detail});
Check(((TextBlock)Field("manufacturingTotals")).Text!.Contains("115")&&((TextBlock)Field("manufacturingTotals")).Text!.Contains("200"),"Manufacturing total excludes materials and pooled wages");
Check(cutting.GetLogicalDescendants().OfType<TextBlock>().Any(t=>t.Text?.Contains("30 ج.م")==true),"Each cost section shows its own job total");
Console.WriteLine("PASS: Separate per-section and whole-job manufacturing totals without materials or pooled-wage double counting.");
var failWorkers=true;
Task<string> FetchProduction(string endpoint)=>endpoint switch{
    "production"=>Task.FromResult("[{\"id\":\"12345678-1234-4234-8234-123456789012\",\"orderNo\":\"JO-AVAILABLE\",\"productCode\":\"P\",\"productName\":\"Product\",\"status\":\"draft\",\"plannedQuantity\":\"1\"}]"),
    "production/catalog"=>Task.FromResult("[]"),
    "master-data"=>Task.FromResult("{\"units\":[],\"warehouses\":[],\"items\":[],\"parties\":[]}"),
    "production/workers"=>failWorkers?Task.FromException<string>(new HttpRequestException("Cannot GET /api/production/workers")):Task.FromResult("[]"),
    _=>throw new Exception("Unexpected network dependency: "+endpoint)
};
var refresh=typeof(MainWindow).GetMethod("RefreshProductionExecutionAsync",flags)!;
await (Task)refresh.Invoke(window,new object[]{(Func<string,Task<string>>)FetchProduction})!;
var jobBox=((Control)Field("executionJobs")).GetLogicalDescendants().OfType<AutoCompleteBox>().Single();
Check(((System.Collections.IEnumerable)jobBox.ItemsSource!).Cast<object>().Single().ToString()!.Contains("JO-AVAILABLE"),"A failed worker endpoint cannot erase or prevent loading job choices");
Check(jobBox.SelectedItem is null,"Initial loading requires an explicit job selection");
Check(((TextBlock)Field("manufacturingApiMessage")).Text!.Contains("production/workers"),"Dependency failure stays visible beside production controls");
failWorkers=false;await (Task)refresh.Invoke(window,new object[]{(Func<string,Task<string>>)FetchProduction})!;
Check((bool)Field("manufacturingApiReady")&&((TextBlock)Field("manufacturingApiMessage")).Text=="","Refreshing after API recovery restores manufacturing readiness");
var ensure=typeof(MainWindow).GetMethod("EnsureProductionFeatureAsync",BindingFlags.NonPublic|BindingFlags.Static)!;
using var missing=new HttpResponseMessage(System.Net.HttpStatusCode.NotFound){Content=new StringContent("Cannot POST /api/production/workers")};
try{await (Task)ensure.Invoke(null,new object[]{missing,"production/workers"})!;throw new Exception("Missing route must fail");}catch(InvalidOperationException ex){Check(ex.Message.Contains("npm.cmd run build"),"Missing worker routes explain how to update the running API");}
Console.WriteLine("PASS: Job dropdown survives worker-endpoint failure, API recovery clears the error, and stale API routes show restart guidance.");
var wageTab=(StackPanel)tabItems[2].Content!;
Check(wageTab.GetLogicalDescendants().OfType<Button>().Any(b=>Equals(b.Content,"حفظ أجر العامل")),"Wages tab has a wage-entry form alongside worker creation");
var workerPickers=((System.Collections.IEnumerable)Field("manufacturingWorkerPickers")).Cast<Control>().ToList();Check(workerPickers.Count==2&&!ReferenceEquals(workerPickers[0],workerPickers[1]),"Assembly and general wages have independent worker selection");
typeof(MainWindow).GetField("manufacturingWorkers",flags)!.SetValue(window,workers);typeof(MainWindow).GetMethod("RefreshManufacturingWorkers",flags)!.Invoke(window,null);
workerPickers[0].GetType().GetMethod("Select")!.Invoke(workerPickers[0],new object[]{"w1"});workerPickers[1].GetType().GetMethod("Select")!.Invoke(workerPickers[1],new object[]{"w2"});
Check((string)workerPickers[0].GetType().GetProperty("Value")!.GetValue(workerPickers[0])! == "w1"&&(string)workerPickers[1].GetType().GetProperty("Value")!.GetValue(workerPickers[1])! == "w2","Changing the wage-tab worker does not change the assembly worker");
Console.WriteLine("PASS: Wage entry restored beside worker registration with independent assembly/general worker choices.");
