using System.Net.Http.Json;
using System.Globalization;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Layout;

namespace WoodFactoryErp.Desktop;

public partial class MainWindow
{
    private TextBlock manufacturingApiMessage=null!;
    private bool manufacturingApiReady;
    private ProductionPicker manufacturingWorker=null!;
    private readonly List<ProductionPicker> manufacturingWorkerPickers=[];
    private TextBlock generalWorkerWages=null!;
    private TextBlock manufacturingTotals=null!,manufacturingHistory=null!,workerRegistry=null!;
    private List<ManufacturingWorker> manufacturingWorkers=[];
    private Guid workerDraftId=Guid.NewGuid();
    private readonly List<TextBox> manufacturingInputs=[];
    private readonly Dictionary<string,TextBlock> manufacturingStageTotals=[];
    private StackPanel? financeManufacturingTable;

    private void ConfigureManufacturingWorkspace(StackPanel progress,StackPanel wages,StackPanel costs,StackPanel pause)
    {
        manufacturingApiMessage=ExecText("");executionRoot.Children.Insert(4,manufacturingApiMessage);
        var packaging=(Border)packagingPanel.Parent!;var pauseBox=(Border)pause.Parent!;
        progress.Children.Remove(packaging);progress.Children.Remove(pauseBox);
        var legacy=new StackPanel{IsVisible=false};foreach(var c in progress.Children.ToArray()){progress.Children.Remove(c);legacy.Children.Add(c);}progress.Children.Add(legacy);
        var start=Card(progress,"تشغيل أمر الشغل","ابدأ تسجيل تكاليف الماكينات والعمال لهذا الأمر؛ لا حاجة لتحديد مراحل أو تسجيل أعداد إنجاز لكل مرحلة.");
        ActionButton(start,"بدء العمل وحساب المصاريف","start",f=>PostFormAsync("start",new(){["costFocused"]=true},f));
        ActionButton(start,"استخدام تسجيل التكاليف لأمر بدأ سابقاً","cost_mode",f=>PostFormAsync("cost_mode",new(),f));
        progress.Children.Add(pauseBox);
        var sections=new TabControl();progress.Children.Add(sections);
        var cutting=BuildManufacturingSection("cutting","التقطيع",false);
        var banding=BuildManufacturingSection("ashareet","الشريط",false);
        var assembly=BuildManufacturingSection("assembly","التجميع وتركيب الإكسسوارات",true);
        var pack=new StackPanel{Spacing=8};pack.Children.Add(packaging);
        sections.ItemsSource=new[]{new TabItem{Header="التقطيع",Content=cutting},new TabItem{Header="الشريط",Content=banding},new TabItem{Header="التجميع وتركيب الإكسسوارات",Content=assembly},new TabItem{Header="التعبئة والتغليف",Content=pack}};
        manufacturingTotals=ExecText("اختر أمر الشغل لعرض المصاريف الصناعية.",18);progress.Children.Add(manufacturingTotals);
        manufacturingHistory=ExecText("");progress.Children.Add(new Expander{Header="تفاصيل تكاليف الماكينات والعمال",Content=manufacturingHistory});
        progress.Children.Add(ExecText("تكلفة الخامات والإكسسوارات منفصلة. المصاريف هنا تخص أمر الشغل المختار فقط. عند قبول الإنتاج تظل التكاليف مرتبطة بالأمر للتكلفة والمالية؛ دفع الأجر لا يسجل تكلفة ثانية."));

        // Keep historical agreement controls initialized for old cached data,
        // but remove the progress-driven agreement form from the visible flow.
        var dueBox=(Border)((StackPanel)wageDue.Parent!).Parent!;wages.Children.Remove(dueBox);
        wages.Children.Remove(executionWages);var historical=new StackPanel{IsVisible=false};foreach(var c in wages.Children.ToArray()){wages.Children.Remove(c);historical.Children.Add(c);}wages.Children.Add(historical);
        var register=Card(wages,"الأجور والعاملين — إضافة عامل","كود العامل فريد. الاسم قد يتكرر؛ الاختيار دائماً بالكود والاسم معاً. يمكن تسجيل الأجر هنا لأي عمل، أو من قسم التجميع؛ كلاهما مرتبط بأمر الشغل المختار.");
        var code=ExecInput("كود العامل الفريد");code.MaxLength=60;var name=ExecInput("اسم العامل بالعربية");name.MaxLength=160;var english=ExecInput("English name — اختياري");english.MaxLength=160;
        Field(register,"كود العامل",code);Field(register,"الاسم بالعربية",name);Field(register,"الاسم بالإنجليزية",english);
        ActionButton(register,"حفظ العامل","",async f=>{
            var payload=new{id=workerDraftId,code=Nonempty(code.Text,"اكتب كود العامل."),nameAr=Nonempty(name.Text,"اكتب اسم العامل."),nameEn=english.Text?.Trim()};
            using var request=new HttpRequestMessage(HttpMethod.Post,"production/workers"){Content=JsonContent.Create(payload,options:JsonOptions)};request.Headers.Authorization=new("Bearer",accessToken);
            using var response=await HttpClient.SendAsync(request);await EnsureProductionFeatureAsync(response,"production/workers");var created=await response.Content.ReadFromJsonAsync<ManufacturingWorker>(JsonOptions)??throw new InvalidOperationException("لم تصل بيانات العامل.");
            workerDraftId=Guid.NewGuid();code.Text=name.Text=english.Text="";manufacturingWorkers.RemoveAll(w=>w.Id==created.Id);manufacturingWorkers.Add(created);RefreshManufacturingWorkers();foreach(var picker in manufacturingWorkerPickers)picker.Select(created.Id);Notice(f,"تم حفظ العامل وربطه باختيار العمال في التجميع.",true);
        });
        wages.Children.Add(BuildManufacturingSection("general_labor","أجر العامل مقابل عمل آخر",true));
        generalWorkerWages=ExecText("");var recorded=Card(wages,"كل الأجور المسجلة لهذا الأمر","يشمل أجور التجميع والأعمال الأخرى من نفس السجل؛ العرض هنا لا يسجل الأجر مرة ثانية.");recorded.Children.Add(generalWorkerWages);
        workerRegistry=ExecText("");wages.Children.Add(workerRegistry);
        var oldWages=new StackPanel{Spacing=8};oldWages.Children.Add(executionWages);oldWages.Children.Add(dueBox);wages.Children.Add(new Expander{Header="اتفاقات أجور تاريخية لهذا الأمر",Content=oldWages});
        costs.Children.Add(ExecText("المصاريف الصناعية المباشرة (ماكينات + أجور + تعبئة) تظهر في قسم العمل وفي تقارير المالية حسب شهر التسجيل؛ لا تُخصم مرة أخرى كمصروف مستقل عند الاستحقاق أو السداد."));
    }
    private StackPanel BuildManufacturingSection(string stage,string title,bool worker)
    {
        var panel=new StackPanel{Spacing=8,Margin=new Avalonia.Thickness(8)};
        var total=ExecText("إجمالي القسم لهذا الأمر: —",18);manufacturingStageTotals[stage]=total;panel.Children.Add(total);
        var form=Card(panel,title,"سجل تكلفة العمل الفعلية لهذا الأمر فقط، لا عدد المنتجات المكتملة.");
        var machine=ExecInput("اسم / كود الماكينة");machine.MaxLength=160;
        manufacturingInputs.Add(machine);
        ProductionPicker? workerPicker=null;
        if(worker){workerPicker=new ProductionPicker("ابحث بكود العامل أو اسمه");manufacturingWorkerPickers.Add(workerPicker);if(stage=="assembly")manufacturingWorker=workerPicker;Field(form,"العامل المسجل",workerPicker);}else Field(form,"الماكينة",machine);
        var choices=new List<ExecChoice>{new("unit","بالوحدة"),new("minute","بالدقيقة"),new("hour","بالساعة")};if(worker)choices.AddRange([new("daily","باليوم"),new("weekly","بالأسبوع"),new("monthly","بالشهر")]);
        var basis=new ComboBox{ItemsSource=choices,SelectedIndex=0,HorizontalAlignment=HorizontalAlignment.Stretch};Field(form,"أساس حساب التكلفة",basis);
        var quantity=ExecInput("وحدات / دقائق / ساعات / حصة الفترة لهذا الأمر");var rate=ExecInput("سعر الوحدة أو وحدة الوقت لهذا الأمر");var reference=ExecInput("مرجع جلسة العمل / توزيع الأجر — فريد");reference.MaxLength=120;var notes=ExecInput("وصف — اختياري");
        manufacturingInputs.AddRange([quantity,rate,reference,notes]);
        Field(form,"الكمية المحاسبية لهذا الأمر",quantity);Field(form,"السعر",rate);Field(form,"مرجع فريد يمنع تسجيل نفس التكلفة مرتين",reference);Field(form,"الوصف",notes);
        if(stage=="general_labor")form.Children.Add(ExecText("للأجر المقطوع: أدخل الكمية 1 والسعر هو مبلغ الأجر كله. وصف العمل مطلوب. هذا أجر مستحق؛ دفعه وتسويته يتمان في المالية."));
        if(worker)form.Children.Add(ExecText("لأجر يومي/أسبوعي/شهري: أدخل حصة هذا الأمر فقط، وليس الراتب كله لكل أمر. هذا تسجيل أجر مستحق؛ السداد في المالية."));
        var preview=ExecText("الإجمالي: —",17);form.Children.Add(preview);
        void Calculate(){try{preview.Text="الإجمالي لهذا الأمر: "+decimal.Round(StrictManufacturingNumber(quantity.Text)*StrictManufacturingNumber(rate.Text),4,MidpointRounding.AwayFromZero).ToString("0.####",CultureInfo.InvariantCulture)+" ج.م";}catch{preview.Text="الإجمالي: أدخل كمية وسعراً صحيحين.";}}
        quantity.TextChanged+=(_,_)=>Calculate();rate.TextChanged+=(_,_)=>Calculate();
        ActionButton(form,stage=="general_labor"?"حفظ أجر العامل":"حفظ تكلفة "+title,"work_cost",async f=>{
            var d=new Dictionary<string,object?>{["costStage"]=stage,["costBasis"]=EnumValue(basis),["quantity"]=StrictManufacturingNumber(quantity.Text),["rate"]=StrictManufacturingNumber(rate.Text),["documentReference"]=Nonempty(reference.Text,"اكتب مرجع العمل الفريد."),["notes"]=stage=="general_labor"?Nonempty(notes.Text,"اكتب وصف العمل الذي قام به العامل."):notes.Text?.Trim()};
            if(worker)d["workerId"]=Required(workerPicker!);else d["machineName"]=Nonempty(machine.Text,"اكتب اسم أو كود الماكينة.");
            if(await PostFormAsync("work_cost",d,f))quantity.Text=rate.Text=reference.Text=notes.Text="";
        });return panel;
    }
    private static decimal StrictManufacturingNumber(string? text)=>decimal.TryParse(text,NumberStyles.AllowDecimalPoint|NumberStyles.AllowLeadingWhite|NumberStyles.AllowTrailingWhite,CultureInfo.InvariantCulture,out var n)&&n>0&&n<=999999999&&decimal.Round(n,4)==n?n:throw new InvalidOperationException("أدخل رقماً موجباً حتى 4 منازل عشرية، بالنقطة العشرية ومن دون فواصل آلاف.");
    private async Task RefreshManufacturingAvailabilityAsync(Func<string,Task<string>> fetch)
    {
        manufacturingApiReady=false;
        try{manufacturingWorkers=JsonSerializer.Deserialize<List<ManufacturingWorker>>(await fetch("production/workers"),JsonOptions)??[];manufacturingApiReady=true;manufacturingApiMessage.Text="";RefreshManufacturingWorkers();}
        catch(Exception ex){manufacturingWorkers=[];RefreshManufacturingWorkers();manufacturingApiMessage.Text="تعذر تحميل العاملين وإعداد التكاليف: "+FriendlyError(ex);workerRegistry.Text=manufacturingApiMessage.Text;}
    }
    private static async Task EnsureProductionFeatureAsync(HttpResponseMessage response,string endpoint)
    {
        if(endpoint=="production/workers"&&(response.StatusCode==System.Net.HttpStatusCode.NotFound||
            response.StatusCode==System.Net.HttpStatusCode.BadRequest&&response.RequestMessage?.Method==HttpMethod.Get&&
            (await response.Content.ReadAsStringAsync()).Contains("uuid",StringComparison.OrdinalIgnoreCase)))throw new ProductionApiVersionException();
        await EnsureApiSuccessAsync(response);
    }
    private sealed class ProductionApiVersionException:InvalidOperationException
    {
        public ProductionApiVersionException():base("نسخة خادم API لا تحتوي على خدمة العاملين الجديدة. أوقف خادم API القديم، ثم من apps/api نفّذ npm.cmd run build ثم npm.cmd run start. تطبيق ترحيل قاعدة البيانات وحده لا يحدّث الخادم."){}
    }
    private void RefreshManufacturingWorkers(){foreach(var picker in manufacturingWorkerPickers)picker.Set(manufacturingWorkers.Select(w=>new ExecChoice(w.Id,$"{w.Code} — {w.NameAr} {w.NameEn}")));if(workerRegistry is not null)workerRegistry.Text=string.Join("\n",manufacturingWorkers.Select(w=>$"{w.Code} — {w.NameAr} | ID: {w.Id}"));}
    private void RenderManufacturingCosts(ExecDetail detail)
    {
        var workerEntries=(detail.WorkCosts??[]).Where(c=>c.WorkerId is not null).ToList();
        generalWorkerWages.Text=workerEntries.Count==0?"لم تُسجل أجور عمال لهذا الأمر بعد.":"إجمالي أجور العمال المسجلة: "+workerEntries.Sum(c=>Number(c.Amount)).ToString("0.####",CultureInfo.InvariantCulture)+" ج.م\n"+string.Join("\n\n",workerEntries.Select(c=>$"{c.WorkerName} | {ManufacturingStage(c.StageCode)} | {c.Amount} ج.م | {c.Date} | {c.Reference}\n{c.Description}"));
        foreach(var (stage,label) in manufacturingStageTotals)label.Text=$"إجمالي القسم لهذا الأمر: {(detail.WorkCosts??[]).Where(c=>c.StageCode==stage).Sum(c=>Number(c.Amount)):0.####} ج.م";
        var direct=detail.Costs.Where(c=>c.Treatment=="direct"&&new[]{"manufacturing_work","packaging","labor","wage_accrual"}.Contains(c.Type)).Sum(c=>Number(c.Amount));
        var materials=detail.Costs.Where(c=>c.Type is "material_issue" or "material_return").Sum(c=>Number(c.Amount));
        manufacturingTotals.Text=$"المصاريف الصناعية المباشرة لهذا الأمر: {direct:0.####} ج.م\nالخامات والإكسسوارات (منفصلة): {materials:0.####} ج.م";
        executionCosts.Text+=$"\nتكاليف الماكينات والعمال المسجلة هذا الشهر: {detail.Costs.Where(c=>c.Type=="manufacturing_work"&&c.Date.StartsWith(detail.Wip.Month)).Sum(c=>Number(c.Amount)):0.####} ج.م (مشمولة في الإجمالي أعلاه)";
        manufacturingHistory.Text=string.Join("\n\n",(detail.WorkCosts??[]).Select(c=>$"{c.Date} | {ManufacturingStage(c.StageCode)} | {c.WorkerName??c.MachineName}\n{c.Quantity} × {c.Rate} ({ManufacturingBasis(c.Basis)}) = {c.Amount} ج.م | {c.Reference} | {c.Description}"));
    }
    private static string ManufacturingStage(string code)=>code switch{"cutting"=>"التقطيع","ashareet"=>"الشريط","assembly"=>"التجميع وتركيب الإكسسوارات","general_labor"=>"أجر مقابل عمل آخر",_=>code};
    private static string ManufacturingBasis(string basis)=>basis switch{"unit"=>"وحدة","minute"=>"دقيقة","hour"=>"ساعة","daily"=>"يوم","weekly"=>"أسبوع","monthly"=>"شهر",_=>basis};
    private sealed record ManufacturingWorker(string Id,string Code,string NameAr,string? NameEn);
    private sealed record ManufacturingWork(string Id,string StageCode,string? WorkerId,string? WorkerName,string? MachineName,string Basis,string Quantity,string Rate,string Amount,string Reference,string Date,string? Description);
}
