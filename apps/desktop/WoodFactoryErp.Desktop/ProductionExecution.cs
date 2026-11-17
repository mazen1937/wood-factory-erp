using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;

namespace WoodFactoryErp.Desktop;

public partial class MainWindow
{
    private ScrollViewer? executionPanel;
    private StackPanel executionRoot=null!,executionStages=null!,executionRoute=null!,wageUnitFields=null!,packagingPanel=null!;
    private TextBlock executionMessage=null!,executionSummary=null!,executionTotals=null!,executionHistory=null!,executionCosts=null!,executionMaterials=null!,executionWages=null!,qualitySummary=null!,qualityHistory=null!,packagingSummary=null!,progressHint=null!,wagePeriodHint=null!,queueText=null!;
    private ProductionPicker executionJobs=null!,qualityJobs=null!,progressStage=null!,progressWorker=null!,wageStage=null!,wageDue=null!,wasteIssue=null!,queueChoice=null!;
    private TextBox executionDate=null!,progressQuantity=null!,progressNotes=null!,pauseReason=null!,packagingAmount=null!,packagingNotes=null!,wageName=null!,wageRate=null!,wageQuantity=null!,wageFrom=null!,wageNotes=null!,qualityQuantity=null!,qualityNotes=null!,wasteQuantity=null!,wasteNotes=null!,executionMonth=null!;
    private ComboBox wageBasis=null!,wageTreatment=null!;
    private CheckBox qualityReinspection=null!,wageCompleted=null!;
    private Expander queuePanel=null!;
    private Button retryQueue=null!,discardQueue=null!;
    private MaterialIssueForm woodIssue=null!,accessoryIssue=null!;
    private MaterialReturnForm woodReturn=null!,accessoryReturn=null!;
    private readonly List<(CheckBox Box,string Code)> executionRouteChoices=[];
    private readonly List<(Button Button,string Action)> executionButtons=[];
    private List<ExecJob> executionJobRows=[];
    private ProductionOutbox? executionOutbox;
    private DispatcherTimer? executionTimer;
    private bool executionLoading,executionSyncing,executionOnline;
    private int executionDetailVersion,executionFailures;
    private DateTime executionNextRetry=DateTime.MinValue;
    private DateTime? executionLastSync;
    private ExecDetail? executionDetail;
    private string? renderedJobId;
    private string? ExecutionJobId=>executionJobs?.Value;
    private string ExecutionUserId()
    {
        var encoded=accessToken!.Split('.')[0].Replace('-','+').Replace('_','/');
        using var token=JsonDocument.Parse(Convert.FromBase64String(encoded.PadRight((encoded.Length+3)/4*4,'=')));
        return token.RootElement.GetProperty("sub").GetString()!;
    }
    private void HideProductionExecution(){if(executionPanel is not null)executionPanel.IsVisible=false;}
    private static TextBlock ExecText(string text,int size=14)=>new(){Text=text,FontSize=size,TextWrapping=TextWrapping.Wrap,HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Right};
    private static TextBox ExecInput(string hint)=>new(){PlaceholderText=hint,HorizontalContentAlignment=Avalonia.Layout.HorizontalAlignment.Right};
    private static void Field(StackPanel panel,string label,Control control){panel.Children.Add(ExecText(label));panel.Children.Add(control);}
    private static StackPanel Card(StackPanel parent,string title,string? explanation=null)
    {
        var content=new StackPanel{Spacing=9};content.Children.Add(ExecText(title,19));
        if(explanation is not null)content.Children.Add(ExecText(explanation));
        parent.Children.Add(new Border{Padding=new Thickness(14),Margin=new Thickness(0,5),CornerRadius=new CornerRadius(8),BorderThickness=new Thickness(1),BorderBrush=new SolidColorBrush(Color.Parse("#94A3B8")),Child=content});return content;
    }
    private Button ActionButton(StackPanel panel,string title,string action,Func<TextBlock,Task> callback)
    {
        var button=new Button{Content=title,MinWidth=180,HorizontalAlignment=Avalonia.Layout.HorizontalAlignment.Right};
        var feedback=ExecText("");panel.Children.Add(button);panel.Children.Add(feedback);
        button.Click+=async(_,_)=>{
            button.IsEnabled=false;feedback.Foreground=Brushes.DarkOrange;feedback.Text="جارٍ تنفيذ الطلب…";
            try{await callback(feedback);}catch(Exception ex){feedback.Foreground=Brushes.IndianRed;feedback.Text=FriendlyError(ex);}
            finally{button.IsEnabled=true;UpdateExecutionButtons();}
        };
        if(action!="")executionButtons.Add((button,action));return button;
    }
    private static void Notice(TextBlock label,string message,bool success){label.Text=message;label.Foreground=success?Brushes.ForestGreen:Brushes.IndianRed;}
    private void BuildProductionExecution()
    {
        executionRoot=new StackPanel{Spacing=12,Margin=new Thickness(8)};
        executionPanel=new ScrollViewer{Content=executionRoot,VerticalScrollBarVisibility=Avalonia.Controls.Primitives.ScrollBarVisibility.Auto,IsVisible=false};
        ((Grid)OrdersPanel.Parent!).Children.Add(executionPanel);
        executionRoot.Children.Add(ExecText("الإنتاج — متابعة الشغل الجاري",28));
        executionMessage=ExecText("");executionRoot.Children.Add(executionMessage);
        executionTotals=ExecText("",17);executionRoot.Children.Add(executionTotals);
        ActionButton(executionRoot,"تحديث البيانات","",async f=>{executionNextRetry=DateTime.MinValue;await SyncProductionExecutionAsync();await RefreshProductionExecutionAsync();Notice(f,executionOnline?"تم تحديث البيانات.":"الاتصال غير متاح؛ المعروض آخر بيانات محفوظة على الجهاز.",executionOnline);});
        executionJobs=new ProductionPicker("ابحث برقم الطلب أو المنتج أو اسم العميل");Field(executionRoot,"١. اختر أمر الشغل",executionJobs);
        executionJobs.Changed+=async()=>{if(executionLoading)return;qualityJobs.Select(executionJobs.Value);await LoadExecutionDetailAsync();};
        executionSummary=ExecText("",17);executionRoot.Children.Add(executionSummary);
        executionDate=ExecInput("YYYY-MM-DD");executionDate.Text=DateTime.Today.ToString("yyyy-MM-dd");Field(executionRoot,"تاريخ تسجيل الحركات",executionDate);
        var tabs=new TabControl();executionRoot.Children.Add(tabs);

        var progress=new StackPanel{Spacing=10,Margin=new Thickness(8)};
        var start=Card(progress,"بدء الشغل","اختر المراحل المطلوبة مرة واحدة، ثم ابدأ. بعد ذلك سجل عدد الوحدات التي تنتهي من كل مرحلة.");
        executionRoute=new StackPanel{Spacing=4};start.Children.Add(executionRoute);
        ActionButton(start,"بدء الإنتاج","start",async f=>{await PostFormAsync("start",new(){["stages"]=executionRouteChoices.Where(x=>x.Box.IsChecked==true).Select(x=>x.Code).ToArray()},f);});
        executionStages=new StackPanel{Spacing=6};progress.Children.Add(executionStages);
        var stage=Card(progress,"تسجيل ما تم إنجازه","مثال: انتهيت الآن من تقطيع وحدتين؛ اختر التقطيع وأدخل ٢. لا تكتب إجمالي ما سبق تسجيله.");
        progressStage=new ProductionPicker("اختر المرحلة");Field(stage,"المرحلة",progressStage);
        progressStage.Changed+=()=>{RefreshProgressWorker();return Task.CompletedTask;};
        progressHint=ExecText("");stage.Children.Add(progressHint);
        progressQuantity=ExecInput("عدد الوحدات التي أنهيتها الآن");Field(stage,"العدد المنجز الآن",progressQuantity);
        progressWorker=new ProductionPicker("العامل صاحب أجر الوحدة، إن وجد");Field(stage,"العامل (لإضافة أجره تلقائياً)",progressWorker);
        progressNotes=ExecInput("ملاحظة عن الإنجاز — اختياري");Field(stage,"ملاحظة",progressNotes);
        ActionButton(stage,"حفظ إنجاز المرحلة","progress",async f=>{var data=new Dictionary<string,object?>{["stageId"]=Required(progressStage),["quantity"]=Positive(progressQuantity.Text),["notes"]=progressNotes.Text?.Trim()};if(progressWorker.Value is not null)data["wageAgreementId"]=progressWorker.Value;if(await PostFormAsync("progress",data,f))progressQuantity.Text="";});
        var pause=Card(progress,"إيقاف واستئناف الشغل");pauseReason=ExecInput("مثال: انتظار خامات / عودة الخامات");Field(pause,"السبب",pauseReason);
        ActionButton(pause,"إيقاف مؤقت","hold",f=>PostReasonAsync("hold",pauseReason,f));
        ActionButton(pause,"استئناف الإنتاج","resume",f=>PostReasonAsync("resume",pauseReason,f));
        packagingPanel=Card(progress,"تكلفة التعبئة والتغليف","أدخل تكلفة التعبئة والتغليف للطلب كله مرة واحدة. تضاف تلقائياً إلى تكلفة الشغل الجاري؛ هذا ليس تسجيل دفع نقدي.");
        packagingSummary=ExecText("");packagingPanel.Children.Add(packagingSummary);
        packagingAmount=ExecInput("إجمالي تكلفة التغليف لهذا الطلب");Field(packagingPanel,"السعر الإجمالي للطلب، وليس سعر الوحدة",packagingAmount);
        packagingNotes=ExecInput("وصف التغليف — اختياري");Field(packagingPanel,"الوصف",packagingNotes);
        ActionButton(packagingPanel,"حفظ تكلفة التعبئة والتغليف","packaging_cost",async f=>{if(await PostFormAsync("packaging_cost",new(){["amount"]=Positive(packagingAmount.Text),["notes"]=packagingNotes.Text?.Trim()},f))packagingAmount.Text="";});

        var materials=new StackPanel{Spacing=10,Margin=new Thickness(8)};
        materials.Children.Add(ExecText("عند حفظ الصرف تُخصم الكمية من المخزن وتنتقل قيمتها تلقائياً إلى تكلفة الطلب تحت الإنتاج. الرد يعيد غير المستخدم إلى مخزنه الأصلي ويخفض تكلفة الطلب."));
        woodIssue=BuildIssueCard(materials,"صرف الخشب","raw_material");accessoryIssue=BuildIssueCard(materials,"صرف الإكسسوارات","accessory");
        woodReturn=BuildReturnCard(materials,"رد خشب غير مستخدم","raw_material");accessoryReturn=BuildReturnCard(materials,"رد إكسسوارات غير مستخدمة","accessory");
        var wasteContent=new StackPanel{Spacing=8};wasteIssue=new ProductionPicker("ابحث في الخامات التي سبق صرفها");Field(wasteContent,"الخامة المصروفة",wasteIssue);
        wasteQuantity=ExecInput("عدد / كمية الهالك");Field(wasteContent,"كمية الهالك الفعلي",wasteQuantity);
        wasteNotes=ExecInput("سبب الهالك");Field(wasteContent,"السبب",wasteNotes);
        ActionButton(wasteContent,"حفظ الهالك الفعلي","waste",async f=>{if(await PostFormAsync("waste",new(){["originalIssueId"]=Required(wasteIssue),["quantity"]=Positive(wasteQuantity.Text),["notes"]=Reason(wasteNotes)},f))wasteQuantity.Text="";});
        materials.Children.Add(new Expander{Header="تسجيل هالك فعلي من خامة مصروفة",Content=wasteContent});
        executionMaterials=ExecText("لا توجد حركات صرف أو رد محفوظة لهذا الطلب.");materials.Children.Add(executionMaterials);

        var wages=new StackPanel{Spacing=10,Margin=new Thickness(8)};
        var wageForm=Card(wages,"تسجيل أجر عامل لهذا الطلب","اعتماد الأجر يحفظ اتفاق العامل. أجر الوحدة يُستحق عند حفظ إنجازه؛ أجر اليوم أو الأسبوع أو الشهر يُستحق بعد انتهاء الفترة. تسجيل الأجر هنا لا يعني أنه دُفع.");
        wageName=ExecInput("اسم العامل");Field(wageForm,"اسم العامل",wageName);
        wageStage=new ProductionPicker("اختر مرحلة العامل");Field(wageForm,"المرحلة",wageStage);
        wageBasis=new ComboBox{ItemsSource=new[]{new ExecChoice("unit","لكل وحدة منجزة"),new ExecChoice("daily","يومي"),new ExecChoice("weekly","أسبوعي"),new ExecChoice("monthly","شهري")},SelectedIndex=0};Field(wageForm,"نوع الأجر",wageBasis);
        wageRate=ExecInput("سعر الوحدة / أجر اليوم أو الأسبوع أو الشهر");Field(wageForm,"السعر حسب نوع الأجر المختار",wageRate);
        wageUnitFields=new StackPanel{Spacing=5};wageQuantity=ExecInput("عدد الوحدات للعامل");Field(wageUnitFields,"عدد الوحدات المكلف بها العامل / التي أنجزها",wageQuantity);wageCompleted=new CheckBox{Content="هذه الوحدات منجزة بالفعل ولم يسجل أجرها بعد"};wageUnitFields.Children.Add(wageCompleted);wageForm.Children.Add(wageUnitFields);
        wageTreatment=new ComboBox{ItemsSource=new[]{new ExecChoice("direct","أجر مباشر على هذا الطلب"),new ExecChoice("pooled","أجر مجمع للتوزيع الشهري")},SelectedIndex=0};Field(wageForm,"تحميل تكلفة الأجر",wageTreatment);
        wageForm.Children.Add(ExecText("المباشر يدخل تكلفة هذا الطلب عند الاستحقاق. المجمع يسجل منفصلاً ليوزع لاحقاً؛ لا يُحسب مرتين."));
        wageFrom=ExecInput("YYYY-MM-DD");wageFrom.Text=DateTime.Today.ToString("yyyy-MM-dd");Field(wageForm,"يبدأ من / بداية فترة الأجر",wageFrom);
        wagePeriodHint=ExecText("");wageForm.Children.Add(wagePeriodHint);
        wageBasis.SelectionChanged+=(_,_)=>RefreshWagePeriod();wageFrom.TextChanged+=(_,_)=>RefreshWagePeriod();RefreshWagePeriod();
        wageNotes=ExecInput("ملاحظات الاتفاق — اختياري");Field(wageForm,"ملاحظات",wageNotes);
        ActionButton(wageForm,"اعتماد أجر العامل","wage_agreement",async f=>{var basis=EnumValue(wageBasis);var data=new Dictionary<string,object?>{["workerName"]=Nonempty(wageName.Text,"اكتب اسم العامل."),["stageId"]=Required(wageStage),["wageBasis"]=basis,["rate"]=Positive(wageRate.Text),["treatment"]=EnumValue(wageTreatment),["periodStart"]=ValidDate(wageFrom.Text),["notes"]=wageNotes.Text?.Trim()};if(basis=="unit"){data["quantity"]=Positive(wageQuantity.Text);data["workAlreadyCompleted"]=wageCompleted.IsChecked==true;}await PostFormAsync("wage_agreement",data,f);});
        executionWages=ExecText("لا توجد أجور محفوظة لهذا الطلب.");wages.Children.Add(executionWages);
        var due=Card(wages,"استحقاق أجر يومي / أسبوعي / شهري","الاتفاق الذي انتهت فترته ولم يُرحّل أجره يظهر هنا. اعتماده يضيف الاستحقاق مرة واحدة؛ صرف المال للعامل يكون من المالية لاحقاً.");
        wageDue=new ProductionPicker("اختر فترة أجر انتهت");Field(due,"الأجر المستحق",wageDue);
        ActionButton(due,"تسجيل استحقاق الفترة","wage_due",async f=>{await PostFormAsync("wage_due",new(){["wageAgreementId"]=Required(wageDue)},f);});

        var quality=new StackPanel{Spacing=10,Margin=new Thickness(8)};
        qualityJobs=new ProductionPicker("ابحث عن الطلب الذي تريد فحصه");Field(quality,"طلب الفحص",qualityJobs);
        qualityJobs.Changed+=async()=>{if(executionLoading)return;executionJobs.Select(qualityJobs.Value);await LoadExecutionDetailAsync();};
        qualitySummary=ExecText("",17);quality.Children.Add(qualitySummary);
        var inspection=Card(quality,"قرار الفحص","حدد عدد الوحدات التي فحصتها، ثم اختر قبول أو رفض. عند إصلاح المرفوض اختر إعادة فحص؛ لا تسجله كفحص جديد.");
        qualityQuantity=ExecInput("عدد الوحدات المفحوصة الآن");Field(inspection,"العدد",qualityQuantity);
        qualityReinspection=new CheckBox{Content="إعادة فحص وحدات سبق رفضها بعد إصلاحها"};inspection.Children.Add(qualityReinspection);
        qualityNotes=ExecInput("سبب الرفض أو ملاحظة الفحص");Field(inspection,"سبب الرفض / ملاحظة",qualityNotes);
        ActionButton(inspection,"قبول العدد المفحوص","quality_check",f=>SubmitQualityAsync("accepted",f));
        ActionButton(inspection,"رفض العدد — يحتاج إصلاحاً","quality_check",f=>SubmitQualityAsync("rejected",f));
        quality.Children.Add(ExecText("المقبول ينتظر اعتماد التكلفة والترحيل إلى المنتج التام. المرفوض يبقى تحت الإنتاج للإصلاح وإعادة الفحص."));
        qualityHistory=ExecText("لم يسجل فحص لهذا الطلب بعد.");quality.Children.Add(qualityHistory);

        var costs=new StackPanel{Spacing=10,Margin=new Thickness(8)};
        costs.Children.Add(ExecText("تكلفة الشغل تحت الإنتاج — عرض تلقائي",21));
        costs.Children.Add(ExecText("لا تحتاج لإدخال مبلغ هنا. تظهر قيمة الخشب والإكسسوارات المصروفة تلقائياً، ويُخصم المرتجع، وتضاف الأجور المستحقة والتغليف. الاتفاق مع العامل قبل الإنجاز ليس تكلفة مكتسبة بعد."));
        executionMonth=ExecInput("YYYY-MM");executionMonth.Text=DateTime.Today.ToString("yyyy-MM");Field(costs,"الشهر المعروض",executionMonth);
        ActionButton(costs,"عرض تكلفة الشهر","",async f=>{await LoadExecutionDetailAsync();Notice(f,"تم عرض التقرير.",true);});
        executionCosts=ExecText("");costs.Children.Add(executionCosts);
        costs.Children.Add(ExecText("هذه تكاليف مسجلة قبل التوزيع الشهري للمصاريف الصناعية والأجور المجمعة؛ التكلفة النهائية للمنتج تعتمد في مرحلة التكاليف."));
        executionHistory=ExecText("لا توجد حركات محفوظة بعد. بدء الإنتاج أو حفظ أي حركة سيظهر هنا بالتاريخ واسم المسجل.");
        tabs.ItemsSource=new[]{new TabItem{Header="مراحل الشغل الجاري",Content=progress},new TabItem{Header="صرف ورد الخامات",Content=materials},new TabItem{Header="الأجور",Content=wages},new TabItem{Header="الفحص والقبول",Content=quality},new TabItem{Header="تكلفة الشغل الجاري",Content=costs},new TabItem{Header="سجل الحركات",Content=executionHistory}};

        var pending=new StackPanel{Spacing=8};pending.Children.Add(ExecText("هذه طلبات حفظ لم يقبلها الخادم بعد. يظهر السبب بجانب كل طلب. إعادة المحاولة ترسل نفس الطلب؛ لتصحيحه يمكنك إلغاء الطلب المرفوض ثم إدخال بيانات صحيحة في النموذج."));
        queueText=ExecText("");pending.Children.Add(queueText);queueChoice=new ProductionPicker("اختر طلب الحفظ الذي ظهر له خطأ");pending.Children.Add(queueChoice);
        queueChoice.Changed+=()=>{UpdateQueueButtons();return Task.CompletedTask;};
        retryQueue=ActionButton(pending,"إعادة إرسال الطلب المختار","",async f=>{var id=Required(queueChoice);executionOutbox!.Mark(id,"pending","");executionNextRetry=DateTime.MinValue;await SyncProductionExecutionAsync();ShowCommandOutcome(id,f);});
        discardQueue=ActionButton(pending,"إلغاء طلب الحفظ المرفوض","",f=>{executionOutbox!.Mark(Required(queueChoice),"abandoned","ألغى المستخدم طلب الحفظ المرفوض لتصحيحه");RenderExecutionQueue();Notice(f,"أُلغي الطلب المحلي المرفوض. يمكنك إدخال البيانات الصحيحة وحفظها.",true);return Task.CompletedTask;});
        queuePanel=new Expander{Header="طلبات حفظ تحتاج مراجعة",Content=pending,IsVisible=false};executionRoot.Children.Add(queuePanel);
        Closed+=(_,_)=>{executionTimer?.Stop();executionOutbox?.Dispose();};
    }

    private MaterialIssueForm BuildIssueCard(StackPanel parent,string title,string itemType)
    {
        var panel=Card(parent,title);var form=new MaterialIssueForm(itemType,new("اكتب الكود أو الاسم للبحث"),ExecInput("العدد / الكمية"),new("ابحث عن المخزن"));
        Field(panel,itemType=="accessory"?"نوع الإكسسوار":"نوع الخشب",form.Item);Field(panel,"العدد بوحدة الصنف المختار",form.Quantity);Field(panel,"مخزن الصرف",form.Warehouse);
        ActionButton(panel,"صرف الكمية للإنتاج","issue",async f=>{if(await PostFormAsync("issue",new(){["itemId"]=Required(form.Item),["warehouseId"]=Required(form.Warehouse),["quantity"]=Positive(form.Quantity.Text),["notes"]=title},f))form.Quantity.Text="";});return form;
    }
    private MaterialReturnForm BuildReturnCard(StackPanel parent,string title,string type)
    {
        var panel=Card(parent,title,"تظهر هنا فقط الأصناف التي سبق صرفها لهذا الطلب ولها كمية قابلة للرد.");
        var form=new MaterialReturnForm(type,new("ابحث عن النوع المصروف"),new("اختر عملية الصرف الأصلية"),ExecInput("العدد المطلوب رده"),ExecInput("سبب الرد"),ExecText("اختر الصرف لعرض المخزن الأصلي والكمية المتاحة."));
        Field(panel,type=="accessory"?"نوع الإكسسوار":"نوع الخشب",form.Item);Field(panel,"عملية الصرف الأصلية",form.Issue);panel.Children.Add(form.Available);Field(panel,"عدد / كمية المرتجع",form.Quantity);Field(panel,"سبب الرد",form.Reason);
        form.Item.Changed+=()=>{RefreshReturnIssues(form);return Task.CompletedTask;};form.Issue.Changed+=()=>{RefreshReturnWarehouse(form);return Task.CompletedTask;};
        ActionButton(panel,"رد الكمية إلى المخزن الأصلي","return",async f=>{if(await PostFormAsync("return",new(){["originalIssueId"]=Required(form.Issue),["quantity"]=Positive(form.Quantity.Text),["notes"]=Reason(form.Reason)},f))form.Quantity.Text="";});return form;
    }
    private async void ShowProductionAsync(object? sender,RoutedEventArgs e)
    {
        try{
            HideFinance();
            if(executionPanel is null)BuildProductionExecution();executionOutbox??=new ProductionOutbox();
            HomePanel.IsVisible=OrdersPanel.IsVisible=InventoryPanel.IsVisible=MasterDataPanel.IsVisible=false;executionPanel!.IsVisible=true;
            await RefreshProductionExecutionAsync();await SyncProductionExecutionAsync();
            executionTimer??=new DispatcherTimer{Interval=TimeSpan.FromSeconds(10)};executionTimer.Tick-=ProductionSyncTick;executionTimer.Tick+=ProductionSyncTick;executionTimer.Start();
        }catch(Exception ex){if(executionMessage is not null)executionMessage.Text=FriendlyError(ex);}
    }
    private async void ProductionSyncTick(object? sender,EventArgs e){if(string.IsNullOrWhiteSpace(accessToken))return;try{await SyncProductionExecutionAsync();}catch(Exception ex){executionMessage.Text=FriendlyError(ex);}}
    private async Task<string> ExecutionGetAsync(string endpoint)
    {
        var key=ExecutionUserId()+":"+endpoint;
        try{using var request=new HttpRequestMessage(HttpMethod.Get,endpoint);request.Headers.Authorization=new("Bearer",accessToken);using var response=await HttpClient.SendAsync(request);await EnsureApiSuccessAsync(response);var json=await response.Content.ReadAsStringAsync();executionOutbox!.Cache(key,json);executionOnline=true;return json;}
        catch{executionOnline=false;var cache=executionOutbox!.Cached(key);if(cache is not null)return cache;throw;}
    }
    private async Task RefreshProductionExecutionAsync()
    {
        executionLoading=true;
        try{
            var selected=ExecutionJobId;var jobs=JsonSerializer.Deserialize<List<ExecJob>>(await ExecutionGetAsync("production"),JsonOptions)??[];
            var catalog=JsonSerializer.Deserialize<List<ExecCatalog>>(await ExecutionGetAsync("production/catalog"),JsonOptions)??[];
            currentMasterData=JsonSerializer.Deserialize<MasterDataResponse>(await ExecutionGetAsync("master-data"),JsonOptions);
            executionJobRows=jobs;var choices=jobs.Select(j=>new ExecChoice(j.Id,$"{j.OrderNo} | {j.ProductCode} — {j.ProductName} | {j.CustomerNameAr} | {ExecutionStatus(j.Status)}")).ToList();
            executionJobs.Set(choices,selected,true);qualityJobs.Set(choices,executionJobs.Value,true);
            executionTotals.Text=$"تحت الإنتاج: {jobs.Count(j=>j.Status=="in_progress")} | متوقف مؤقتاً: {jobs.Count(j=>j.Status=="on_hold")} | مقبول بانتظار التكلفة: {jobs.Count(j=>j.Status=="ready_for_costing")}";
            if(executionRouteChoices.Count==0)foreach(var s in catalog.Where(c=>ActiveRoute.Contains(c.Code))){var check=new CheckBox{Content=StageLabel(s.Code,s.NameAr),IsChecked=true,IsEnabled=s.Code!="quality_control"};executionRoute.Children.Add(check);executionRouteChoices.Add((check,s.Code));}
            foreach(var form in new[]{woodIssue,accessoryIssue}){
                form.Item.Set((currentMasterData?.Items??[]).Where(i=>i.StockTracked&&i.Classification=="materials"&&i.ItemType==form.ItemType).Select(i=>new ExecChoice(i.Id,$"{i.Code} — {i.NameAr} ({currentMasterData?.Units.FirstOrDefault(u=>u.Id==i.BaseUnitId)?.Code})")));
                form.Warehouse.Set((currentMasterData?.Warehouses??[]).Where(w=>w.WarehouseType=="raw_material").Select(w=>new ExecChoice(w.Id,$"{w.Code} — {w.NameAr}")),firstIfSingle:true);
            }
        }catch(Exception ex){executionMessage.Text=FriendlyError(ex);throw;}
        finally{executionLoading=false;}
        await LoadExecutionDetailAsync();RenderExecutionQueue();
    }
    private async Task LoadExecutionDetailAsync()
    {
        var id=ExecutionJobId;var version=++executionDetailVersion;
        executionDetail=null;UpdateExecutionButtons();executionSummary.Text="جارٍ تحميل الطلب المحدد…";
        executionStages.Children.Clear();executionMaterials.Text=executionWages.Text=executionCosts.Text=executionHistory.Text=qualityHistory.Text=qualitySummary.Text="";
        if(id is null){executionSummary.Text="اختر أمر الشغل من نتائج البحث. إذا لم يوجد أمر شغل، أنشئه من الطلبات أولاً.";return;}
        try{
            var month=executionMonth.Text?.Trim();if(!System.Text.RegularExpressions.Regex.IsMatch(month??"",@"^\d{4}-(0[1-9]|1[0-2])$"))throw new InvalidOperationException("الشهر بصيغة YYYY-MM.");
            var detail=JsonSerializer.Deserialize<ExecDetail>(await ExecutionGetAsync($"production/{id}?month={month}"),JsonOptions)??throw new InvalidOperationException("بيانات أمر الشغل فارغة.");
            if(version!=executionDetailVersion)return;
            executionDetail=detail;var job=executionJobRows.First(j=>j.Id==id);
            if(renderedJobId!=id){foreach(var q in new[]{progressQuantity,woodIssue.Quantity,accessoryIssue.Quantity,woodReturn.Quantity,accessoryReturn.Quantity,qualityQuantity,packagingAmount})q.Text="";renderedJobId=id;}
            executionSummary.Text=$"الطلب: {job.OrderNo} | المنتج: {job.ProductName} | العميل: {job.CustomerNameAr??"إنتاج مخزني"}\nالحالة: {ExecutionStatus(JsonValue(detail.Order,"status"))} | المخطط: {job.PlannedQuantity} وحدة | المقبول: {detail.Acceptances.Sum(a=>Number(a.Quantity))}";
            executionStages.Children.Clear();foreach(var s in detail.Stages.Where(s=>ActiveRoute.Contains(s.Code)))executionStages.Children.Add(ExecText($"{StageLabel(s.Code,s.NameAr)}: {s.CompletedQuantity} / {job.PlannedQuantity} وحدة",16));
            progressStage.Set(detail.Stages.Where(s=>ActiveRoute.Contains(s.Code)&&s.Code!="quality_control").Select(s=>new ExecChoice(s.Id,StageLabel(s.Code,s.NameAr))),first:true);
            wageStage.Set(detail.Stages.Where(s=>ActiveRoute.Contains(s.Code)).Select(s=>new ExecChoice(s.Id,StageLabel(s.Code,s.NameAr))),first:true);
            RefreshProgressWorker();RefreshReturnForm(woodReturn);RefreshReturnForm(accessoryReturn);
            wasteIssue.Set(detail.Materials.Where(m=>m.OriginalIssueId is null&&Number(m.ReturnableQuantity)>0).Select(m=>new ExecChoice(m.Id,$"{m.ItemCode} — {m.ItemNameAr} | متاح: {m.ReturnableQuantity} {m.UnitCode}")));
            executionMaterials.Text=detail.Materials.Count==0?"لم يسجل صرف أو رد خامات لهذا الطلب بعد.":string.Join(Environment.NewLine,detail.Materials.Select(m=>$"{m.Date} | {(m.OriginalIssueId is null?"صرف":"مرتجع")} | {m.ItemNameAr} | {m.Quantity} {m.UnitCode} | {m.WarehouseNameAr} | القيمة: {m.Amount}"));
            packagingSummary.Text=detail.Packaging.Count==0?"لم تسجل تكلفة تغليف لهذا الطلب بعد.":$"تكلفة التغليف المسجلة: {detail.Packaging[0].Amount} — بتاريخ {detail.Packaging[0].Date}";
            RenderWages(detail);RenderQuality(detail);RenderWip(detail);
            executionHistory.Text=detail.Events.Count==0?"لا توجد حركات محفوظة لهذا الطلب بعد. ستظهر الحركة هنا بعد حفظها بنجاح.":string.Join("\n\n",detail.Events.Select(ev=>$"{ev.Date} | {ExecutionAction(ev.Action)} | سجّلها: {ev.Actor}"+(JsonValue(ev.Payload,"quantity")==""?"":$" | العدد: {JsonValue(ev.Payload,"quantity")}")+(JsonValue(ev.Payload,"workerName")==""?"":$" | العامل: {JsonValue(ev.Payload,"workerName")}")+(string.IsNullOrWhiteSpace(ev.Notes)?"":$"\n{ev.Notes}")));
            UpdateExecutionButtons();
        }catch(Exception ex){if(version==executionDetailVersion){executionDetail=null;executionMessage.Text=FriendlyError(ex);UpdateExecutionButtons();}throw;}
    }
    private void RefreshProgressWorker()
    {
        if(executionDetail is null)return;var stage=executionDetail.Stages.FirstOrDefault(s=>s.Id==progressStage.Value);var job=executionJobRows.FirstOrDefault(j=>j.Id==ExecutionJobId);
        progressHint.Text=stage is null?"اختر المرحلة لعرض الكمية المنجزة.":$"المنجز المسجل: {stage.CompletedQuantity} من {job?.PlannedQuantity}. أدخل فقط ما انتهى الآن.";
        progressWorker.Set(executionDetail.Wages.Where(w=>w.Basis=="unit"&&w.StageId==stage?.Id&&Number(w.AccruedQuantity)<Number(w.AssignedQuantity)).Select(w=>new ExecChoice(w.Id,$"{w.WorkerName} | المتبقي له: {Number(w.AssignedQuantity)-Number(w.AccruedQuantity)} وحدة | السعر: {w.Rate}")),firstIfSingle:true);
    }
    private void RefreshReturnForm(MaterialReturnForm form)
    {
        if(executionDetail is null)return;
        var choices=executionDetail.Materials.Where(m=>m.OriginalIssueId is null&&m.ItemType==form.ItemType&&Number(m.ReturnableQuantity)>0).GroupBy(m=>m.ItemId).Select(g=>new ExecChoice(g.Key,$"{g.First().ItemCode} — {g.First().ItemNameAr}"));
        form.Item.Set(choices);RefreshReturnIssues(form);
    }
    private void RefreshReturnIssues(MaterialReturnForm form)
    {
        var available=executionDetail?.Materials.Where(m=>m.OriginalIssueId is null&&m.ItemType==form.ItemType&&Number(m.ReturnableQuantity)>0).ToList()??[];
        form.Issue.Set(available.Where(m=>m.ItemId==form.Item.Value).Select(m=>new ExecChoice(m.Id,$"{m.Date} | {m.WarehouseNameAr} | المتاح: {m.ReturnableQuantity} {m.UnitCode}")),firstIfSingle:true);
        if(available.Count==0)form.Available.Text="لا توجد كمية مصروفة قابلة للرد لهذا النوع في الطلب المحدد. احفظ الصرف أولاً، ثم تظهر الكمية هنا.";
        else RefreshReturnWarehouse(form);
    }
    private void RefreshReturnWarehouse(MaterialReturnForm form)
    {
        var issue=executionDetail?.Materials.FirstOrDefault(m=>m.Id==form.Issue.Value);
        form.Available.Text=issue is null?"اختر النوع ثم عملية الصرف الأصلية. ستعود الكمية إلى المخزن الذي خرجت منه.":$"المخزن الأصلي: {issue.WarehouseNameAr}\nالمتاح للرد: {issue.ReturnableQuantity} {issue.UnitCode}";
    }
    private void RefreshWagePeriod()
    {
        var basis=(wageBasis.SelectedItem as ExecChoice)?.Value??"unit";wageUnitFields.IsVisible=basis=="unit";
        if(!DateOnly.TryParseExact(wageFrom.Text,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var from)){wagePeriodHint.Text="أدخل تاريخ البداية بصيغة YYYY-MM-DD.";return;}
        var end=basis switch{"weekly"=>from.AddDays(6),"monthly"=>from.AddMonths(1).AddDays(-1),_=>from};
        wagePeriodHint.Text=basis=="unit"?"سيسجل الاتفاق لعدد الوحدات المكلف بها العامل، ويضاف أجرها تلقائياً عند تسجيل إنجازه من قسم المراحل.":$"فترة الأجر: {from:yyyy-MM-dd} إلى {end:yyyy-MM-dd}. يستحق المبلغ المحدد مرة واحدة عن هذه الفترة بعد انتهائها.";
    }
    private void RenderWages(ExecDetail detail)
    {
        executionWages.Text=detail.Wages.Count==0?"لا توجد اتفاقات أجور محفوظة لهذا الطلب.":string.Join("\n\n",detail.Wages.Select(w=>$"{w.WorkerName} | {w.StageName} | {WageBasisLabel(w.Basis)} | السعر: {w.Rate}\n"+(w.Basis=="unit"?$"المكلف به: {w.AssignedQuantity} وحدة | المنجز المستحق: {w.AccruedQuantity} | المتفق عليه: {Number(w.Rate)*Number(w.AssignedQuantity)}":$"الفترة: {w.PeriodStart} إلى {w.PeriodEnd}")+$"\nالمستحق المسجل: {w.EarnedAmount} — {(w.Treatment=="direct"?"مباشر على الطلب":"مجمع للتوزيع")} — لم يسجل دفع من هذه الشاشة"));
        var date=DateOnly.TryParseExact(executionDate.Text,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var parsed)?parsed.ToString("yyyy-MM-dd"):"";
        wageDue.Set(detail.Wages.Where(w=>w.Basis!="unit"&&Number(w.AccruedQuantity)==0&&string.CompareOrdinal(w.PeriodEnd,date)<=0).Select(w=>new ExecChoice(w.Id,$"{w.WorkerName} | {WageBasisLabel(w.Basis)} | حتى {w.PeriodEnd} | {w.Rate}")));
    }
    private void RenderQuality(ExecDetail detail)
    {
        var accepted=detail.Acceptances.Sum(a=>Number(a.Quantity));var rejected=detail.Quality.Where(q=>q.Decision=="rejected").Sum(q=>Number(q.Quantity))-detail.Quality.Where(q=>q.Reinspection).Sum(q=>Number(q.Quantity));
        var upstream=detail.Stages.LastOrDefault(s=>s.Code!="quality_control");var ready=Number(upstream?.CompletedQuantity)-accepted-rejected;
        qualitySummary.Text=$"الطلب: {executionJobRows.FirstOrDefault(j=>j.Id==ExecutionJobId)?.OrderNo}\nجاهز لفحص جديد: {Math.Max(0,ready)} | مقبول: {accepted} | مرفوض ينتظر إصلاحاً وإعادة فحص: {rejected}";
        qualityReinspection.IsEnabled=rejected>0;if(rejected<=0)qualityReinspection.IsChecked=false;
        qualityHistory.Text=detail.Quality.Count==0?"لم تسجل قرارات فحص لهذا الطلب بعد.":string.Join(Environment.NewLine,detail.Quality.Select(q=>$"{q.Date} | {(q.Reinspection?"إعادة فحص":"فحص جديد")} | {(q.Decision=="accepted"?"مقبول":"مرفوض")} | العدد: {q.Quantity} | الفاحص: {q.Inspector} | {q.Reason}"));
    }
    private void RenderWip(ExecDetail detail)
    {
        var w=detail.Wip;var direct=detail.Costs.Where(c=>c.Treatment=="direct"&&c.Date.StartsWith(w.Month)).ToList();
        decimal Sum(params string[] types)=>direct.Where(c=>types.Contains(c.Type)).Sum(c=>Number(c.Amount));
        executionCosts.Text=$"الشهر: {w.Month}\nقيمة الشغل المرحلة من الشهور السابقة: {w.OpeningDirectCost}\n\nخامات وإكسسوارات مصروفة ناقص المرتجع هذا الشهر: {Sum("material_issue","material_return"):N4}\nأجور مستحقة مباشرة هذا الشهر: {Sum("labor","wage_accrual"):N4}\nتعبئة وتغليف هذا الشهر: {Sum("packaging"):N4}\nتكاليف إنتاج أخرى سبق تسجيلها: {Sum("consumable"):N4}\n\nإجمالي الإضافات الصافية للشهر: {w.CurrentDirectCost}\nقيمة الشغل تحت الإنتاج حتى نهاية الشهر: {w.ClosingDirectCost}\nأجور مجمعة تنتظر التوزيع الشهري (خارج الإجمالي المباشر): {w.PooledWages}";
    }
    private async Task SubmitQualityAsync(string decision,TextBlock feedback)
    {
        var notes=decision=="rejected"?Reason(qualityNotes):qualityNotes.Text?.Trim();
        if(await PostFormAsync("quality_check",new(){["quantity"]=Positive(qualityQuantity.Text),["decision"]=decision,["reinspection"]=qualityReinspection.IsChecked==true,["notes"]=notes},feedback))qualityQuantity.Text="";
    }
    private async Task PostReasonAsync(string action,TextBox reason,TextBlock feedback){await PostFormAsync(action,new(){["notes"]=Reason(reason)},feedback);}
    private async Task<bool> PostFormAsync(string action,Dictionary<string,object?> values,TextBlock feedback)
    {
        var job=ExecutionJobId??throw new InvalidOperationException("اختر أمر الشغل أولاً.");var user=ExecutionUserId();
        values["action"]=action;values["workDate"]=ValidDate(executionDate.Text);
        var fingerprint=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(job+JsonSerializer.Serialize(values,JsonOptions))));
        var id=executionOutbox!.Enqueue(user,job,fingerprint,key=>{var payload=new Dictionary<string,object?>(values){["operationId"]=key};return JsonSerializer.Serialize(payload,JsonOptions);});
        // Pressing Save again explicitly retries the same rejected payload. A corrected
        // payload supersedes only rejected local copies of this form; official posts remain.
        foreach(var blocked in executionOutbox.Commands(user).Where(c=>c.JobId==job&&c.Status=="blocked")){
            using var json=JsonDocument.Parse(blocked.Payload);
            if(JsonValue(json.RootElement,"action")==action&&blocked.Id!=id)executionOutbox.Mark(blocked.Id,"abandoned","استبدل المستخدم طلب الحفظ المرفوض ببيانات مصححة");
        }
        if(executionOutbox.Command(user,id)?.Status=="blocked")executionOutbox.Mark(id,"pending","");
        executionNextRetry=DateTime.MinValue;await SyncProductionExecutionAsync();
        if(string.IsNullOrWhiteSpace(accessToken)||ExecutionUserId()!=user)throw new InvalidOperationException("تغيرت جلسة المستخدم؛ الطلب محفوظ لحسابه الأصلي.");
        return ShowCommandOutcome(id,feedback);
    }
    private bool ShowCommandOutcome(string id,TextBlock feedback)
    {
        var command=executionOutbox!.Command(ExecutionUserId(),id);
        if(command?.Status=="accepted"){
            using var result=JsonDocument.Parse(command.Detail);var amount=JsonValue(result.RootElement,"amount");var earned=JsonValue(result.RootElement,"earnedAmount");
            Notice(feedback,"تم الحفظ بنجاح وظهرت الحركة في سجل الحركات."+(amount==""?"":$" القيمة المضافة / المردودة: {amount}.")+(earned==""?"":$" أجر مستحق مسجل: {earned}."),true);return true;
        }
        if(command?.Status=="blocked")Notice(feedback,"لم يُحفظ الطلب: "+ExecutionError(command.Detail)+" صحح البيانات واضغط حفظ مرة أخرى.",false);
        else{feedback.Foreground=Brushes.DarkOrange;feedback.Text="لم يؤكد الخادم الحفظ بعد. الطلب محفوظ على جهازك وسيعاد إرساله عند عودة الاتصال؛ لم تتغير الكميات الرسمية بعد.";}
        RenderExecutionQueue();return false;
    }
    private async Task SyncProductionExecutionAsync()
    {
        if(executionSyncing||executionOutbox is null||string.IsNullOrWhiteSpace(accessToken)||DateTime.UtcNow<executionNextRetry)return;
        executionSyncing=true;var changed=false;
        try{
            var user=ExecutionUserId();
            foreach(var command in executionOutbox.Commands(user).Where(c=>c.Status=="pending")){
                using var request=new HttpRequestMessage(HttpMethod.Post,$"production/{command.JobId}/events"){Content=new StringContent(command.Payload,Encoding.UTF8,"application/json")};request.Headers.Authorization=new("Bearer",accessToken);
                try{
                    using var response=await HttpClient.SendAsync(request);var json=await response.Content.ReadAsStringAsync();
                    if(string.IsNullOrWhiteSpace(accessToken)||ExecutionUserId()!=user)return;
                    if(response.IsSuccessStatusCode){using var ack=JsonDocument.Parse(json);if(JsonValue(ack.RootElement,"operationId")!=command.Id)throw new HttpRequestException("تعذر تأكيد هوية الحفظ.");executionOutbox.Mark(command.Id,"accepted",json);executionOnline=true;executionLastSync=DateTime.Now;executionFailures=0;changed=true;}
                    else if((int)response.StatusCode>=400&&(int)response.StatusCode<500&&response.StatusCode!=HttpStatusCode.Unauthorized&&response.StatusCode!=HttpStatusCode.TooManyRequests){executionOutbox.Mark(command.Id,"blocked",json);executionOnline=true;}
                    else{BackoffExecution();break;}
                }catch(Exception ex)when(ex is HttpRequestException or TaskCanceledException or JsonException){BackoffExecution();break;}
            }
            if(changed)await RefreshProductionExecutionAsync();
        }finally{executionSyncing=false;RenderExecutionQueue();}
    }
    private void BackoffExecution(){executionOnline=false;executionNextRetry=DateTime.UtcNow.AddSeconds(Math.Min(300,10*Math.Pow(2,Math.Min(5,executionFailures++))));}
    private void RenderExecutionQueue()
    {
        if(executionOutbox is null||string.IsNullOrWhiteSpace(accessToken))return;var commands=executionOutbox.Commands(ExecutionUserId());
        executionMessage.Text=$"{(executionOnline?"متصل":"تعذر الاتصال — المعروض نسخة محفوظة")} | طلبات لم تحفظ بعد: {commands.Count(c=>c.Status=="pending")} | تحتاج تصحيحاً: {commands.Count(c=>c.Status=="blocked")} | آخر حفظ مؤكد: {executionLastSync?.ToString("HH:mm:ss")??"—"}";
        queuePanel.IsVisible=commands.Count>0;
        queueText.Text=string.Join("\n\n",commands.Select(c=>{using var json=JsonDocument.Parse(c.Payload);return $"{executionJobRows.FirstOrDefault(j=>j.Id==c.JobId)?.OrderNo??"أمر شغل"} | {ExecutionAction(JsonValue(json.RootElement,"action"))} | {(c.Status=="blocked"?"لم يحفظ: "+ExecutionError(c.Detail):"بانتظار الاتصال والتأكيد")}";}));
        queueChoice.Set(commands.Where(c=>c.Status=="blocked").Select(c=>new ExecChoice(c.Id,ExecutionError(c.Detail))));UpdateQueueButtons();
    }
    private void UpdateQueueButtons(){if(retryQueue is null)return;retryQueue.IsEnabled=discardQueue.IsEnabled=queueChoice.Value is not null;}
    private void UpdateExecutionButtons()
    {
        var state=executionDetail is null?"":JsonValue(executionDetail.Order,"status");
        foreach(var (button,action) in executionButtons)button.IsEnabled=action switch{
            "start"=>state is "draft" or "open","hold"=>state=="in_progress","resume"=>state=="on_hold",
            "quality_check"=>state is "in_progress" or "on_hold",
            "packaging_cost"=>(state is "in_progress" or "on_hold" or "ready_for_costing")&&executionDetail?.Packaging.Count==0,
            "return" or "waste" or "wage_agreement" or "wage_due"=>state is "in_progress" or "on_hold" or "ready_for_costing",
            _=>state=="in_progress"};
        if(executionRoute is not null)executionRoute.IsEnabled=state is "draft" or "open";UpdateQueueButtons();
    }
    private static readonly string[] ActiveRoute=["cutting","ashareet","assembly","packaging","quality_control"];
    private static string StageLabel(string code,string original)=>code=="assembly"?"التجميع وتركيب الإكسسوارات":original;
    private static string ValidDate(string? text)=>DateOnly.TryParseExact(text,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out var date)?date.ToString("yyyy-MM-dd"):throw new InvalidOperationException("أدخل تاريخاً صحيحاً بصيغة YYYY-MM-DD.");
    private static string Nonempty(string? text,string message)=>!string.IsNullOrWhiteSpace(text)?text.Trim():throw new InvalidOperationException(message);
    private static string Reason(TextBox field)=>Nonempty(field.Text,"اكتب السبب قبل الحفظ.");
    private static decimal Positive(string? text)=>decimal.TryParse(text,NumberStyles.Number,CultureInfo.InvariantCulture,out var value)&&value>0&&value<=999999999&&decimal.Round(value,4)==value?value:throw new InvalidOperationException("أدخل العدد / المبلغ أكبر من صفر (حتى ٤ منازل عشرية). استخدم النقطة للكسور.");
    private static decimal Number(string? value)=>decimal.TryParse(value,NumberStyles.Number,CultureInfo.InvariantCulture,out var number)?number:0;
    private static string Required(ProductionPicker picker)=>picker.Value??throw new InvalidOperationException("اختر قيمة من نتائج البحث أولاً.");
    private static string EnumValue(ComboBox combo)=>(combo.SelectedItem as ExecChoice)?.Value??throw new InvalidOperationException("اختر النوع.");
    private static string JsonValue(JsonElement root,string name)=>root.TryGetProperty(name,out var value)?value.ValueKind==JsonValueKind.String?value.GetString()??"":value.ValueKind==JsonValueKind.Null?"":value.ToString():"";
    private static string ExecutionError(string value){try{using var json=JsonDocument.Parse(value);return JsonValue(json.RootElement,"message");}catch{return value;}}
    private static string ExecutionStatus(string value)=>value switch{"draft" or "open"=>"لم يبدأ الإنتاج","in_progress"=>"تحت الإنتاج","on_hold"=>"متوقف مؤقتاً","ready_for_costing"=>"مقبول بانتظار التكلفة والترحيل","completed"=>"مكتمل",_=>value};
    private static string WageBasisLabel(string basis)=>basis switch{"unit"=>"لكل وحدة منجزة","daily"=>"يومي","weekly"=>"أسبوعي","monthly"=>"شهري",_=>basis};
    private static string ExecutionAction(string action)=>action switch{"start"=>"بدء الإنتاج","progress"=>"إنجاز مرحلة","issue" or "material_issue"=>"صرف خامات","return" or "material_return"=>"رد خامات","waste"=>"هالك فعلي","hold"=>"إيقاف مؤقت","resume"=>"استئناف","quality_check"=>"فحص وقبول / رفض","accept"=>"قبول","wage_agreement"=>"اتفاق أجر عامل","wage_due" or "wage_accrual"=>"استحقاق أجر","packaging_cost" or "packaging"=>"تكلفة تعبئة وتغليف","labor"=>"أجر مسجل","consumable"=>"تكلفة مستهلك",_=>action};
    private sealed class ProductionPicker:StackPanel
    {
        private readonly AutoCompleteBox box;private bool updating;private List<ExecChoice> choices=[];
        public event Func<Task>? Changed;
        public string? Value=>box.SelectedItem is ExecChoice c&&box.Text==c.Label?c.Value:null;
        public string Label=>box.Text??"";
        public ProductionPicker(string placeholder){var row=new Grid{ColumnDefinitions=new ColumnDefinitions("*,Auto"),ColumnSpacing=4};box=new AutoCompleteBox{MinimumPrefixLength=0,FilterMode=AutoCompleteFilterMode.Contains,PlaceholderText=placeholder,ValueMemberBinding=new Binding("Label")};row.Children.Add(box);var open=new Button{Content="▼",MinWidth=36};Grid.SetColumn(open,1);row.Children.Add(open);open.Click+=(_,_)=>{box.Focus();box.Text="";box.IsDropDownOpen=true;};Children.Add(row);box.SelectionChanged+=async(_,_)=>{if(!updating&&Changed is not null)try{await Changed();}catch{ /* Caller retains its visible load error. */ }};}
        public void Set(IEnumerable<ExecChoice> source,string? desired=null,bool first=false,bool firstIfSingle=false){desired??=Value;choices=source.ToList();updating=true;box.ItemsSource=choices;var choice=choices.FirstOrDefault(c=>c.Value==desired)??((first||firstIfSingle&&choices.Count==1)?choices.FirstOrDefault():null);box.SelectedItem=choice;box.Text=choice?.Label??"";updating=false;}
        public void Select(string? value){if(value is null){updating=true;box.SelectedItem=null;box.Text="";updating=false;}else Set(choices,value);}
    }
    private sealed record ExecChoice(string Value,string Label){public override string ToString()=>Label;}
    private sealed record MaterialIssueForm(string ItemType,ProductionPicker Item,TextBox Quantity,ProductionPicker Warehouse);
    private sealed record MaterialReturnForm(string ItemType,ProductionPicker Item,ProductionPicker Issue,TextBox Quantity,TextBox Reason,TextBlock Available);
    private sealed record ExecJob(string Id,string OrderNo,string ProductCode,string ProductName,string? CustomerNameAr,string Status,string PlannedQuantity);
    private sealed record ExecCatalog(string Code,string NameAr);
    private sealed record ExecStage(string Id,string Code,string NameAr,string CompletedQuantity);
    private sealed record ExecMaterial(string Id,string ItemId,string ItemType,string WarehouseId,string ItemCode,string ItemNameAr,string WarehouseNameAr,string UnitCode,string Quantity,string Amount,string? OriginalIssueId,string Date,string ReturnableQuantity);
    private sealed record ExecCost(string Type,string Amount,string Treatment,string Date,string? Description);
    private sealed record ExecEvent(string Action,string Date,string? Notes,string Actor,JsonElement Payload);
    private sealed record ExecAcceptance(string Quantity,string Date,string Status);
    private sealed record ExecWip(string Month,string OpeningDirectCost,string CurrentDirectCost,string ClosingDirectCost,string PooledWages);
    private sealed record ExecWage(string Id,string WorkerName,string StageId,string StageName,string Basis,string Rate,string? AssignedQuantity,string AccruedQuantity,string Treatment,string PeriodStart,string? PeriodEnd,string EarnedAmount);
    private sealed record ExecQuality(string Quantity,string Decision,bool Reinspection,string? Reason,string Date,string Inspector);
    private sealed record ExecPackaging(string Amount,string Date,string Description);
    private sealed record ExecDetail(JsonElement Order,List<ExecStage> Stages,List<ExecMaterial> Materials,List<ExecCost> Costs,List<ExecEvent> Events,List<ExecAcceptance> Acceptances,ExecWip Wip,List<ExecWage> Wages,List<ExecQuality> Quality,List<ExecPackaging> Packaging);
}
