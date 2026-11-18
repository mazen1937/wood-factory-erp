using System.Globalization;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform.Storage;

namespace WoodFactoryErp.Desktop;

public partial class MainWindow
{
    private TextBox monthlyMonth=null!,monthlySearch=null!;
    private ComboBox monthlyDomain=null!;
    private StackPanel monthlyTable=null!,monthlyCards=null!,monthlyExceptions=null!;
    private TextBlock monthlyStatus=null!,monthlyPageLabel=null!,monthlyBreakdown=null!;
    private Button monthlyPrevious=null!,monthlyNext=null!;
    private JsonElement monthlyData;
    private int monthlyPage,monthlyLoadVersion;
    private const int MonthlyPageSize=75;
    private readonly List<Window> monthlyWindows=[];

    private TabItem BuildMonthlyFinanceTab()
    {
        var root=new StackPanel{Spacing=9};
        root.Children.Add(ExecText("دفتر الشهر — الطلبات والمخزون والإنتاج والمالية",22));
        var controls=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8};root.Children.Add(controls);
        monthlyMonth=new TextBox{Text=DateTime.Today.ToString("yyyy-MM"),MaxLength=7,Width=115,PlaceholderText="YYYY-MM"};controls.Children.Add(monthlyMonth);
        var load=new Button{Content="عرض الشهر"};controls.Children.Add(load);load.Click+=async(_,_)=>{load.IsEnabled=false;try{await LoadMonthlyFinanceAsync();}finally{load.IsEnabled=true;}};
        var export=new Button{Content="تصدير الشهر CSV"};controls.Children.Add(export);export.Click+=async(_,_)=>{try{await ExportMonthlyFinanceAsync();}catch(Exception ex){monthlyStatus.Text=FriendlyError(ex);}};
        monthlyStatus=ExecText("اختر الشهر واضغط عرض الشهر.");root.Children.Add(monthlyStatus);
        monthlyCards=new StackPanel();root.Children.Add(monthlyCards);
        monthlyBreakdown=ExecText("");root.Children.Add(new Expander{Header="تفصيل الحسابات وما يدخل في كل إجمالي",Content=monthlyBreakdown});
        monthlyExceptions=new StackPanel{Spacing=5};root.Children.Add(new Expander{Header="مطابقة مصادر التكاليف والمخزون — أظهر الفروق",Content=monthlyExceptions});
        var searchRow=new Grid{ColumnDefinitions=new ColumnDefinitions("*,220"),ColumnSpacing=8};root.Children.Add(searchRow);
        monthlySearch=ExecInput("بحث بكود الطلب/أمر الشغل/الصنف/العامل/المرجع أو الوصف");monthlySearch.MaxLength=200;searchRow.Children.Add(monthlySearch);
        monthlyDomain=new ComboBox{ItemsSource=new[]{new ExecChoice("","كل الحركات"),new("orders","طلبات العملاء"),new("production","أوامر الشغل والتنفيذ"),new("inventory","المخزون"),new("costs","تكاليف الإنتاج والأجور"),new("finance","المستندات والخزينة"),new("master","العاملون"),new("configuration","الإعدادات المالية")},SelectedIndex=0};Grid.SetColumn(monthlyDomain,1);searchRow.Children.Add(monthlyDomain);
        monthlySearch.TextChanged+=(_,_)=>{monthlyPage=0;RenderMonthlyActivities();};monthlyDomain.SelectionChanged+=(_,_)=>{monthlyPage=0;RenderMonthlyActivities();};
        root.Children.Add(ExecText("قيمة الحركة تختلف حسب المصدر؛ لا تُجمع قيم الجدول معاً. ملخص الشهر الكامل ثابت عند البحث. اضغط «عرض» لفتح المصدر والطلبات المرتبطة.",12));
        monthlyTable=new StackPanel();root.Children.Add(monthlyTable);
        var pages=new StackPanel{Orientation=Orientation.Horizontal,Spacing=9};root.Children.Add(pages);
        monthlyPrevious=new Button{Content="السابق"};monthlyNext=new Button{Content="التالي"};monthlyPageLabel=ExecText("");pages.Children.Add(monthlyPrevious);pages.Children.Add(monthlyPageLabel);pages.Children.Add(monthlyNext);
        monthlyPrevious.Click+=(_,_)=>{monthlyPage=Math.Max(0,monthlyPage-1);RenderMonthlyActivities();};monthlyNext.Click+=(_,_)=>{monthlyPage++;RenderMonthlyActivities();};
        monthlyMonth.TextChanged+=(_,_)=>ClearMonthlyData();
        return new TabItem{Header="دفتر الشهر",Content=root};
    }
    private void ClearMonthlyData()
    {
        ++monthlyLoadVersion;monthlyData=default;monthlyPage=0;
        monthlyTable?.Children.Clear();monthlyCards?.Children.Clear();monthlyExceptions?.Children.Clear();if(monthlyBreakdown is not null)monthlyBreakdown.Text="";
        if(monthlyPageLabel is not null)monthlyPageLabel.Text="";if(monthlyStatus is not null)monthlyStatus.Text="اختر عرض الشهر لتحميل بيانات هذا الشهر.";
    }
    private void ClearMonthlySession(){ClearMonthlyData();foreach(var w in monthlyWindows.ToArray().Reverse())w.Close();monthlyWindows.Clear();}
    private async Task LoadMonthlyFinanceAsync()
    {
        if(monthlyMonth is null||string.IsNullOrWhiteSpace(accessToken)||financeOutbox is null)return;
        var month=monthlyMonth.Text?.Trim()??"";var version=++monthlyLoadVersion;var user=FinanceUser;
        try{
            if(!System.Text.RegularExpressions.Regex.IsMatch(month,@"^[2-9]\d{3}-(0[1-9]|1[0-2])$"))throw new InvalidOperationException("اختر الشهر بصيغة YYYY-MM.");
            monthlyStatus.Text="جارٍ تجميع الشهر من المصادر الرسمية…";
            using var response=JsonDocument.Parse(await FinanceGetAsync("finance/monthly?month="+month));var fresh=financeOnline;
            if(version!=monthlyLoadVersion||string.IsNullOrWhiteSpace(accessToken)||FinanceUser!=user)return;
            if(J(response.RootElement,"month")!=month)throw new InvalidOperationException("شهر الاستجابة لا يطابق الشهر المختار.");
            monthlyData=response.RootElement.Clone();monthlyPage=0;RenderMonthlySummary();RenderMonthlyActivities();
            monthlyStatus.Text=$"{(fresh?"لقطة رسمية من الخادم":"نسخة محفوظة — تعذر الاتصال؛ قد توجد حركات أحدث")} | {month} | وقت اللقطة: {J(monthlyData,"snapshotAt")}";
        }catch(Exception ex){if(version==monthlyLoadVersion)monthlyStatus.Text="تعذر تحديث دفتر الشهر: "+FriendlyError(ex);}
    }
    private static string MonthlyMoney(JsonElement value,string key)=>decimal.TryParse(J(value,key),NumberStyles.AllowLeadingSign|NumberStyles.AllowDecimalPoint,CultureInfo.InvariantCulture,out var n)?n.ToString("N4",CultureInfo.InvariantCulture):"—";
    private List<JsonElement> MonthlyActivities()=>monthlyData.ValueKind==JsonValueKind.Object&&monthlyData.TryGetProperty("activities",out var rows)?rows.EnumerateArray().ToList():[];
    private List<JsonElement> FilterMonthlyActivities()
    {
        var domain=(monthlyDomain?.SelectedItem as ExecChoice)?.Value??"";
        var terms=(monthlySearch?.Text??"").Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
        return MonthlyActivities().Where(r=>(domain==""||J(r,"domain")==domain)&&terms.All(t=>(r.GetRawText()+" "+MonthlyKind(r)).Contains(t,StringComparison.OrdinalIgnoreCase))).ToList();
    }
    private void RenderMonthlySummary()
    {
        monthlyCards.Children.Clear();monthlyExceptions.Children.Clear();if(!monthlyData.TryGetProperty("summary",out var s))return;
        var grid=new Avalonia.Controls.Primitives.UniformGrid{Columns=4};monthlyCards.Children.Add(grid);
        foreach(var (key,title) in new[]{("netSales","صافي المبيعات دون الضريبة"),("salesTax","ضريبة المبيعات المسجلة"),("grossProfit","مجمل الربح = المبيعات − تكلفة البيع"),("manufacturingIncurred","الصناعي المسجل خلال الشهر"),("periodExpenses","المصروفات المالية المسجلة"),("cashIn","المتحصلات النقدية الخارجية"),("cashOut","المدفوعات النقدية الخارجية"),("closingTreasury","رصيد الخزينة بنهاية الشهر")}){
            var card=new StackPanel{Spacing=3};card.Children.Add(ExecText(title,13));card.Children.Add(ExecText(MonthlyMoney(s,key)+" ج.م",19));grid.Children.Add(new Border{Child=card,Padding=new Thickness(9),Margin=new Thickness(3),BorderThickness=new Thickness(1),BorderBrush=Brushes.Gray,CornerRadius=new CornerRadius(6)});
        }
        monthlyBreakdown.Text=$"تكلفة تحت التشغيل أول الشهر: {MonthlyMoney(s,"openingDirectWip")} | المحول للإنتاج التام: {MonthlyMoney(s,"completedProductionCost")} | آخر الشهر: {MonthlyMoney(s,"closingDirectWip")}\nالمبيعات قبل الخصم: {MonthlyMoney(s,"grossSales")} | الخصومات: {MonthlyMoney(s,"discounts")}\nتكلفة المبيعات: {MonthlyMoney(s,"cogs")} | هامش مجمل الربح: {(J(s,"grossMargin")==""?"غير متاح لصافي مبيعات غير موجب":J(s,"grossMargin")+" %")}\nرسوم التوصيل المحصلة على المستندات: {MonthlyMoney(s,"deliveryCharges")}\nالصناعي المسجل = تشغيل وأجور وتعبئة مباشرة {MonthlyMoney(s,"directManufacturing")} + أجور مجمعة {MonthlyMoney(s,"pooledWages")} + مصاريف صناعية مالية {MonthlyMoney(s,"industrialExpenses")}\nاستهلاك الخامات والإكسسوارات بعد الرد + المستهلكات: {MonthlyMoney(s,"materialConsumption")} (منفصل عن الصناعي)\nإضافات تكلفة الإنتاج المباشرة: {MonthlyMoney(s,"directProductionAdditions")}\nاستلام خامات: {MonthlyMoney(s,"rawReceipts")} | شراء منتجات تامة: {MonthlyMoney(s,"finishedPurchases")} (مخزون؛ ليست كلها تكلفة مبيعات هذا الشهر)\nاستلامات الشهر غير المربوطة بفواتير موردين حتى نهايته: {MonthlyMoney(s,"unbilledRawReceipts")} | أجور الشهر التي لم يصدر لها استحقاق مالي حتى نهايته: {MonthlyMoney(s,"unpostedWagePayables")}\nذمم العملاء بنهاية الشهر: {MonthlyMoney(s,"receivablesAtEnd")} | الذمم الدائنة المثبتة بنهاية الشهر: {MonthlyMoney(s,"payablesAtEnd")}\nدفعات عملاء غير مخصصة بنهاية الشهر: {MonthlyMoney(s,"customerAdvancesAtEnd")} | دفعات موردين/عمال غير مخصصة: {MonthlyMoney(s,"supplierAdvancesAtEnd")} (لا تخفض الذمم قبل تخصيصها)\nالخزينة: افتتاحي {MonthlyMoney(s,"openingTreasury")} + تغير {MonthlyMoney(s,"treasuryChange")} = ختامي {MonthlyMoney(s,"closingTreasury")}\nقيود الأرصدة الافتتاحية خلال الشهر: {MonthlyMoney(s,"openingEntries")} (ليست إيراداً). التحويلات الداخلية ليست إيراداً أو مصروفاً. الفواتير المرجعية واستحقاقات الأجور لا تسجل بيعاً أو تكلفة ثانية.\nنتيجة حسابية قبل الإقفال = مجمل الربح + رسوم التوصيل − المصروفات المالية: {MonthlyMoney(s,"resultBeforeClosing")}\nهذه ليست صافي ربح معتمداً: الدفعة المقبولة بالكامل يمكن استلامها من شاشة بيع وتسليم بعد اعتماد تكلفتها. توزيع المصاريف المشتركة وإقفال الفترة لم يُنفذا بعد. لا نخصم تكلفة التشغيل مرة أخرى من مجمل الربح. أوامر بانتظار اعتماد تكلفة الإنتاج حالياً: {J(s,"pendingCostingJobs")}. الضريبة هنا مسجلة على المبيعات وليست إقراراً ضريبياً.";
        var exceptions=new List<JsonElement>();foreach(var key in new[]{"exceptions","stockExceptions"})if(monthlyData.TryGetProperty(key,out var e))exceptions.AddRange(e.EnumerateArray());
        monthlyExceptions.Children.Add(ExecText(exceptions.Count==0?"لم تظهر فروق في المطابقات المنفذة. مطابقة الرصيد الحالي منفصلة عن حركات الشهر؛ هذا ليس اعتماد إقفال.":$"توجد {exceptions.Count} فروق تحتاج مراجعة؛ لا تعتمد هذه الأرقام كإقفال نهائي."));
        foreach(var e in exceptions)monthlyExceptions.Children.Add(ExecText($"{J(e,"kind")} | المصدر {J(e,"reference")} | المتوقع {J(e,"expected")} | الفعلي {J(e,"actual")}"));
    }
    private static string MonthlyKind(JsonElement row)
    {
        var kind=J(row,"kind");if(J(row,"domain")=="inventory")return InventoryMovementLabel(kind);
        if(FinanceLabels.TryGetValue(kind,out var name)&&J(row,"domain")=="finance")return name;
        return kind switch{"customer_order"=>"طلب عميل","job_order"=>"إنشاء أمر شغل","material_issue"=>"تحميل تكلفة خامات","material_return"=>"رد تكلفة خامات","manufacturing_work"=>"تكلفة تشغيل / أجر","packaging"=>"تكلفة تعبئة","worker_created"=>"تسجيل عامل",_=>ExecutionAction(kind)};
    }
    private void RenderMonthlyActivities()
    {
        if(monthlyTable is null)return;monthlyTable.Children.Clear();var rows=FilterMonthlyActivities();var pages=Math.Max(1,(rows.Count+MonthlyPageSize-1)/MonthlyPageSize);monthlyPage=Math.Clamp(monthlyPage,0,pages-1);
        var table=new StackPanel{Spacing=1};string[] headers=["التاريخ","الحركة / الأثر","المرجع","الطلب / أمر الشغل","البيان","الكمية","قيمة المصدر","أثر الخزينة","المصدر"];
        Grid Row(string[] cells,bool header){var grid=new Grid{ColumnDefinitions=new ColumnDefinitions("95,165,130,170,250,95,120,115,80"),MinHeight=36};for(var i=0;i<cells.Length;i++){var text=ExecText(cells[i],header?14:13);text.Margin=new Thickness(5);Grid.SetColumn(text,i);grid.Children.Add(text);}return grid;}
        table.Children.Add(Row(headers,true));foreach(var r in rows.Skip(monthlyPage*MonthlyPageSize).Take(MonthlyPageSize)){
            var grid=Row([J(r,"date"),MonthlyKind(r),J(r,"reference"),J(r,"orderNo")+" / "+J(r,"jobNo"),J(r,"title"),J(r,"quantity")+" "+ManufacturingBasis(J(r,"unit")),MonthlyMoney(r,"amount"),MonthlyMoney(r,"cash")],false);
            var button=new Button{Content="عرض",Margin=new Thickness(3)};Grid.SetColumn(button,8);grid.Children.Add(button);button.Click+=async(_,_)=>{try{await ShowMonthlySourceAsync(r);}catch(Exception ex){monthlyStatus.Text=FriendlyError(ex);}};table.Children.Add(grid);
        }
        if(rows.Count==0)table.Children.Add(ExecText("لا توجد حركات مطابقة في هذا الشهر."));monthlyTable.Children.Add(new ScrollViewer{Content=table,MaxHeight=425,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,VerticalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Auto});
        if(monthlyPageLabel is not null){monthlyPageLabel.Text=$"{rows.Count} حركة مطابقة من {MonthlyActivities().Count} | صفحة {monthlyPage+1} / {pages}";monthlyPrevious.IsEnabled=monthlyPage>0;monthlyNext.IsEnabled=monthlyPage+1<pages;}
    }
    private async Task ShowMonthlySourceAsync(JsonElement row)
    {
        var root=new StackPanel{Spacing=9,Margin=new Thickness(15)};root.Children.Add(ExecText(MonthlyKind(row)+" — "+J(row,"reference"),22));root.Children.Add(ExecText(J(row,"date")+" | "+J(row,"title")));
        if(row.TryGetProperty("orderIds",out var ids))foreach(var id in ids.EnumerateArray()){
            var orderId=id.GetString()!;var button=new Button{Content="فتح شجرة الطلب المرتبط — "+orderId};root.Children.Add(button);button.Click+=async(_,_)=>{try{await ShowMonthlyOrderAsync(orderId);}catch(Exception ex){monthlyStatus.Text=FriendlyError(ex);}};
        }
        var table=new StackPanel();root.Children.Add(table);if(row.TryGetProperty("detail",out var detail)&&detail.ValueKind==JsonValueKind.Object)FinanceTable(table,["البيان","القيمة"],detail.EnumerateObject().Select(p=>new[]{MonthlyField(p.Name),p.Value.ToString()}));
        await ShowMonthlyWindowAsync(root,"تفاصيل المصدر");
    }
    private async Task ShowMonthlyOrderAsync(string id)
    {
        var user=FinanceUser;using var response=JsonDocument.Parse(await FinanceGetAsync("finance/orders/"+id+"/activity"));if(string.IsNullOrWhiteSpace(accessToken)||FinanceUser!=user)return;
        var data=response.RootElement.Clone();var root=BuildMonthlyOrderTree(data);await ShowMonthlyWindowAsync(root,"شجرة الطلب — السجل الكامل");
    }
    private StackPanel BuildMonthlyOrderTree(JsonElement data)
    {
        var root=new StackPanel{Spacing=9,Margin=new Thickness(15)};var order=data.GetProperty("order");root.Children.Add(ExecText(J(order,"orderNo")+" — "+J(order,"orderName"),23));root.Children.Add(ExecText($"العميل: {J(order,"customer")} | الحالة: {J(order,"status")} | {J(order,"date")} → {J(order,"delivery")}\n{J(order,"notes")}\nهذا سجل الطلب الكامل، ويشمل شهوراً أخرى. إجماليات الشهر تبقى في دفتر الشهر."));
        var required=new StackPanel();FinanceTable(required,["الكود","الصنف","الكمية المطلوبة","الوحدة"],data.GetProperty("requirements").EnumerateArray().Select(i=>new[]{J(i,"code"),J(i,"name"),J(i,"quantity"),J(i,"unit")}));root.Children.Add(new Expander{Header="الخامات والإكسسوارات المطلوبة — ليست صرفاً فعلياً",Content=required});
        var activities=data.GetProperty("activities").EnumerateArray().ToList();
        foreach(var job in data.GetProperty("jobs").EnumerateArray()){
            var child=new StackPanel{Spacing=7};child.Children.Add(ExecText($"{J(job,"name")} | {ExecutionStatus(J(job,"status"))} | الكمية: {J(job,"quantity")}\nالتكلفة المباشرة المسجلة: {J(job,"directCost")} | أجور مجمعة: {J(job,"pooledCost")}"));
            RenderMonthlyTreeRows(child,activities.Where(r=>J(r,"jobId")==J(job,"id")));root.Children.Add(new Expander{Header="أمر الشغل "+J(job,"jobNo"),Content=child});
        }
        var finance=new StackPanel();RenderMonthlyTreeRows(finance,activities.Where(r=>J(r,"domain")=="finance"));root.Children.Add(new Expander{Header="البيع والتحصيل والفواتير والمدفوعات المرتبطة",Content=finance});
        var all=new StackPanel();RenderMonthlyTreeRows(all,activities);root.Children.Add(new Expander{Header="كل حركات الطلب عبر الشهور",Content=all});
        var files=new StackPanel{Spacing=6};foreach(var file in data.GetProperty("attachments").EnumerateArray()){
            var attachment=JsonSerializer.Deserialize<OrderAttachmentDetail>(file.GetRawText(),JsonOptions)!;var button=new Button{Content=J(file,"name")+" — "+J(file,"fileName")};files.Children.Add(button);
            button.Click+=async(_,_)=>{try{await OpenWorkspaceAttachmentAsync(J(order,"id"),attachment);}catch(Exception ex){monthlyStatus.Text=FriendlyError(ex);}};
        }
        root.Children.Add(new Expander{Header="مرفقات الطلب",Content=files});return root;
    }
    private void RenderMonthlyTreeRows(StackPanel parent,IEnumerable<JsonElement> rows){foreach(var r in rows){var line=new StackPanel{Orientation=Orientation.Horizontal,Spacing=8};line.Children.Add(ExecText(J(r,"date")+" | "+MonthlyKind(r)+" | "+J(r,"reference")+" | "+J(r,"title")+" | "+MonthlyMoney(r,"amount")));var button=new Button{Content="تفاصيل"};line.Children.Add(button);button.Click+=async(_,_)=>{try{await ShowMonthlySourceAsync(r);}catch(Exception ex){monthlyStatus.Text=FriendlyError(ex);}};parent.Children.Add(line);}if(parent.Children.Count==0)parent.Children.Add(ExecText("لا توجد حركات مرتبطة."));}
    private async Task ShowMonthlyWindowAsync(StackPanel root,string title){var w=new Window{Title=title,Width=1080,Height=740,WindowStartupLocation=WindowStartupLocation.CenterOwner,Content=new ScrollViewer{Content=root}};var owner=monthlyWindows.LastOrDefault()??this;monthlyWindows.Add(w);try{await w.ShowDialog(owner);}finally{monthlyWindows.Remove(w);}}
    private static string MonthlyField(string name)=>name switch{"sourceId"=>"المصدر الأصلي","quantity"=>"الكمية","rate"=>"السعر","workerName"=>"العامل","machine"=>"الماكينة","basis"=>"أساس الحساب","reference"=>"المرجع","description"=>"الوصف","tax"=>"الضريبة","net"=>"الصافي","cogs"=>"تكلفة المبيعات","treatment"=>"معالجة التكلفة","itemCode"=>"كود الصنف","warehouse"=>"المخزن","unitCost"=>"تكلفة الوحدة","itemBalanceAfter"=>"رصيد الصنف بعد الحركة","itemValueAfter"=>"قيمة الصنف بعد الحركة","notes"=>"ملاحظات","serial"=>"رقم المستند الداخلي",_=>name};
    private async Task ExportMonthlyFinanceAsync()
    {
        if(monthlyData.ValueKind!=JsonValueKind.Object)throw new InvalidOperationException("اعرض الشهر أولاً.");
        var snapshot=monthlyData.Clone();var user=FinanceUser;
        var file=await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions{Title="تصدير دفتر الشهر الكامل",SuggestedFileName="ERP-"+J(snapshot,"month")+".csv",FileTypeChoices=[new FilePickerFileType("CSV"){Patterns=["*.csv"]}]});if(file is null)return;
        if(string.IsNullOrWhiteSpace(accessToken)||FinanceUser!=user)throw new InvalidOperationException("تغير المستخدم؛ افتح التقرير بحسابك قبل التصدير.");
        static string Quote(string value)=>"\""+((value.Length>0&&"=+-@\t\r".Contains(value[0]))?"'":"")+value.Replace("\"","\"\"")+"\"";
        await using var stream=await file.OpenWriteAsync();using var writer=new StreamWriter(stream,new UTF8Encoding(true));
        await writer.WriteLineAsync("الشهر,المؤشر,القيمة");var month=J(snapshot,"month");foreach(var p in snapshot.GetProperty("summary").EnumerateObject())await writer.WriteLineAsync(string.Join(",",new[]{month,p.Name,p.Value.ToString()}.Select(Quote)));
        await writer.WriteLineAsync("التاريخ,الحركة,المرجع,طلب العميل,أمر الشغل,البيان,الكمية,الوحدة,قيمة المصدر,أثر الخزينة,مفتاح المصدر");foreach(var r in snapshot.GetProperty("activities").EnumerateArray())await writer.WriteLineAsync(string.Join(",",new[]{J(r,"date"),MonthlyKind(r),J(r,"reference"),J(r,"orderNo"),J(r,"jobNo"),J(r,"title"),J(r,"quantity"),J(r,"unit"),J(r,"amount"),J(r,"cash"),J(r,"key")}.Select(Quote)));
        monthlyStatus.Text="تم تصدير كل حركات الشهر وإجمالياته، بما فيها الصفحات غير المعروضة. قيم المصادر المختلطة ليست إجمالياً مالياً واحداً.";
    }
}
