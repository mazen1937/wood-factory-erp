using System.Reflection;
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
var tabs=root.GetLogicalDescendants().OfType<TabControl>().Single();
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
((ComboBox)Field("wageBasis")).SelectedIndex=3;
Check(!((StackPanel)Field("wageUnitFields")).IsVisible,"Monthly wages do not show piecework unit count");
((ComboBox)Field("wageBasis")).SelectedIndex=0;
Check(((StackPanel)Field("wageUnitFields")).IsVisible,"Piecework explicitly asks how many units are assigned");
var progress=all.OfType<Button>().Single(b=>Equals(b.Content,"حفظ إنجاز المرحلة"));
progress.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));Dispatcher.UIThread.RunJobs();
Check(all.OfType<TextBlock>().Any(t=>t.Text?.Contains("اختر قيمة من نتائج البحث أولاً") == true),"Incomplete action must show an error next to its own button");
Check(all.OfType<TextBox>().All(t=>t.PlaceholderText!="المبلغ الفعلي"),"WIP report has no unexplained actual-amount input");
Console.WriteLine("PASS: Runtime form creation, searchable selectors, independent quantities, quality tab, wage basis, safe retry controls and inline action feedback.");
