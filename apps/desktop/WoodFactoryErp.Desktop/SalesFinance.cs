using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

namespace WoodFactoryErp.Desktop;

public partial class MainWindow
{
    private ScrollViewer? financePanel;
    private StackPanel financeRoot=null!,financeDocumentTable=null!,financeBalanceTable=null!,financeTreasuryTable=null!,financeSourceTable=null!,financeLineTable=null!,financeQueueTable=null!,financeStockTable=null!;
    private TextBlock financeStatus=null!,financeTotals=null!,financeReconciliation=null!;
    private TextBox financeSearch=null!;
    private ProductionPicker financePrintDocument=null!;
    private ProductionOutbox? financeOutbox;
    private DispatcherTimer? financeTimer;
    private bool financeSyncing,financeOnline;
    private DateTime financeNextRetry;
    private int financeFailures;
    private JsonElement financeData;
    private string? financeDraftUser;
    private bool financeRestoring;
    private readonly List<FinanceForm> financeForms=[];
    private readonly List<TextBox> financeConfigInputs=[];
    private string FinanceUser=>ExecutionUserId();
    private static readonly Dictionary<string,string> FinanceLabels=new()
    {
        ["sale"]="بيع منتج تام",["sales_return"]="مرتجع / إلغاء بيع",["invoice"]="فاتورة داخلية مجمعة",["delivery"]="تسليم جزئي",
        ["customer_receipt"]="تحصيل / مقدم عميل",["supplier_payment"]="سداد مورد / عامل",["expense"]="إثبات مصروف آجل",["supplier_invoice"]="فاتورة مورد من استلام سابق",
        ["purchase_fg"]="شراء منتج تام",["wage_payable"]="إثبات أجر مستحق للسداد",["allocation"]="تخصيص دفعة لدين",["transfer"]="تحويل بين حسابين",
        ["opening_treasury"]="رصيد خزينة افتتاحي معتمد",["customer_refund"]="رد رصيد عميل",["reversal"]="عكس مستند بسبب معتمد"
    };
    private void HideFinance(){if(financePanel is not null)financePanel.IsVisible=false;}
    private async void ShowFinanceAsync(object? sender,RoutedEventArgs e)
    {
        try{
            if(financePanel is null)BuildFinance();
            financeOutbox??=new ProductionOutbox(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"WoodFactoryErp","finance.sqlite"));
            HideProductionExecution();HomePanel.IsVisible=OrdersPanel.IsVisible=InventoryPanel.IsVisible=MasterDataPanel.IsVisible=false;financePanel!.IsVisible=true;
            await RefreshFinanceAsync();RestoreFinanceDrafts();await SyncFinanceAsync();
            financeTimer??=new DispatcherTimer{Interval=TimeSpan.FromSeconds(10)};financeTimer.Tick-=FinanceTick;financeTimer.Tick+=FinanceTick;financeTimer.Start();
        }catch(Exception ex){if(financeStatus is not null)financeStatus.Text=FriendlyError(ex);}
    }
    private async void FinanceTick(object? s,EventArgs e){if(string.IsNullOrWhiteSpace(accessToken))return;try{await SyncFinanceAsync();}catch(Exception ex){financeStatus.Text=FriendlyError(ex);}}
    private static Button FinanceButton(StackPanel panel,string title,Func<TextBlock,Task> action)
    {
        var button=new Button{Content=title,HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Right};var message=ExecText("");panel.Children.Add(button);panel.Children.Add(message);
        button.Click+=async(_,_)=>{button.IsEnabled=false;message.Text="جارٍ التنفيذ…";try{await action(message);}catch(Exception ex){Notice(message,FriendlyError(ex),false);}finally{button.IsEnabled=true;}};return button;
    }
    private void BuildFinance()
    {
        financeRoot=new StackPanel{Spacing=12,Margin=new Thickness(10)};financePanel=new ScrollViewer{Content=financeRoot,IsVisible=false};((Grid)OrdersPanel.Parent!).Children.Add(financePanel);
        financeRoot.Children.Add(ExecText("المبيعات والمالية",28));financeStatus=ExecText("");financeRoot.Children.Add(financeStatus);financeTotals=ExecText("",18);financeRoot.Children.Add(financeTotals);
        financeRoot.Children.Add(ExecText("العملة: جنيه مصري. كل مستند مستقل وله مرجع فريد. البيع يخصم المخزون ويسجل التكلفة مرة واحدة؛ الفاتورة والتحصيل لا يعيدان تسجيل البيع. المنتجات المقبولة بانتظار التكلفة غير متاحة للبيع."));
        FinanceButton(financeRoot,"تحديث الأرصدة والمستندات",async m=>{await RefreshFinanceAsync();Notice(m,financeOnline?"تم التحديث من الخادم.":"تعذر الاتصال؛ المعروض آخر نسخة محفوظة وليست تأكيداً لأرصدة حالية.",financeOnline);});
        var tabs=new TabControl();financeRoot.Children.Add(tabs);var entries=new List<TabItem>();
        var sales=new StackPanel{Spacing=10};sales.Children.Add(ExecText("اختر منتجاً تاماً من البيانات الأساسية ومخزنه. سعر البيع والخصم إجمالي البند والضريبة المعتمدة منفصلون. رسوم التوصيل لا تدخل هامش المنتج."));
        AddFinanceForm(sales,"sale",true);financeStockTable=new StackPanel();sales.Children.Add(ExecText("المخزون المتاح وتكلفته الحالية",20));sales.Children.Add(financeStockTable);entries.Add(new(){Header="المبيعات",Content=sales});
        var customers=new StackPanel{Spacing=10};customers.Children.Add(ExecText("المقدم والتحصيل يزيدان الخزينة فقط. خصصهما للدين بعد الحفظ من نموذج التخصيص. يمكنك توزيع الدفعة عبر عدة تخصيصات؛ الرصيد الباقي يظل مقدماً للعميل."));
        AddFinanceForm(customers,"customer_receipt");AddFinanceForm(customers,"allocation");AddFinanceForm(customers,"customer_refund");entries.Add(new(){Header="العملاء والتحصيل",Content=customers});
        var suppliers=new StackPanel{Spacing=10};suppliers.Children.Add(ExecText("فاتورة الخامات تربط استلاماً سابقاً ولا تستلم المخزون مرتين. إثبات أجر العامل يربط الأجر المسجل بالإنتاج ولا يضيف تكلفة جديدة. المصروف ينشئ ديناً؛ السداد يخفض الدين والخزينة ولا يكرر المصروف."));
        AddFinanceForm(suppliers,"supplier_invoice");AddFinanceForm(suppliers,"wage_payable");AddFinanceForm(suppliers,"expense");AddFinanceForm(suppliers,"supplier_payment");AddFinanceForm(suppliers,"purchase_fg",true);entries.Add(new(){Header="الموردون والأجور والمصاريف",Content=suppliers});
        var documents=new StackPanel{Spacing=10};documents.Children.Add(ExecText("الفواتير هنا مستندات داخلية بعد البيع؛ يمكن تجميع مبيعات العميل خلال الشهر. ليست فاتورة إلكترونية أو ضريبية قانونية. التسليم لا يخصم المخزون مرة ثانية. المرتجع يعيد التكلفة الأصلية ويحرر زيادة التحصيل إلى رصيد العميل."));
        AddFinanceForm(documents,"invoice");AddFinanceForm(documents,"delivery",true);AddFinanceForm(documents,"sales_return",true);AddFinanceForm(documents,"reversal");entries.Add(new(){Header="الفواتير والتسليم والتصحيح",Content=documents});
        var treasury=new StackPanel{Spacing=10};AddFinanceForm(treasury,"transfer");AddFinanceForm(treasury,"opening_treasury");financeTreasuryTable=new();treasury.Children.Add(financeTreasuryTable);entries.Add(new(){Header="الخزينة والبنوك",Content=treasury});
        var reports=new StackPanel{Spacing=10};financeSearch=ExecInput("ابحث بالمرجع أو الطرف أو نوع المستند");Field(reports,"بحث في الجداول",financeSearch);financeSearch.TextChanged+=(_,_)=>RenderFinanceTables();
        FinanceButton(reports,"تصدير المستندات المعروضة CSV",ExportFinanceCsvAsync);financeReconciliation=ExecText("");reports.Children.Add(financeReconciliation);
        financePrintDocument=new("ابحث عن المستند المعتمد للطباعة");Field(reports,"مستند داخلي للطباعة",financePrintDocument);FinanceButton(reports,"تنزيل المستند PDF",ExportFinancePdfAsync);
        financeDocumentTable=new();reports.Children.Add(ExecText("آخر ١٠٠٠ مستند — الإجماليات تحسب من كامل السجل",20));reports.Children.Add(financeDocumentTable);
        financeBalanceTable=new();reports.Children.Add(ExecText("الديون والدفعات والمقدمات",20));reports.Children.Add(financeBalanceTable);
        financeLineTable=new();reports.Children.Add(ExecText("بنود المبيعات ومراجعها والتكلفة الأصلية",20));reports.Children.Add(financeLineTable);
        financeSourceTable=new();reports.Children.Add(ExecText("مصادر مستحقة لم تربط بمستند مالي بعد",20));reports.Children.Add(financeSourceTable);entries.Add(new(){Header="الجداول والمراجعة",Content=reports});
        var config=new StackPanel{Spacing=10};BuildFinanceConfig(config);entries.Add(new(){Header="إعدادات معتمدة",Content=config});
        var queue=new StackPanel{Spacing=10};queue.Children.Add(ExecText("الحفظ غير المؤكد يحتفظ بنفس المعرّف حتى بعد إعادة تشغيل البرنامج. طلب مرفوض يوقف الإرسال التالي إلى أن تراجعه. لا يلغي هذا مستنداً رسمياً."));financeQueueTable=new();queue.Children.Add(financeQueueTable);entries.Add(new(){Header="طلبات الحفظ",Content=queue});
        tabs.ItemsSource=entries;
        Closed+=(_,_)=>{financeTimer?.Stop();financeOutbox?.Dispose();};
    }
    private FinanceForm AddFinanceForm(StackPanel parent,string kind,bool hasLines=false)
    {
        var panel=Card(parent,FinanceLabels[kind]);var form=new FinanceForm(kind,panel);financeForms.Add(form);
        Field(panel,"مرجع فريد للمستند",form.Reference);Field(panel,"تاريخ الترحيل — YYYY-MM-DD",form.Date);
        if(new[]{"sale","invoice","customer_receipt","supplier_payment","expense","supplier_invoice","purchase_fg"}.Contains(kind))Field(panel,"العميل / المورد (اتركه فارغاً عند سداد استحقاق عامل)",form.Party);
        if(new[]{"customer_receipt","supplier_payment","transfer","opening_treasury","customer_refund"}.Contains(kind))Field(panel,"حساب النقدية / البنك",form.Account);
        if(kind=="transfer")Field(panel,"الحساب المستلم",form.TargetAccount);
        if(new[]{"expense","customer_receipt","supplier_payment","transfer","opening_treasury","customer_refund"}.Contains(kind))Field(panel,"المبلغ",form.Amount);
        if(new[]{"sale","sales_return"}.Contains(kind))Field(panel,"رسوم التوصيل للعميل / المردود منها (اختياري)",form.DeliveryCharge);
        if(kind is "sale" or "customer_receipt")Field(panel,"الطلب المرتبط — اختياري",form.Order);
        if(new[]{"sales_return","delivery","customer_refund","reversal","supplier_invoice","wage_payable","supplier_payment","customer_receipt","invoice"}.Contains(kind))Field(panel,kind=="invoice"?"اختر بيعاً ثم أضفه للتجميع أدناه":kind is "supplier_payment" or "customer_receipt"?"الدين المراد سداده — اختياري للمقدم / الدفعة غير المخصصة":"المستند / المصدر الأصلي",form.Source);
        if(kind=="expense")Field(panel,"تصنيف المصروف",form.Category);
        if(kind=="sales_return")Field(panel,"نوع الحركة",form.ReturnType);
        if(kind=="allocation"){Field(panel,"الدفعة ذات الرصيد المتاح",form.Settlement);Field(panel,"الدين المطلوب تسويته",form.Obligation);Field(panel,"المبلغ المخصص الآن",form.Amount);}
        Field(panel,"الوصف / سبب التصحيح / مستند اعتماد الافتتاح",form.Notes);
        if(kind is "invoice" or "supplier_invoice"){
            FinanceButton(panel,kind=="invoice"?"إضافة البيع إلى الفاتورة":"إضافة الاستلام إلى فاتورة المورد",m=>{var id=Required(form.Source);if(!form.SaleIds.Contains(id))form.SaleIds.Add(id);RenderInvoiceSelection(form);SaveFinanceDrafts();Notice(m,"تمت الإضافة. يمكنك تجميع مصادر أخرى لنفس الطرف قبل الحفظ.",true);return Task.CompletedTask;});
            panel.Children.Add(form.CartPanel);FinanceButton(panel,"مسح تجميع الفاتورة",m=>{form.SaleIds.Clear();RenderInvoiceSelection(form);SaveFinanceDrafts();return Task.CompletedTask;});
        }
        if(hasLines){
            if(kind is "sales_return" or "delivery")Field(panel,"البند الأصلي من البيع المختار",form.OriginalLine);
            else{Field(panel,"المنتج التام — ابحث بالكود أو الاسم",form.Item);Field(panel,"مخزن المنتجات التامة",form.Warehouse);}
            Field(panel,"العدد / الكمية",form.Quantity);
            if(kind is "sale" or "purchase_fg")Field(panel,kind=="sale"?"سعر بيع الوحدة قبل الخصم والضريبة":"صافي تكلفة اقتناء الوحدة",form.Price);
            if(kind=="sale"){Field(panel,"خصم إجمالي البند — مبلغ",form.Discount);Field(panel,"سياسة الضريبة المعتمدة",form.Tax);}
            FinanceButton(panel,"إضافة / تحديث بند",m=>{AddFinanceCartLine(form);Notice(m,"تم تحديث جدول البنود. لم يرحّل المستند بعد.",true);return Task.CompletedTask;});panel.Children.Add(form.CartPanel);
            FinanceButton(panel,"مسح البنود الحالية",m=>{form.Lines.Clear();RenderFinanceCart(form);SaveFinanceDrafts();return Task.CompletedTask;});
        }
        FinanceButton(panel,"مراجعة وحفظ المستند",async message=>{
            var data=FinancePayload(form);await SubmitFinanceAsync("documents",data,message);
        });
        form.Source.Changed+=()=>{RefreshFinanceSourceLines(form);return Task.CompletedTask;};
        form.Party.Changed+=()=>{RefreshFinanceSources(form);form.Order.Set(Rows("orders").Where(o=>J(o,"customer_id")==form.Party.Value).Select(o=>new ExecChoice(J(o,"id"),J(o,"order_no")+" — "+J(o,"product_name"))));return Task.CompletedTask;};
        foreach(var input in FinanceInputs(form))input.TextChanged+=(_,_)=>SaveFinanceDrafts();
        foreach(var picker in FinancePickers(form))picker.Changed+=()=>{SaveFinanceDrafts();return Task.CompletedTask;};
        return form;
    }
    private void AddFinanceCartLine(FinanceForm f)
    {
        var quantity=FinanceNumber(f.Quantity.Text,true);var row=new Dictionary<string,object?>{["quantity"]=quantity};string key,label;
        if(f.Kind is "delivery" or "sales_return"){
            var source=Required(f.OriginalLine);var original=Rows("lines").First(l=>J(l,"id")==source);key=source;label=J(original,"item_code")+" — "+J(original,"item_name");row["sourceLineId"]=source;row["itemId"]=J(original,"item_id");row["warehouseId"]=J(original,"warehouse_id");
        }else{
            row["itemId"]=Required(f.Item);row["warehouseId"]=Required(f.Warehouse);row["unitPrice"]=FinanceNumber(f.Price.Text);key=Required(f.Item)+":"+Required(f.Warehouse);label=f.Item.Label;
            if(f.Kind=="sale"){row["discount"]=FinanceNumber(f.Discount.Text);row["taxPolicyId"]=Required(f.Tax);}
        }
        f.Lines.RemoveAll(l=>l.Key==key);f.Lines.Add(new FinanceCartLine(key,label,row));RenderFinanceCart(f);SaveFinanceDrafts();
    }
    private static string FinanceNumber(string? text,bool positive=false)
    {
        text=text?.Trim();if(!System.Text.RegularExpressions.Regex.IsMatch(text??"",@"^\d{1,12}(\.\d{1,4})?$"))throw new InvalidOperationException("أدخل رقماً حتى ٤ منازل عشرية، دون فواصل آلاف.");
        var value=decimal.Parse(text!,CultureInfo.InvariantCulture);if(positive&&value<=0)throw new InvalidOperationException("القيمة يجب أن تكون أكبر من صفر.");return value.ToString("0.0000",CultureInfo.InvariantCulture);
    }
    private Dictionary<string,object?> FinancePayload(FinanceForm f)
    {
        var d=new Dictionary<string,object?>{["kind"]=f.Kind,["reference"]=Nonempty(f.Reference.Text,"اكتب مرجعاً فريداً للمستند."),["date"]=ValidDate(f.Date.Text),["notes"]=f.Notes.Text?.Trim()};
        if(new[]{"sale","purchase_fg","delivery","sales_return"}.Contains(f.Kind)&&f.Lines.Count==0)throw new InvalidOperationException("أضف بنداً واحداً على الأقل قبل الحفظ.");
        if(new[]{"sale","purchase_fg","invoice","supplier_invoice","expense","customer_receipt"}.Contains(f.Kind))d["partyId"]=Required(f.Party);
        if(new[]{"customer_receipt","supplier_payment","transfer","opening_treasury","customer_refund"}.Contains(f.Kind))d["accountId"]=Required(f.Account);
        if(f.Party.Value is not null)d["partyId"]=f.Party.Value;
        if(f.Account.Value is not null)d["accountId"]=f.Account.Value;
        if(f.TargetAccount.Value is not null)d["targetAccountId"]=f.TargetAccount.Value;
        if(f.Order.Value is not null)d["orderId"]=f.Order.Value;
        if(new[]{"expense","customer_receipt","supplier_payment","transfer","opening_treasury","customer_refund"}.Contains(f.Kind))d["amount"]=FinanceNumber(f.Amount.Text,true);
        if(f.Kind is "sale" or "sales_return")d["deliveryCharge"]=FinanceNumber(f.DeliveryCharge.Text);
        if(f.Lines.Count>0)d["lines"]=f.Lines.Select(l=>l.Values).ToArray();
        if(f.Kind=="expense")d["category"]=EnumValue(f.Category);
        if(f.Kind=="sales_return")d["returnType"]=EnumValue(f.ReturnType);
        if(f.Kind=="invoice"){if(f.SaleIds.Count==0)throw new InvalidOperationException("أضف المبيعات إلى الفاتورة أولاً.");d["saleIds"]=f.SaleIds.ToArray();}
        if(new[]{"sales_return","delivery","customer_refund","reversal"}.Contains(f.Kind))d["sourceId"]=Required(f.Source);
        if(f.Kind is "supplier_payment" or "customer_receipt"){if(f.Source.Value is not null)d["obligationId"]=f.Source.Value;else if(f.Party.Value is null)throw new InvalidOperationException("اختر الطرف أو دين العامل المراد سداده.");}
        if(f.Kind=="allocation")d["allocations"]=new[]{new{settlementId=Required(f.Settlement),obligationId=Required(f.Obligation),amount=FinanceNumber(f.Amount.Text,true)}};
        if(f.Kind is "supplier_invoice" or "wage_payable"){
            if(f.Kind=="supplier_invoice"){if(f.SaleIds.Count==0)throw new InvalidOperationException("أضف الاستلامات إلى فاتورة المورد أولاً.");d["sourceIds"]=f.SaleIds.ToArray();d["sourceType"]="inventory_receipt";}
            else{var id=Required(f.Source);d["sourceIds"]=new[]{id};d["sourceType"]=J(Rows("wages").First(w=>J(w,"id")==id),"source_type");}
        }
        return d;
    }
    private void RenderFinanceCart(FinanceForm f)
    {
        string Format(decimal value)=>value.ToString("0.0000",CultureInfo.InvariantCulture);
        decimal total=0;var rows=new List<string[]>();
        foreach(var l in f.Lines){var q=Number(l.Values["quantity"]?.ToString());var price=Number(l.Values.GetValueOrDefault("unitPrice")?.ToString());var discount=Number(l.Values.GetValueOrDefault("discount")?.ToString());
            var net=decimal.Round(q*price,4,MidpointRounding.AwayFromZero)-discount;
            var rate=Rows("policies").Where(p=>J(p,"id")==l.Values.GetValueOrDefault("taxPolicyId")?.ToString()).Select(p=>Number(J(p,"rate"))).FirstOrDefault();
            var tax=decimal.Round(net*rate/100,4,MidpointRounding.AwayFromZero);total+=net+tax;
            rows.Add([l.Label,Format(q),Format(price),Format(discount),f.Kind is "sale" or "purchase_fg"?Format(net):"حسب المستند الأصلي",Format(tax),f.Kind is "sale" or "purchase_fg"?Format(net+tax):"حسب المستند الأصلي"]);
        }
        FinanceTable(f.CartPanel,["البند","العدد","سعر الوحدة","الخصم","الصافي","الضريبة","الإجمالي"],rows);
        if(f.Kind is "sale" or "purchase_fg")f.CartPanel.Children.Add(ExecText("إجمالي البنود للمراجعة: "+Format(total)+" + رسوم التوصيل المدخلة أعلاه. يعيد الخادم التحقق من الأسعار والضريبة والمخزون عند الحفظ."));
    }
    private void RenderInvoiceSelection(FinanceForm f)=>FinanceTable(f.CartPanel,["المصادر المختارة للفاتورة"],f.SaleIds.Select(id=>new[]{Rows(f.Kind=="invoice"?"documents":"receipts").Where(d=>J(d,"id")==id).Select(d=>J(d,f.Kind=="invoice"?"reference":"document_reference")+" | "+J(d,f.Kind=="invoice"?"total":"amount")).FirstOrDefault()??id}));
    private void BuildFinanceConfig(StackPanel panel)
    {
        panel.Children.Add(ExecText("للمدير فقط. لا توجد نسبة ضريبة افتراضية. أدخل أساس اعتماد المحاسب حتى عند الصفر أو الإعفاء. الحسابات تستخدم الجنيه المصري؛ التحويل بين عملات مختلفة غير متاح."));
        var account=Card(panel,"حساب نقدية / بنك");TextBox code=ExecInput("كود الحساب"),name=ExecInput("اسم الحساب");var type=new ComboBox{ItemsSource=new[]{new ExecChoice("cash","نقدية"),new ExecChoice("bank","بنك")},SelectedIndex=0};Field(account,"الكود",code);Field(account,"الاسم",name);Field(account,"النوع",type);
        FinanceButton(account,"اعتماد الحساب",m=>SubmitFinanceAsync("configuration",new(){["kind"]="account",["code"]=Nonempty(code.Text,"اكتب الكود."),["name"]=Nonempty(name.Text,"اكتب اسم الحساب."),["accountType"]=EnumValue(type)},m));
        var tax=Card(panel,"سياسة ضريبية معتمدة — إصدار مؤرخ");TextBox taxCode=ExecInput("كود السياسة"),rate=ExecInput("النسبة — %"),basis=ExecInput("مرجع / أساس اعتماد المحاسب"),from=ExecInput("YYYY-MM-DD");from.Text=DateTime.Today.ToString("yyyy-MM-dd");Field(tax,"كود السياسة",taxCode);Field(tax,"النسبة",rate);Field(tax,"ساري من",from);Field(tax,"أساس الاعتماد",basis);
        FinanceButton(tax,"اعتماد السياسة",m=>SubmitFinanceAsync("configuration",new(){["kind"]="tax",["code"]=Nonempty(taxCode.Text,"اكتب كود السياسة."),["rate"]=FinanceNumber(rate.Text),["name"]=Nonempty(basis.Text,"اكتب أساس اعتماد الضريبة."),["effectiveFrom"]=ValidDate(from.Text)},m));
        financeConfigInputs.AddRange([code,name,taxCode,rate,basis,from]);
    }
    private async Task SubmitFinanceAsync(string endpoint,Dictionary<string,object?> data,TextBlock feedback)
    {
        if(financeOutbox is null)throw new InvalidOperationException("افتح المالية وسجل الدخول أولاً.");var user=FinanceUser;
        var fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(endpoint+JsonSerializer.Serialize(data,JsonOptions))));
        var id=financeOutbox.Enqueue(user,endpoint,fingerprint,key=>{var d=new Dictionary<string,object?>(data){["operationId"]=key};return JsonSerializer.Serialize(d,JsonOptions);});
        financeNextRetry=DateTime.MinValue;await SyncFinanceAsync();
        if(string.IsNullOrWhiteSpace(accessToken)||FinanceUser!=user)throw new InvalidOperationException("تغير المستخدم؛ الطلب محفوظ للحساب الأصلي.");
        var cmd=financeOutbox.Command(user,id);
        Notice(feedback,cmd?.Status=="accepted"?"تم الحفظ رسمياً. راجع المستند والأثر في الجداول؛ لا تعد إدخال نفس الحدث بمرجع جديد.":cmd?.Status=="blocked"?"لم يحفظ: "+ExecutionError(cmd.Detail)+" راجع طلبات الحفظ قبل التصحيح.":"الحفظ غير مؤكد. الطلب محفوظ محلياً بنفس المعرّف وسيعاد إرساله؛ الأرصدة الرسمية لم تؤكد بعد.",cmd?.Status=="accepted");
    }
    private async Task SyncFinanceAsync()
    {
        if(financeSyncing||financeOutbox is null||string.IsNullOrWhiteSpace(accessToken)||DateTime.UtcNow<financeNextRetry)return;financeSyncing=true;
        try{
            var user=FinanceUser;var changed=false;
            foreach(var cmd in financeOutbox.Commands(user)){
                if(cmd.Status=="blocked")break;
                try{
                    using var request=new HttpRequestMessage(HttpMethod.Post,"finance/"+cmd.JobId){Content=new StringContent(cmd.Payload,Encoding.UTF8,"application/json")};request.Headers.Authorization=new("Bearer",accessToken);
                    using var response=await HttpClient.SendAsync(request);var json=await response.Content.ReadAsStringAsync();if(string.IsNullOrWhiteSpace(accessToken)||FinanceUser!=user)return;
                    if(response.IsSuccessStatusCode){using var parsed=JsonDocument.Parse(json);if(J(parsed.RootElement,"operationId")!=cmd.Id)throw new HttpRequestException("تعذر تأكيد هوية الحفظ.");financeOutbox.Mark(cmd.Id,"accepted",json);financeOnline=true;financeFailures=0;changed=true;}
                    else if((int)response.StatusCode is >=400 and <500&&response.StatusCode!=HttpStatusCode.Unauthorized&&response.StatusCode!=HttpStatusCode.TooManyRequests){financeOutbox.Mark(cmd.Id,"blocked",json);break;}
                    else{FinanceBackoff();break;}
                }catch(Exception ex)when(ex is HttpRequestException or TaskCanceledException or JsonException){FinanceBackoff();break;}
            }
            if(changed)await RefreshFinanceAsync();RenderFinanceQueue();
        }finally{financeSyncing=false;}
    }
    private void FinanceBackoff(){financeOnline=false;financeNextRetry=DateTime.UtcNow.AddSeconds(Math.Min(300,10*Math.Pow(2,Math.Min(5,financeFailures++))));}
    private async Task<string> FinanceGetAsync(string endpoint)
    {
        var key=FinanceUser+":"+endpoint;
        try{using var request=new HttpRequestMessage(HttpMethod.Get,endpoint);request.Headers.Authorization=new("Bearer",accessToken);using var response=await HttpClient.SendAsync(request);if((int)response.StatusCode is >=400 and <500&&response.StatusCode!=HttpStatusCode.TooManyRequests)throw new InvalidOperationException("تعذر عرض البيانات المالية: "+ExecutionError(await response.Content.ReadAsStringAsync()));await EnsureApiSuccessAsync(response);var json=await response.Content.ReadAsStringAsync();financeOutbox!.Cache(key,json);financeOnline=true;return json;}
        catch(HttpRequestException){financeOnline=false;return financeOutbox!.Cached(key)??throw new InvalidOperationException("تعذر الاتصال ولا توجد نسخة مالية محفوظة لهذا المستخدم.");}
        catch(TaskCanceledException){financeOnline=false;return financeOutbox!.Cached(key)??throw new InvalidOperationException("انتهت مهلة الاتصال ولا توجد نسخة محفوظة.");}
    }
    private async Task RefreshFinanceAsync()
    {
        var user=FinanceUser;using var doc=JsonDocument.Parse(await FinanceGetAsync("finance"));var financeFresh=financeOnline;if(string.IsNullOrWhiteSpace(accessToken)||FinanceUser!=user)return;financeData=doc.RootElement.Clone();
        currentMasterData=JsonSerializer.Deserialize<MasterDataResponse>(await FinanceGetAsync("master-data"),JsonOptions);
        if(string.IsNullOrWhiteSpace(accessToken)||FinanceUser!=user)return;financeOnline=financeOnline&&financeFresh;
        financeRestoring=true;
        foreach(var f in financeForms){
            var customers=f.Kind is "sale" or "invoice" or "customer_receipt";f.Party.Set((currentMasterData?.Parties??[]).Where(p=>p.PartyType=="both"||p.PartyType==(customers?"customer":"supplier")).Select(p=>new ExecChoice(p.Id,p.Code+" — "+p.NameAr)));
            var accounts=Rows("accounts").Select(a=>new ExecChoice(J(a,"id"),J(a,"code")+" — "+J(a,"name_ar")+" | الرصيد: "+J(a,"balance"))).ToList();f.Account.Set(accounts);f.TargetAccount.Set(accounts);
            f.Item.Set((currentMasterData?.Items??[]).Where(i=>i.ItemType=="finished_good"&&i.StockTracked).Select(i=>new ExecChoice(i.Id,i.Code+" — "+i.NameAr)));
            f.Warehouse.Set((currentMasterData?.Warehouses??[]).Where(w=>w.WarehouseType=="finished_goods").Select(w=>new ExecChoice(w.Id,w.Code+" — "+w.NameAr)),firstIfSingle:true);
            f.Order.Set(Rows("orders").Where(o=>J(o,"customer_id")==f.Party.Value).Select(o=>new ExecChoice(J(o,"id"),J(o,"order_no")+" — "+J(o,"product_name"))));
            f.Tax.Set(Rows("policies").Select(p=>new ExecChoice(J(p,"id"),J(p,"code")+" | "+J(p,"rate")+"% | من "+J(p,"effective_from").Split('T')[0])));
            f.Settlement.Set(Rows("settlements").Where(s=>Number(J(s,"available"))>0).Select(s=>new ExecChoice(J(s,"id"),J(s,"reference")+" | "+PartyLabel(s)+" | متاح: "+J(s,"available"))));
            f.Obligation.Set(Rows("obligations").Where(s=>Number(J(s,"outstanding"))>0).Select(s=>new ExecChoice(J(s,"id"),J(s,"reference")+" | "+PartyLabel(s)+" | متبقٍ: "+J(s,"outstanding"))));RefreshFinanceSources(f);
        }
        financeRestoring=false;
        financePrintDocument.Set(Rows("documents").Select(d=>new ExecChoice(J(d,"id"),J(d,"reference")+" — "+Label(J(d,"kind")))));
        financeStockRows=Rows("stock").Select(r=>new[]{J(r,"code"),J(r,"name_ar"),J(r,"warehouse"),J(r,"quantity"),J(r,"unit_cost")}).ToList();
        RenderFinanceTables();RenderFinanceQueue();
    }
    private void RefreshFinanceSources(FinanceForm f)
    {
        IEnumerable<ExecChoice> choices=[];
        if(f.Kind=="supplier_invoice")choices=Rows("receipts").Where(r=>J(r,"party_id")==f.Party.Value).Select(r=>new ExecChoice(J(r,"id"),J(r,"document_reference")+" | "+J(r,"amount")));
        else if(f.Kind=="wage_payable")choices=Rows("wages").Select(r=>new ExecChoice(J(r,"id"),J(r,"worker_name")+" | "+J(r,"date")+" | "+J(r,"amount")));
        else if(f.Kind is "supplier_payment" or "customer_receipt")choices=Rows("obligations").Where(r=>J(r,"side")== (f.Kind=="supplier_payment"?"ap":"ar")&&J(r,"party_id")== (f.Party.Value??"")&&Number(J(r,"outstanding"))>0).Select(r=>new ExecChoice(J(r,"id"),J(r,"reference")+" | "+PartyLabel(r)+" | "+J(r,"outstanding")));
        else if(f.Kind=="customer_refund")choices=Rows("settlements").Where(s=>J(s,"side")=="ar"&&Number(J(s,"available"))>0).Select(s=>new ExecChoice(J(s,"document_id"),J(s,"reference")+" | "+PartyLabel(s)+" | "+J(s,"available")));
        else if(f.Kind=="reversal")choices=Rows("documents").Where(d=>new[]{"customer_receipt","supplier_payment","expense","transfer"}.Contains(J(d,"kind"))).Select(d=>new ExecChoice(J(d,"id"),J(d,"reference")+" | "+Label(J(d,"kind"))));
        else choices=Rows("documents").Where(d=>J(d,"kind")=="sale"&&(f.Kind!="invoice"||J(d,"party_id")==f.Party.Value)).Select(d=>new ExecChoice(J(d,"id"),J(d,"reference")+" | "+PartyLabel(d)+" | "+J(d,"total")));
        f.Source.Set(choices);RefreshFinanceSourceLines(f);
    }
    private void RefreshFinanceSourceLines(FinanceForm f){f.OriginalLine.Set(Rows("lines").Where(l=>J(l,"document_id")==f.Source.Value).Select(l=>new ExecChoice(J(l,"id"),J(l,"item_code")+" — "+J(l,"item_name")+" | بيع: "+J(l,"quantity")+" | مرتجع: "+J(l,"returned")+" | مسلم: "+J(l,"delivered"))));}
    private IEnumerable<JsonElement> Rows(string property)=>financeData.ValueKind==JsonValueKind.Object&&financeData.TryGetProperty(property,out var value)&&value.ValueKind==JsonValueKind.Array?value.EnumerateArray():[];
    private static string J(JsonElement value,string name)=>JsonValue(value,name);
    private static string Label(string kind)=>FinanceLabels.GetValueOrDefault(kind,kind);
    private static string PartyLabel(JsonElement r)=>J(r,"party")==""?J(r,"worker_name"):J(r,"party");
    private bool FinanceMatch(params string[] fields)=>string.IsNullOrWhiteSpace(financeSearch.Text)||fields.Any(f=>f.Contains(financeSearch.Text.Trim(),StringComparison.OrdinalIgnoreCase));
    private void RenderFinanceTables()
    {
        if(financeData.ValueKind!=JsonValueKind.Object)return;var summary=financeData.GetProperty("summary");
        financeStatus.Text=financeOnline?"متصل — الأرقام من آخر تحديث مؤكد بالخادم.":"تعذر الاتصال — نسخة محفوظة؛ لا تعتمدها كرصيد حالي.";
        financeTotals.Text=$"صافي المبيعات: {J(summary,"net")} | تكلفة المباع: {J(summary,"cogs")} | مجمل الربح: {J(summary,"profit")} | الهامش: {J(summary,"margin")} %\nالضريبة: {J(summary,"tax")} | رسوم التوصيل: {J(summary,"delivery")}\nمديونية العملاء: {J(summary,"receivables")} | ديون الموردين والأجور: {J(summary,"payables")} | مقدمات العملاء غير المخصصة: {J(summary,"advances")} | إجمالي الخزينة: {J(summary,"treasury")}";
        FinanceTable(financeDocumentTable,["رقم داخلي","التاريخ","النوع","المرجع","الطرف","الصافي","الضريبة","توصيل","الإجمالي","التكلفة","المسجل"],Rows("documents").Where(d=>FinanceMatch(J(d,"reference"),PartyLabel(d),Label(J(d,"kind")))).Select(d=>new[]{J(d,"serial"),J(d,"posting_date").Split('T')[0],Label(J(d,"kind")),J(d,"reference"),PartyLabel(d),J(d,"net"),J(d,"tax"),J(d,"delivery_charge"),J(d,"total"),J(d,"cogs"),J(d,"actor")}));
        var obligations=Rows("obligations").Where(r=>FinanceMatch(J(r,"reference"),PartyLabel(r))).Select(r=>new[]{J(r,"side")=="ar"?"دين عميل":"دين مورد / عامل",J(r,"reference"),PartyLabel(r),J(r,"amount"),J(r,"credited"),J(r,"allocated"),J(r,"outstanding")});
        var settlements=Rows("settlements").Where(r=>FinanceMatch(J(r,"reference"),PartyLabel(r))).Select(r=>new[]{J(r,"side")=="ar"?"دفعة عميل":"دفعة مورد / عامل",J(r,"reference"),PartyLabel(r),J(r,"amount"),"—",J(r,"allocated"),J(r,"available")});
        FinanceTable(financeBalanceTable,["السجل","المرجع","الطرف","الأصلي","الائتمان","المخصص","المتبقي / المتاح"],obligations.Concat(settlements));
        FinanceTable(financeTreasuryTable,["الحساب","النوع","العملة","الرصيد"],Rows("accounts").Select(a=>new[]{J(a,"name_ar"),J(a,"account_type")=="cash"?"نقدية":"بنك",J(a,"currency"),J(a,"balance")}));
        var ledger=new StackPanel();financeTreasuryTable.Children.Add(ledger);FinanceTable(ledger,["التاريخ","الحساب","المستند","المرجع","داخل / خارج"],Rows("treasury").Select(m=>new[]{J(m,"posting_date").Split('T')[0],J(m,"name_ar"),Label(J(m,"kind")),J(m,"reference"),J(m,"amount")}));
        FinanceTable(financeLineTable,["المستند","الصنف","العدد","الوحدة","الصافي","الضريبة","التكلفة","المرتجع","المسلم"],Rows("lines").Where(r=>FinanceMatch(J(r,"reference"),J(r,"item_code"),J(r,"item_name"))).Select(r=>new[]{J(r,"reference"),J(r,"item_code")+" — "+J(r,"item_name"),J(r,"quantity"),J(r,"unit_code"),J(r,"net"),J(r,"tax"),J(r,"cogs"),J(r,"returned"),J(r,"delivered")}));
        FinanceTable(financeSourceTable,["نوع المصدر","المرجع / العامل","الطرف","المبلغ","التاريخ"],Rows("receipts").Select(r=>new[]{"استلام خامات",J(r,"document_reference"),PartyLabel(r),J(r,"amount"),J(r,"date")}).Concat(Rows("wages").Select(w=>new[]{"أجر إنتاج",J(w,"worker_name"),"عامل",J(w,"amount"),J(w,"date")})));
        var r=financeData.GetProperty("reconciliations");var labels=new Dictionary<string,string>{{"invalid_obligations","أرصدة الديون"},{"invalid_settlements","أرصدة الدفعات"},{"invalid_transfers","التحويلات"},{"invalid_sales","إجماليات البيع"},{"invalid_stock_quantities","كميات المخزون"},{"invalid_stock_values","قيم المخزون"}};
        financeReconciliation.Text="استثناءات المراجعة (المتوقع صفر): "+string.Join(" | ",r.EnumerateObject().Select(p=>labels.GetValueOrDefault(p.Name,"مراجعة إضافية")+": "+p.Value));
        financeReconciliation.Foreground=r.EnumerateObject().Any(p=>p.Value.GetInt32()!=0)?Brushes.IndianRed:Brushes.ForestGreen;
        FinanceTable(financeStockTable,["كود المنتج","الاسم","المخزن","الكمية","متوسط التكلفة"],financeStockRows);
    }
    private List<string[]> financeStockRows=[];
    private static TextBox[] FinanceInputs(FinanceForm f)=>[f.Reference,f.Date,f.Amount,f.DeliveryCharge,f.Notes,f.Quantity,f.Price,f.Discount];
    private static ProductionPicker[] FinancePickers(FinanceForm f)=>[f.Party,f.Account,f.TargetAccount,f.Source,f.Item,f.Warehouse,f.Tax,f.OriginalLine,f.Order,f.Obligation,f.Settlement];
    private void SaveFinanceDrafts()
    {
        if(financeRestoring||financeOutbox is null||string.IsNullOrWhiteSpace(accessToken)||financeDraftUser!=FinanceUser)return;
        var drafts=financeForms.Select(f=>new FinanceDraft(f.Kind,FinanceInputs(f).Select(i=>i.Text??"").ToArray(),FinancePickers(f).Select(p=>p.Value).ToArray(),f.Lines.ToList(),f.SaleIds.ToList(),f.Category.SelectedIndex,f.ReturnType.SelectedIndex)).ToList();
        financeOutbox.Cache("forms:"+FinanceUser,JsonSerializer.Serialize(drafts,JsonOptions));
    }
    private void RestoreFinanceDrafts()
    {
        if(financeDraftUser==FinanceUser)return;financeDraftUser=FinanceUser;financeRestoring=true;
        try{var json=financeOutbox?.Cached("forms:"+FinanceUser);if(json is null)return;var drafts=JsonSerializer.Deserialize<List<FinanceDraft>>(json,JsonOptions)??[];
            foreach(var d in drafts){var f=financeForms.FirstOrDefault(f=>f.Kind==d.Kind);if(f is null)continue;var inputs=FinanceInputs(f);for(var i=0;i<Math.Min(inputs.Length,d.Text.Length);i++)inputs[i].Text=d.Text[i];var pickers=FinancePickers(f);for(var i=0;i<Math.Min(pickers.Length,d.Choices.Length);i++){pickers[i].Select(d.Choices[i]);if(i==0)RefreshFinanceSources(f);if(i==3)RefreshFinanceSourceLines(f);}f.Lines=d.Lines;f.SaleIds=d.SaleIds;f.Category.SelectedIndex=d.Category;f.ReturnType.SelectedIndex=d.ReturnType;if(f.Kind is "invoice" or "supplier_invoice")RenderInvoiceSelection(f);else RenderFinanceCart(f);}
        }finally{financeRestoring=false;}
    }
    private void ClearFinanceSession()
    {
        SaveFinanceDrafts();financeDraftUser=null;financeRestoring=true;
        try{foreach(var f in financeForms){foreach(var i in FinanceInputs(f))i.Text="";f.Date.Text=DateTime.Today.ToString("yyyy-MM-dd");f.Discount.Text=f.DeliveryCharge.Text="0";foreach(var p in FinancePickers(f))p.Set([]);f.Lines.Clear();f.SaleIds.Clear();f.CartPanel.Children.Clear();}financeData=default;financeStockRows.Clear();foreach(var i in financeConfigInputs)i.Text="";
            if(financePanel is not null){financeTotals.Text=financeStatus.Text=financeReconciliation.Text="";foreach(var table in new[]{financeDocumentTable,financeBalanceTable,financeTreasuryTable,financeSourceTable,financeLineTable,financeQueueTable,financeStockTable})table.Children.Clear();financePrintDocument.Set([]);}
        }
        finally{financeRestoring=false;}
    }
    private void RenderFinanceQueue()
    {
        if(financeOutbox is null||string.IsNullOrWhiteSpace(accessToken))return;financeQueueTable.Children.Clear();
        var commands=financeOutbox.Commands(FinanceUser);if(commands.Count==0){financeQueueTable.Children.Add(ExecText("لا توجد طلبات حفظ معلقة أو مرفوضة."));return;}
        foreach(var cmd in commands){using var parsed=JsonDocument.Parse(cmd.Payload);var card=Card(financeQueueTable,J(parsed.RootElement,"reference")==""?"إعداد مالي":J(parsed.RootElement,"reference"));card.Children.Add(ExecText(cmd.Status=="blocked"?"مرفوض: "+ExecutionError(cmd.Detail):"غير مؤكد — بانتظار الإرسال بنفس المعرّف"));
            if(cmd.Status=="blocked"){
                FinanceButton(card,"إعادة المحاولة بنفس البيانات",async m=>{financeOutbox.Mark(cmd.Id,"pending","");financeNextRetry=DateTime.MinValue;await SyncFinanceAsync();Notice(m,"راجع حالة الطلب بعد إعادة المحاولة.",true);});
                FinanceButton(card,"إلغاء المسودة المرفوضة لتصحيحها",m=>{financeOutbox.Mark(cmd.Id,"abandoned","ألغى المستخدم مسودة مالية مرفوضة للتصحيح");RenderFinanceQueue();return Task.CompletedTask;});
            }
        }
    }
    private static void FinanceTable(StackPanel parent,string[] headers,IEnumerable<string[]> data)
    {
        parent.Children.Clear();var table=new StackPanel{Spacing=1};var rows=data.ToList();
        Grid Row(string[] cells,bool header){var g=new Grid{ColumnDefinitions=new ColumnDefinitions(string.Join(",",headers.Select(_=>"160"))),Background=header?new SolidColorBrush(Color.Parse("#DCE7F5")):new SolidColorBrush(Color.Parse("#F4F6F9")),MinHeight=38};for(var i=0;i<cells.Length;i++){var text=new TextBlock{Text=cells[i],Margin=new Thickness(7),TextWrapping=TextWrapping.Wrap,Foreground=Brushes.Black,FontWeight=header?FontWeight.SemiBold:FontWeight.Normal,HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Right};Grid.SetColumn(text,i);g.Children.Add(text);}return g;}
        table.Children.Add(Row(headers,true));foreach(var row in rows)table.Children.Add(Row(row,false));if(rows.Count==0)table.Children.Add(ExecText("لا توجد سجلات مطابقة بعد."));parent.Children.Add(new ScrollViewer{Content=table,HorizontalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,VerticalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,MaxHeight=460});
    }
    private async Task ExportFinanceCsvAsync(TextBlock message)
    {
        var file=await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions{Title="تصدير سجل المستندات",SuggestedFileName="Finance_Documents.csv",FileTypeChoices=[new FilePickerFileType("CSV"){Patterns=["*.csv"]}]});if(file is null)return;
        static string Quote(string text)=>"\""+((text.Length>0&&"=+-@\t\r".Contains(text[0]))?"'":"")+text.Replace("\"","\"\"")+"\"";
        var rows=new List<string[]>{new[]{"رقم داخلي","التاريخ","النوع","المرجع","الطرف","الصافي","الضريبة","التوصيل","الإجمالي","تكلفة المباع"}};
        rows.AddRange(Rows("documents").Where(d=>FinanceMatch(J(d,"reference"),PartyLabel(d),Label(J(d,"kind")))).Select(d=>new[]{J(d,"serial"),J(d,"posting_date"),Label(J(d,"kind")),J(d,"reference"),PartyLabel(d),J(d,"net"),J(d,"tax"),J(d,"delivery_charge"),J(d,"total"),J(d,"cogs")}));
        await using var stream=await file.OpenWriteAsync();using var writer=new StreamWriter(stream,new UTF8Encoding(true));foreach(var row in rows)await writer.WriteLineAsync(string.Join(",",row.Select(Quote)));Notice(message,"تم تصدير المستندات المعروضة. يحتوي الملف أرقاماً داخلية وليست فواتير ضريبية.",true);
    }
    private async Task ExportFinancePdfAsync(TextBlock message)
    {
        if(!financeOnline)throw new InvalidOperationException("حدث البيانات من الخادم قبل طباعة مستند مالي.");
        var id=Required(financePrintDocument);var d=Rows("documents").First(d=>J(d,"id")==id);
        var ids=J(d,"kind")=="invoice"?Rows("invoiceSales").Where(l=>J(l,"invoice_id")==id).Select(l=>J(l,"sale_id")).ToHashSet():new HashSet<string>{id};
        var lines=Rows("lines").Where(l=>ids.Contains(J(l,"document_id"))).Select(l=>new[]{J(l,"reference")+" | "+J(l,"item_code")+" — "+J(l,"item_name"),J(l,"quantity"),J(l,"unit_price"),J(l,"discount"),J(l,"tax"),J(l,"total")}).ToList();
        var bytes=Documents.FinanceDocumentPdf.Generate(Label(J(d,"kind")),J(d,"reference"),J(d,"serial"),J(d,"posting_date"),PartyLabel(d),J(d,"net"),J(d,"tax"),J(d,"delivery_charge"),J(d,"total"),J(d,"notes"),lines);
        var file=await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions{Title="حفظ مستند داخلي",SuggestedFileName="Finance_"+J(d,"serial")+".pdf",FileTypeChoices=[new FilePickerFileType("PDF"){Patterns=["*.pdf"]}]});if(file is null)return;
        await using var stream=await file.OpenWriteAsync();await stream.WriteAsync(bytes);Notice(message,"تم حفظ المستند الداخلي PDF.",true);
    }
    private static string InventoryMovementLabel(string? kind)=>kind switch{"production_issue"=>"صرف للإنتاج","production_return"=>"رد من الإنتاج","sale_issue"=>"صرف بيع","sales_return"=>"مرتجع بيع","finished_purchase"=>"استلام منتج تام مشترى","receipt"=>"استلام خامات",_=>kind??"حركة"};
    private sealed record FinanceCartLine(string Key,string Label,Dictionary<string,object?> Values);
    private sealed record FinanceDraft(string Kind,string[] Text,string?[] Choices,List<FinanceCartLine> Lines,List<string> SaleIds,int Category,int ReturnType);
    private sealed class FinanceForm(string kind,StackPanel panel)
    {
        public string Kind=kind;public StackPanel Panel=panel,CartPanel=new(){Spacing=5};
        public TextBox Reference=ExecInput("مرجع المستند"),Date=new(){Text=DateTime.Today.ToString("yyyy-MM-dd")},Amount=ExecInput("المبلغ"),DeliveryCharge=new(){Text="0"},Notes=ExecInput("الوصف / السبب"),Quantity=ExecInput("العدد"),Price=ExecInput("سعر الوحدة"),Discount=new(){Text="0"};
        public ProductionPicker Party=new("ابحث بالكود أو الاسم"),Account=new("الحساب"),TargetAccount=new("الحساب المستلم"),Source=new("المصدر الأصلي"),Item=new("المنتج التام"),Warehouse=new("المخزن"),Tax=new("الضريبة المعتمدة"),OriginalLine=new("البند الأصلي"),Order=new("الطلب — اختياري"),Obligation=new("الدين"),Settlement=new("الدفعة");
        public ComboBox Category=new(){ItemsSource=new[]{new ExecChoice("industrial","مصروف صناعي"),new ExecChoice("administrative","مصروف إداري"),new ExecChoice("customer_delivery","نقل / توصيل فعلي")},SelectedIndex=0},ReturnType=new(){ItemsSource=new[]{new ExecChoice("return","مرتجع كمية سبق تسليمها"),new ExecChoice("cancel","إلغاء كمية لم تسلم")},SelectedIndex=0};
        public List<FinanceCartLine> Lines=[];public List<string> SaleIds=[];
    }
}
