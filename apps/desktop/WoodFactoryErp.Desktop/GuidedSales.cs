using System.Globalization;
using System.Net.Http.Json;
using System.Text.Json;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;

namespace WoodFactoryErp.Desktop;
public partial class MainWindow
{
    private ProductionPicker sellingCustomer=null!,sellingOrder=null!,sellingItem=null!,sellingWarehouse=null!,sellingTax=null!,sellingJob=null!,sellingPosted=null!,sellingAccount=null!;
    private TextBox sellingDate=null!,sellingReference=null!,sellingQuantity=null!,sellingPrice=null!,sellingDiscount=null!,sellingDelivery=null!,sellingNotes=null!,sellingCompletionRef=null!,sellingPaymentRef=null!,sellingPaymentAmount=null!,sellingDeliveryRef=null!,sellingDeliveryQuantity=null!;
    private TextBlock sellingPreview=null!,sellingReadiness=null!,sellingJobCost=null!,sellingPostedInfo=null!;
    private CheckBox sellingDeliverNow=null!,sellingApproveCost=null!,sellingAdvanced=null!;
    private readonly List<TabItem> allSalesFinanceTabs=[];
    private TabControl sellingTabs=null!;
    private bool sellingRestoring;
    private string? sellingDraftUser;
    private static string SalesReference(string prefix)=>prefix+"-"+DateTime.Today.ToString("yyyyMMdd")+"-"+Guid.NewGuid().ToString("N")[..10].ToUpperInvariant();
    private TextBox[] SellingInputs()=>[sellingDate,sellingReference,sellingQuantity,sellingPrice,sellingDiscount,sellingDelivery,sellingNotes,sellingCompletionRef,sellingPaymentRef,sellingPaymentAmount,sellingDeliveryRef,sellingDeliveryQuantity];
    private ProductionPicker[] SellingPickers()=>[sellingOrder,sellingItem,sellingWarehouse,sellingTax,sellingJob,sellingPosted,sellingAccount,sellingCustomer];
    private void SimplifySalesFinance(TabControl tabs,List<TabItem> original)
    {
        sellingTabs=tabs;var monthly=original[0];var guided=BuildGuidedSalesTab();var essentials=new StackPanel{Spacing=10};essentials.Children.Add(ExecText("سجّل المصروف أو الأجر المستحق أولاً، ثم سدده من الحساب المناسب. التحصيل والسداد لا يسجلان بيعاً أو تكلفة ثانية."));
        foreach(var kind in new[]{"expense","customer_receipt","supplier_payment","wage_payable"}){
            var f=financeForms.Single(x=>x.Kind==kind);var border=(Border)f.Panel.Parent!;var old=(Expander)border.Parent!;var parent=(StackPanel)old.Parent!;old.Content=null;parent.Children.Remove(old);
            var expanded=new Expander{Header=FinanceLabels[kind],Content=border,IsExpanded=kind=="expense"};expanded.PropertyChanged+=(_,e)=>{if(e.Property==Expander.IsExpandedProperty&&expanded.IsExpanded)foreach(var other in essentials.Children.OfType<Expander>().Where(x=>x!=expanded))other.IsExpanded=false;};essentials.Children.Add(expanded);
        }
        allSalesFinanceTabs.Clear();allSalesFinanceTabs.AddRange([guided,monthly,new TabItem{Header="مصاريف ودفعات",Content=essentials}]);allSalesFinanceTabs.AddRange(original.Skip(1));
        sellingAdvanced=new CheckBox{Content="إظهار الإجراءات المتقدمة والسجلات والإعدادات"};financeRoot.Children.Insert(financeRoot.Children.IndexOf(tabs),sellingAdvanced);
        sellingAdvanced.IsCheckedChanged+=(_,_)=>{var selected=tabs.SelectedItem;tabs.ItemsSource=sellingAdvanced.IsChecked==true?allSalesFinanceTabs:allSalesFinanceTabs.Take(3).ToList();if(selected is TabItem item&&((IEnumerable<TabItem>)tabs.ItemsSource).Contains(item))tabs.SelectedItem=item;};
        tabs.ItemsSource=allSalesFinanceTabs.Take(3).ToList();
    }
    private TabItem BuildGuidedSalesTab()
    {
        var root=new StackPanel{Spacing=10};root.Children.Add(ExecText("تسعير وبيع وتسليم",24));
        root.Children.Add(ExecText("السعر المحفوظ ليس بيعاً. تسجيل البيع يحسب الإيراد والضريبة والتكلفة والربح ويخصم المنتج من المخزون مرة واحدة. التحصيل يسجل المال الذي استلمته فعلاً."));
        sellingReadiness=ExecText("اعرض البيانات لبيان ما يلزم للبيع.",16);root.Children.Add(sellingReadiness);
        sellingCustomer=new("ابحث بكود العميل أو اسمه");Field(root,"العميل",sellingCustomer);sellingOrder=new("طلب مرتبط — اختياري");Field(root,"طلب العميل (اختياري)",sellingOrder);sellingDate=ExecInput("YYYY-MM-DD");sellingDate.Text=DateTime.Today.ToString("yyyy-MM-dd");Field(root,"تاريخ البيع / التسجيل",sellingDate);
        sellingItem=new("الصنف النهائي المطابق للمنتج");sellingWarehouse=new("مخزن المنتجات التامة");
        var product=Card(root,"1. المنتج الذي ستبيعه");Field(product,"الصنف النهائي",sellingItem);FinanceButton(product,"إضافة صنف منتج نهائي غير موجود",async m=>{if(await AddSellingProductAsync())Notice(m,"تم حفظ الصنف. اختيار الصنف وحده لا ينشئ رصيداً مخزنياً.",true);else Notice(m,"تم الرجوع إلى نموذج البيع.",true);});Field(product,"المخزن",sellingWarehouse);
        var completion=new StackPanel{Spacing=8};sellingJob=new("أمر الشغل الذي اجتاز الفحص");Field(completion,"أمر الشغل",sellingJob);sellingJobCost=ExecText("");completion.Children.Add(sellingJobCost);
        sellingCompletionRef=ExecInput("مرجع استلام الإنتاج");sellingCompletionRef.Text=SalesReference("FG");Field(completion,"مرجع الاستلام",sellingCompletionRef);
        sellingApproveCost=new CheckBox{Content="راجعت مطابقة الصنف والكمية، واكتمل تسجيل الخامات والأجور والتعبئة وجميع تكاليف هذا الأمر"};completion.Children.Add(sellingApproveCost);
        FinanceButton(completion,"اعتماد التكلفة واستلام الدفعة التامة",async m=>{
            if(sellingApproveCost.IsChecked!=true)throw new InvalidOperationException("راجع تكاليف الأمر ومطابقة الصنف قبل الاعتماد.");var job=Rows("readyJobs").FirstOrDefault(j=>J(j,"id")==Required(sellingJob));if(job.ValueKind!=JsonValueKind.Object)throw new InvalidOperationException("اختر أمر الشغل.");
            var result=await SubmitGuidedFinanceAsync("documents",new(){["kind"]="production_completion",["reference"]=Nonempty(sellingCompletionRef.Text,"اكتب مرجع الاستلام."),["date"]=ValidDate(sellingDate.Text),["productionOrderId"]=Required(sellingJob),["costApproved"]=true,["lines"]=new[]{new{itemId=Required(sellingItem),warehouseId=Required(sellingWarehouse),quantity=J(job,"quantity")}},["notes"]="استلام الإنتاج التام بعد مراجعة التكاليف"},m);
            if(result is not null){sellingApproveCost.IsChecked=false;Notice(m,"تم تحويل التكلفة المسجلة إلى مخزون التام. يمكن الآن تسعير المنتج وبيعه.",true);}
        });
        product.Children.Add(new Expander{Header="المنتج من تصنيعنا ولم يدخل مخزون التام بعد؟",Content=completion});
        var price=Card(root,"2. سعر البيع والحسابات");sellingQuantity=ExecInput("الكمية");sellingQuantity.Text="1";sellingPrice=ExecInput("سعر الوحدة قبل الضريبة");sellingDiscount=ExecInput("خصم إجمالي — صفر إن لم يوجد");sellingDiscount.Text="0";sellingDelivery=ExecInput("رسوم توصيل — صفر إن لم توجد");sellingDelivery.Text="0";
        Field(price,"الكمية المباعة",sellingQuantity);Field(price,"سعر بيع الوحدة قبل الضريبة",sellingPrice);Field(price,"الخصم الإجمالي",sellingDiscount);sellingTax=new("سياسة الضريبة المعتمدة أو إعفاء موثق");Field(price,"الضريبة",sellingTax);Field(price,"رسوم التوصيل",sellingDelivery);
        sellingPreview=ExecText("اختر المنتج وأدخل السعر لرؤية الحساب.",17);price.Children.Add(sellingPreview);
        FinanceButton(price,"حفظ سعر البيع لاستخدامه لاحقاً",async m=>{var result=await SubmitFinanceAsync("prices",new(){["itemId"]=Required(sellingItem),["orderId"]=sellingOrder.Value,["unitPrice"]=FinanceNumber(sellingPrice.Text,true),["date"]=ValidDate(sellingDate.Text),["notes"]="سعر بيع محفوظ — ليس بيعاً ولا تحصيلاً"},m);if(result is not null)Notice(m,"تم حفظ سعر البيع؛ لا يُضاف إيراد إلا عند تسجيل البيع.",true);});
        sellingReference=ExecInput("مرجع البيع الفريد");sellingReference.Text=SalesReference("SALE");Field(price,"مرجع البيع",sellingReference);sellingNotes=ExecInput("وصف اختياري");Field(price,"الوصف",sellingNotes);
        sellingDeliverNow=new CheckBox{Content="تم تسليم هذه الكمية للعميل الآن",IsChecked=true};price.Children.Add(sellingDeliverNow);
        FinanceButton(price,"تسجيل البيع والتسليم المحدد",async m=>{
            var reference=Nonempty(sellingReference.Text,"اكتب مرجع البيع.");var existing=Rows("documents").FirstOrDefault(d=>J(d,"kind")=="sale"&&J(d,"reference")==reference);
            if(existing.ValueKind==JsonValueKind.Object){sellingPosted.Select(J(existing,"id"));RefreshPostedSaleInfo();Notice(m,"هذا البيع مسجل بالفعل. اختر عملية جديدة إذا كان بيعاً آخر.",true);return;}
            var customer=Required(sellingCustomer);
            var result=await SubmitFinanceAsync("documents",new(){["kind"]=sellingDeliverNow.IsChecked==true?"sale_and_delivery":"sale",["reference"]=reference,["date"]=ValidDate(sellingDate.Text),["partyId"]=customer,["orderId"]=sellingOrder.Value,["deliveryCharge"]=FinanceNumber(sellingDelivery.Text),["deliverNow"]=sellingDeliverNow.IsChecked==true,["notes"]=sellingNotes.Text?.Trim(),["lines"]=new[]{new{itemId=Required(sellingItem),warehouseId=Required(sellingWarehouse),quantity=FinanceNumber(sellingQuantity.Text,true),unitPrice=FinanceNumber(sellingPrice.Text,true),discount=FinanceNumber(sellingDiscount.Text),taxPolicyId=Required(sellingTax)}}},m);
            if(result is JsonElement ack){sellingPosted.Select(J(ack,"id"));RefreshPostedSaleInfo();Notice(m,"تم تسجيل البيع. راجع تفاصيله أدناه وسجّل فقط المبلغ الذي استلمته بالفعل.",true);}
        });
        FinanceButton(price,"بدء عملية بيع جديدة",m=>{sellingReference.Text=SalesReference("SALE");sellingNotes.Text="";Notice(m,"أنشئ مرجع جديد. راجع الصنف والكمية والسعر قبل الحفظ.",true);return Task.CompletedTask;});
        var after=Card(root,"3. تسليم لاحق / تحصيل من العميل");sellingPosted=new("اختر البيع المسجل");Field(after,"البيع",sellingPosted);sellingPostedInfo=ExecText("");after.Children.Add(sellingPostedInfo);
        sellingPaymentAmount=ExecInput("المبلغ الذي استلمته فعلاً");sellingAccount=new("نقدية أو بنك");sellingPaymentRef=ExecInput("مرجع التحصيل");sellingPaymentRef.Text=SalesReference("PAY");Field(after,"المبلغ المحصل",sellingPaymentAmount);Field(after,"الحساب الذي دخلته الفلوس",sellingAccount);Field(after,"مرجع التحصيل",sellingPaymentRef);
        FinanceButton(after,"تسجيل المبلغ المحصل وتسوية البيع",async m=>{
            var sale=SelectedGuidedSale();var obligation=Rows("obligations").FirstOrDefault(o=>J(o,"document_id")==J(sale,"id"));if(obligation.ValueKind!=JsonValueKind.Object)throw new InvalidOperationException("لا توجد مديونية باقية لهذا البيع.");
            var reference=Nonempty(sellingPaymentRef.Text,"اكتب مرجع التحصيل.");if(Rows("documents").Any(d=>J(d,"kind")=="customer_receipt"&&J(d,"reference")==reference)){Notice(m,"هذا التحصيل مسجل بالفعل. اختر دفعة جديدة لتسجيل مبلغ آخر.",true);return;}
            await SubmitFinanceAsync("documents",new(){["kind"]="customer_receipt",["reference"]=reference,["date"]=ValidDate(sellingDate.Text),["partyId"]=J(sale,"party_id"),["accountId"]=Required(sellingAccount),["amount"]=FinanceNumber(sellingPaymentAmount.Text,true),["obligationId"]=J(obligation,"id"),["notes"]="تحصيل مقابل "+J(sale,"reference")},m);
        });
        FinanceButton(after,"دفعة تحصيل جديدة",m=>{sellingPaymentRef.Text=SalesReference("PAY");sellingPaymentAmount.Text="";Notice(m,"أدخل قيمة الدفعة الجديدة فقط.",true);return Task.CompletedTask;});
        var later=new StackPanel{Spacing=7};sellingDeliveryQuantity=ExecInput("الكمية المسلمة لاحقاً");sellingDeliveryRef=ExecInput("مرجع التسليم");sellingDeliveryRef.Text=SalesReference("DEL");Field(later,"الكمية المسلمة الآن",sellingDeliveryQuantity);Field(later,"مرجع التسليم",sellingDeliveryRef);
        FinanceButton(later,"تسجيل تسليم لاحق دون خصم المخزون مرة أخرى",async m=>{
            var sale=SelectedGuidedSale();var lines=Rows("lines").Where(l=>J(l,"document_id")==J(sale,"id")).ToList();if(lines.Count!=1)throw new InvalidOperationException("هذا البيع متعدد الأصناف؛ استخدم نموذج التسليم المتقدم لتحديد كل صنف.");var line=lines[0];
            await SubmitFinanceAsync("documents",new(){["kind"]="delivery",["reference"]=Nonempty(sellingDeliveryRef.Text,"اكتب مرجع التسليم."),["date"]=ValidDate(sellingDate.Text),["sourceId"]=J(sale,"id"),["lines"]=new[]{new{itemId=J(line,"item_id"),warehouseId=J(line,"warehouse_id"),sourceLineId=J(line,"id"),quantity=FinanceNumber(sellingDeliveryQuantity.Text,true)}}},m);
        });after.Children.Add(new Expander{Header="إذا لم يتم التسليم وقت البيع",Content=later});
        sellingCustomer.Changed+=()=>{var order=Rows("orders").FirstOrDefault(o=>J(o,"id")==sellingOrder.Value);if(order.ValueKind==JsonValueKind.Object&&J(order,"customer_id")!=sellingCustomer.Value)sellingOrder.Select(null);RefreshGuidedSalesContext();LoadSavedSellingPrice();return Task.CompletedTask;};sellingOrder.Changed+=()=>{var order=Rows("orders").FirstOrDefault(o=>J(o,"id")==sellingOrder.Value);if(order.ValueKind==JsonValueKind.Object)sellingCustomer.Select(J(order,"customer_id"));RefreshGuidedSalesContext();LoadSavedSellingPrice();return Task.CompletedTask;};sellingItem.Changed+=()=>{sellingApproveCost.IsChecked=false;LoadSavedSellingPrice();RefreshSellingPreview();return Task.CompletedTask;};sellingWarehouse.Changed+=()=>{RefreshSellingPreview();return Task.CompletedTask;};sellingTax.Changed+=()=>{RefreshSellingPreview();return Task.CompletedTask;};sellingJob.Changed+=()=>{sellingApproveCost.IsChecked=false;RefreshSellingJob();return Task.CompletedTask;};sellingPosted.Changed+=()=>{RefreshPostedSaleInfo();return Task.CompletedTask;};
        foreach(var input in SellingInputs())input.TextChanged+=(_,_)=>{RefreshSellingPreview();SaveGuidedSalesDraft();};foreach(var picker in SellingPickers())picker.Changed+=()=>{SaveGuidedSalesDraft();return Task.CompletedTask;};
        sellingDate.LostFocus+=(_,_)=>{if(!sellingRestoring&&!financeRestoring)RefreshGuidedSalesContext();};
        sellingDeliverNow.IsCheckedChanged+=(_,_)=>SaveGuidedSalesDraft();return new TabItem{Header="بيع وتسليم",Content=root};
    }
    private void RefreshGuidedSalesContext()
    {
        if(sellingOrder is null)return;var wasRestoring=sellingRestoring;sellingRestoring=true;try{
            sellingCustomer.Set((currentMasterData?.Parties??[]).Where(p=>p.PartyType is "customer" or "both").Select(p=>new ExecChoice(p.Id,p.Code+" — "+p.NameAr)));
            sellingOrder.Set(Rows("orders").Where(o=>sellingCustomer.Value is null||J(o,"customer_id")==sellingCustomer.Value).Select(o=>new ExecChoice(J(o,"id"),J(o,"order_no")+" — "+J(o,"product_name"))));
            sellingItem.Set((currentMasterData?.Items??[]).Where(i=>i.ItemType=="finished_good"&&i.StockTracked&&i.Classification=="materials").Select(i=>new ExecChoice(i.Id,i.Code+" — "+i.NameAr)));
            sellingWarehouse.Set((currentMasterData?.Warehouses??[]).Where(w=>w.WarehouseType=="finished_goods").Select(w=>new ExecChoice(w.Id,w.Code+" — "+w.NameAr)),firstIfSingle:true);
            var date=sellingDate.Text??"";var policies=Rows("policies").Where(p=>string.CompareOrdinal(J(p,"effective_from"),date)<=0).ToList();sellingTax.Set(policies.Where(p=>!policies.Any(n=>J(n,"code")==J(p,"code")&&string.CompareOrdinal(J(n,"effective_from"),J(p,"effective_from"))>0)).Select(p=>new ExecChoice(J(p,"id"),J(p,"code")+" — "+J(p,"rate")+"%")),firstIfSingle:true);
            sellingJob.Set(Rows("readyJobs").Where(j=>(sellingOrder.Value is not null?J(j,"orderId")==sellingOrder.Value:J(j,"orderId")=="")).Select(j=>new ExecChoice(J(j,"id"),J(j,"jobNo")+" — "+ExecutionStatus(J(j,"status")))));
            var order=Rows("orders").FirstOrDefault(o=>J(o,"id")==sellingOrder.Value);sellingPosted.Set(Rows("documents").Where(d=>J(d,"kind")=="sale"&&(sellingOrder.Value is not null?J(d,"customer_order_id")==sellingOrder.Value:sellingCustomer.Value is null||J(d,"party_id")==sellingCustomer.Value)).Select(d=>new ExecChoice(J(d,"id"),J(d,"reference")+" | "+J(d,"total"))));
            sellingAccount.Set(Rows("accounts").Select(a=>new ExecChoice(J(a,"id"),J(a,"code")+" — "+J(a,"name_ar"))),firstIfSingle:true);
            var count=Rows("documents").Count(d=>J(d,"kind")=="sale");if(financeData.ValueKind==JsonValueKind.Object&&financeData.TryGetProperty("salesDiagnostics",out var diagnostics))count=int.TryParse(J(diagnostics,"saleCount"),out var allCount)?allCount:count;sellingReadiness.Text=count==0?"الإيراد صفر لأنه لم يُسجل بيع بعد. ابدأ باختيار الطلب والصنف؛ تكلفة الإنتاج أو حفظ السعر وحدهما لا ينشئان إيراداً.":$"يوجد {count} بيع في النظام. يعرض دفتر الشهر الحركات حسب تاريخها. أي بيع جديد يحتاج مخزوناً نهائياً متاحاً وتكلفة مسجلة.";
            if(!GuidedSalesApiReady())sellingReadiness.Text="خادم API الحالي لا يحتوي على مسار البيع المبسط الجديد. أعد بناءه وتشغيله بعد تطبيق الترحيل 017 قبل تسجيل أي عملية من هنا.";
            if(!(currentMasterData?.Items.Any(i=>i.ItemType=="finished_good")??false))sellingReadiness.Text+="\nلا يوجد صنف منتج نهائي بعد؛ استخدم إضافة صنف المنتج أدناه.";
            if(!Rows("policies").Any())sellingReadiness.Text+="\nلا توجد سياسة ضريبة: أضف السياسة المعتمدة أو إعفاء موثق بنسبة صفر من الإعدادات المتقدمة.";
        }finally{sellingRestoring=wasRestoring;}RefreshSellingJob();RefreshSellingPreview();RefreshPostedSaleInfo();
    }
    private void LoadSavedSellingPrice(){if(sellingRestoring||sellingItem.Value is null)return;var candidates=Rows("savedPrices").Where(p=>J(p,"itemId")==sellingItem.Value&&string.CompareOrdinal(J(p,"date"),sellingDate.Text)<=0);var price=candidates.FirstOrDefault(p=>J(p,"orderId")==sellingOrder.Value);if(price.ValueKind!=JsonValueKind.Object)price=candidates.FirstOrDefault(p=>J(p,"orderId")=="");sellingPrice.Text=price.ValueKind==JsonValueKind.Object?J(price,"unitPrice"):"";}
    private void RefreshSellingJob(){if(sellingJobCost is null)return;var job=Rows("readyJobs").FirstOrDefault(j=>J(j,"id")==sellingJob.Value);sellingJobCost.Text=job.ValueKind==JsonValueKind.Object?$"الحالة: {ExecutionStatus(J(job,"status"))} | المقبول: {J(job,"accepted")} / {J(job,"quantity")}\nالتكلفة المباشرة المسجلة للدفعة: {J(job,"directCost")} ج.م\nيستلم هذا الزر الدفعة الكاملة فقط، مرة واحدة. إذا وجدت مصاريف مشتركة غير موزعة يوضح الخادم السبب ويوقف الاستلام.":"اختر أمر الشغل المقبول بالكامل. الأوامر غير المقبولة تحتاج فحصاً وقبولاً من شاشة الإنتاج أولاً.";}
    private void RefreshSellingPreview()
    {
        if(sellingPreview is null)return;try{
            var q=decimal.Parse(FinanceNumber(sellingQuantity.Text,true),CultureInfo.InvariantCulture);var price=decimal.Parse(FinanceNumber(sellingPrice.Text,true),CultureInfo.InvariantCulture);var discount=decimal.Parse(FinanceNumber(sellingDiscount.Text),CultureInfo.InvariantCulture);var delivery=decimal.Parse(FinanceNumber(sellingDelivery.Text),CultureInfo.InvariantCulture);
            var stock=Rows("stock").FirstOrDefault(s=>J(s,"item_id")==sellingItem.Value&&J(s,"warehouse_id")==sellingWarehouse.Value);if(stock.ValueKind!=JsonValueKind.Object||Number(J(stock,"quantity"))<=0)throw new InvalidOperationException("لا يوجد رصيد متاح لهذا المنتج في المخزن المختار. استلم الإنتاج المقبول أو راجع المخزون أولاً.");
            if(q>Number(J(stock,"quantity")))throw new InvalidOperationException("الكمية تتجاوز المخزون المتاح: "+J(stock,"quantity"));
            var policy=Rows("policies").FirstOrDefault(p=>J(p,"id")==sellingTax.Value);if(policy.ValueKind!=JsonValueKind.Object)throw new InvalidOperationException("اختر سياسة الضريبة المعتمدة قبل البيع.");
            var gross=decimal.Round(q*price,4,MidpointRounding.AwayFromZero);if(discount>gross)throw new InvalidOperationException("الخصم أكبر من قيمة البيع.");var net=gross-discount;var tax=decimal.Round(net*Number(J(policy,"rate"))/100,4,MidpointRounding.AwayFromZero);
            var globalQuantity=Number(J(stock,"global_quantity"));var value=Number(J(stock,"inventory_value"));var cost=globalQuantity>0?decimal.Round(q*value/globalQuantity,4,MidpointRounding.AwayFromZero):decimal.Round(q*Number(J(stock,"unit_cost")),4,MidpointRounding.AwayFromZero);var profit=net-cost;
            sellingPreview.Text=$"صافي البيع قبل الضريبة: {net:0.####} ج.م\nالضريبة: {tax:0.####} | التوصيل: {delivery:0.####} | إجمالي المطلوب من العميل: {net+tax+delivery:0.####} ج.م\nتكلفة الكمية من المخزون: {cost:0.####} | مجمل الربح المتوقع: {profit:0.####} ج.م\nهذه معاينة على الرصيد المحمل؛ الخادم يعيد الحساب عند التسجيل. مصروفات المكتب والإدارة تظهر في ملخص الشهر منفصلة عن مجمل ربح المنتج.";
        }catch(Exception ex){sellingPreview.Text=FriendlyError(ex);}
    }
    private bool GuidedSalesApiReady()=>financeData.ValueKind==JsonValueKind.Object&&financeData.TryGetProperty("capabilities",out var a)&&a.ValueKind==JsonValueKind.Array&&a.EnumerateArray().Any(v=>v.GetString()=="guided_sales_v1");
    private Task<JsonElement?> SubmitGuidedFinanceAsync(string endpoint,Dictionary<string,object?> data,TextBlock message){if(!GuidedSalesApiReady())throw new InvalidOperationException("أعد بناء وتشغيل خادم API الجديد بعد الترحيل 017. لن نسجل بيعاً من واجهة أحدث على خادم قديم.");return SubmitFinanceAsync(endpoint,data,message);}
    private JsonElement SelectedGuidedSale(){var sale=Rows("documents").FirstOrDefault(d=>J(d,"id")==sellingPosted.Value&&J(d,"kind")=="sale");return sale.ValueKind==JsonValueKind.Object?sale:throw new InvalidOperationException("اختر البيع المسجل أولاً.");}
    private void RefreshPostedSaleInfo(){if(sellingPostedInfo is null)return;try{var sale=SelectedGuidedSale();var debt=Rows("obligations").FirstOrDefault(o=>J(o,"document_id")==J(sale,"id"));var lines=Rows("lines").Where(l=>J(l,"document_id")==J(sale,"id")).ToList();sellingPostedInfo.Text=$"البيع: {J(sale,"reference")} | الإجمالي: {J(sale,"total")} | الضريبة: {J(sale,"tax")} | تكلفة البيع: {J(sale,"cogs")}\nالمتبقي على العميل: {(debt.ValueKind==JsonValueKind.Object?J(debt,"outstanding"):"0")}\n"+string.Join("\n",lines.Select(l=>$"{J(l,"item_code")} | مباع: {J(l,"quantity")} | مسلم صافياً: {J(l,"delivered")} | مرتجع: {J(l,"returned")}"));}catch{sellingPostedInfo.Text="اختر بيعاً لرؤية التسليم والمديونية. لم تُسجل أي نقود بمجرد حفظ السعر أو البيع.";}}
    private async Task<bool> AddSellingProductAsync()
    {
        var dialog=new Window{Title="إضافة منتج نهائي",Width=510,Height=560,WindowStartupLocation=WindowStartupLocation.CenterOwner};var root=new StackPanel{Spacing=8,Margin=new Thickness(16)};dialog.Content=new ScrollViewer{Content=root};var code=ExecInput("كود المنتج النهائي");var name=ExecInput("الاسم بالعربية");var english=ExecInput("English name — اختياري");var unit=new ProductionPicker("وحدة القياس");unit.Set((currentMasterData?.Units??[]).Select(u=>new ExecChoice(u.Id,u.Code+" — "+u.NameAr)),firstIfSingle:true);
        var job=Rows("readyJobs").FirstOrDefault(j=>J(j,"id")==sellingJob.Value);if(job.ValueKind==JsonValueKind.Object){code.Text=J(job,"productCode");name.Text=J(job,"productName");}
        Field(root,"الكود الفريد",code);Field(root,"الاسم",name);Field(root,"الاسم الإنجليزي",english);Field(root,"الوحدة",unit);string? createdId=null;
        FinanceButton(root,"حفظ صنف المنتج النهائي",async m=>{using var request=new HttpRequestMessage(HttpMethod.Post,"master-data/items"){Content=JsonContent.Create(new{code=Nonempty(code.Text,"اكتب الكود."),nameAr=Nonempty(name.Text,"اكتب الاسم."),nameEn=english.Text?.Trim(),itemType="finished_good",baseUnitId=Required(unit),stockTracked=true,classification="materials"},options:JsonOptions)};request.Headers.Authorization=new("Bearer",accessToken);using var response=await HttpClient.SendAsync(request);await EnsureApiSuccessAsync(response);using var result=JsonDocument.Parse(await response.Content.ReadAsStringAsync());createdId=J(result.RootElement,"id");dialog.Close();});
        await dialog.ShowDialog(this);if(createdId is not null){await RefreshFinanceAsync();sellingItem.Select(createdId);RefreshSellingPreview();return true;}return false;
    }
    private void SaveGuidedSalesDraft(){if(sellingRestoring||financeRestoring||financeOutbox is null||string.IsNullOrWhiteSpace(accessToken)||sellingDate is null)return;financeOutbox.Cache("guided-sales:"+FinanceUser,JsonSerializer.Serialize(new GuidedSalesDraft(SellingInputs().Select(x=>x.Text??"").ToArray(),SellingPickers().Select(x=>x.Value).ToArray(),sellingDeliverNow.IsChecked==true),JsonOptions));}
    private void RestoreGuidedSalesDraft(){if(sellingOrder is null||string.IsNullOrWhiteSpace(accessToken)||sellingDraftUser==FinanceUser)return;sellingDraftUser=FinanceUser;var json=financeOutbox?.Cached("guided-sales:"+FinanceUser);if(json is null)return;sellingRestoring=true;try{var draft=JsonSerializer.Deserialize<GuidedSalesDraft>(json,JsonOptions)!;var inputs=SellingInputs();for(var i=0;i<Math.Min(inputs.Length,draft.Text.Length);i++)inputs[i].Text=draft.Text[i];var pickers=SellingPickers();for(var i=0;i<Math.Min(pickers.Length,draft.Choices.Length);i++){pickers[i].Select(draft.Choices[i]);if(i==0)RefreshGuidedSalesContext();}sellingDeliverNow.IsChecked=draft.DeliverNow;sellingApproveCost.IsChecked=false;}finally{sellingRestoring=false;}RefreshSellingJob();RefreshSellingPreview();RefreshPostedSaleInfo();}
    private void ClearGuidedSalesSession(){SaveGuidedSalesDraft();sellingDraftUser=null;if(sellingDate is null)return;sellingRestoring=true;try{foreach(var t in SellingInputs())t.Text="";foreach(var p in SellingPickers())p.Set([]);sellingApproveCost.IsChecked=false;sellingDeliverNow.IsChecked=true;sellingDate.Text=DateTime.Today.ToString("yyyy-MM-dd");sellingQuantity.Text="1";sellingDiscount.Text=sellingDelivery.Text="0";sellingReference.Text=SalesReference("SALE");sellingCompletionRef.Text=SalesReference("FG");sellingPaymentRef.Text=SalesReference("PAY");sellingDeliveryRef.Text=SalesReference("DEL");sellingPrice.Text="";sellingReadiness.Text=sellingPreview.Text=sellingJobCost.Text=sellingPostedInfo.Text="";}finally{sellingRestoring=false;}}
    private sealed record GuidedSalesDraft(string[] Text,string?[] Choices,bool DeliverNow);
}
