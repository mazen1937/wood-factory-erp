using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using WoodFactoryErp.Desktop.Security;
using WoodFactoryErp.Desktop.Documents;

namespace WoodFactoryErp.Desktop;

public partial class MainWindow : Window
{
    private static readonly HttpClient HttpClient = new() { BaseAddress = new Uri("http://localhost:3000/api/") };
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { PropertyNameCaseInsensitive = true };
    private string? accessToken;

    private Border LoginPanel = null!;
    private Border WelcomePanel = null!;
    private TextBox UsernameTextBox = null!;
    private TextBox PasswordTextBox = null!;
    private TextBlock LoginMessageTextBlock = null!;
    private TextBlock WelcomeTextBlock = null!;
    private TextBlock RolesTextBlock = null!;
    private Button SignInButton = null!;
    private Button AdminButton = null!;
    private Button MasterDataButton = null!;
    private ScrollViewer HomePanel = null!;
    private ScrollViewer MasterDataPanel = null!;
    private ScrollViewer InventoryPanel = null!;
    private ScrollViewer OrdersPanel = null!;
    private Button InventoryButton = null!;
    private Button OrdersButton = null!;
    private Button RefreshOrdersButton = null!;
    private TextBlock OrdersMessageTextBlock = null!;
    private ComboBox SelectedOrderComboBox = null!;
    private TextBox OrderNumberTextBox = null!;
    private ComboBox OrderCustomerComboBox = null!;
    private TextBox OrderDateTextBox = null!;
    private TextBox OrderDeliveryDateTextBox = null!;
    private TextBox OrderNotesTextBox = null!;
    private Button CreateOrderButton = null!;
    private TextBlock OrdersListTextBlock = null!;
    private TextBox EngineeringReferenceTextBox = null!;
    private TextBox EngineeringNotesTextBox = null!;
    private Button CreateEngineeringPackageButton = null!;
    private ComboBox EngineeringPackageComboBox = null!;
    private TextBlock EngineeringPackageSummaryTextBlock = null!;
    private ComboBox EngineeringLineTypeComboBox = null!;
    private TextBox EngineeringLineDescriptionTextBox = null!;
    private TextBox EngineeringLineMaterialReferenceTextBox = null!;
    private TextBox EngineeringLineQuantityTextBox = null!;
    private TextBox EngineeringLineUnitTextBox = null!;
    private TextBox EngineeringLineDimensionsTextBox = null!;
    private TextBox EngineeringLineNotesTextBox = null!;
    private Button AddEngineeringLineButton = null!;
    private ComboBox EngineeringPackageFileComboBox = null!;
    private Button UploadEngineeringFileButton = null!;
    private Button DownloadEngineeringFileButton = null!;
    private Button FinalizeEngineeringPackageButton = null!;
    private TextBlock EngineeringLinesTextBlock = null!;
    private ComboBox EngineeringAttachmentTypeComboBox = null!;
    private TextBox EngineeringAttachmentDescriptionTextBox = null!;
    private TextBox EngineeringMaterialCodeTextBox = null!;
    private TextBox EngineeringMaterialColorTextBox = null!;
    private TextBox EngineeringBoardThicknessTextBox = null!;
    private TextBox EngineeringBoardLengthTextBox = null!;
    private TextBox EngineeringBoardWidthTextBox = null!;
    private TextBox EngineeringBoardQuantityTextBox = null!;
    private ComboBox EngineeringCutListFileComboBox = null!;
    private TextBox EngineeringCutListPartNameTextBox = null!;
    private TextBox EngineeringCutListPartLengthTextBox = null!;
    private TextBox EngineeringCutListPartWidthTextBox = null!;
    private TextBox EngineeringCutListPartThicknessTextBox = null!;
    private TextBox EngineeringCutListPartQuantityTextBox = null!;
    private TextBox EngineeringCutListPartNotesTextBox = null!;
    private Button AddEngineeringCutListPartButton = null!;
    private TextBlock EngineeringCutListPartsTextBlock = null!;
    private TextBox ProductionOrderNumberTextBox = null!;
    private TextBox ProductionProductCodeTextBox = null!;
    private TextBox ProductionProductNameTextBox = null!;
    private TextBox ProductionDateTextBox = null!;
    private TextBox ProductionPlannedQuantityTextBox = null!;
    private Button CreateProductionOrderButton = null!;
    private TextBlock ProductionOrdersTextBlock = null!;
    private ComboBox ProductionOrderComboBox = null!;
    private TextBlock ProductionWorkOrderSummaryTextBlock = null!;
    private StackPanel ProductionWorkOrderLinesPanel = null!;
    private Button SaveProductionWorkOrderButton = null!;
    private Button DownloadProductionWorkOrderPdfButton = null!;
    private TextBlock ProductionWorkOrderMessageTextBlock = null!;
    private bool isLoadingProductionOrders;
    private bool isLoadingProductionWorkOrder;
    private List<ProductionOrderView> currentProductionOrders = [];
    private List<WorkOrderLineEditor> currentWorkOrderLineEditors = [];
    private bool isLoadingOrders;
    private bool isLoadingPackages;
    private List<CustomerOrderView> currentOrders = [];
    private List<EngineeringPackageView> currentPackages = [];
    private Button RefreshInventoryButton = null!;
    private TextBlock InventoryMessageTextBlock = null!;
    private TextBlock InventoryBalancesTextBlock = null!;
    private TextBlock InventoryMovementsTextBlock = null!;
    private ComboBox ReceiptItemComboBox = null!;
    private ComboBox ReceiptWarehouseComboBox = null!;
    private ComboBox ReceiptSupplierComboBox = null!;
    private TextBox ReceiptReferenceTextBox = null!;
    private TextBox ReceiptDateTextBox = null!;
    private TextBox ReceiptQuantityTextBox = null!;
    private TextBox ReceiptUnitCostTextBox = null!;
    private TextBox ReceiptNotesTextBox = null!;
    private Button PostReceiptButton = null!;
    private Button RefreshMasterDataButton = null!;
    private TextBlock MasterDataMessageTextBlock = null!;
    private TextBlock UnitsTextBlock = null!;
    private TextBlock WarehousesTextBlock = null!;
    private TextBlock ItemsTextBlock = null!;
    private TextBlock PartiesTextBlock = null!;

    private TextBox UnitCodeTextBox = null!;
    private TextBox UnitNameArTextBox = null!;
    private TextBox UnitNameEnTextBox = null!;
    private TextBox UnitPrecisionTextBox = null!;
    private Button SaveUnitButton = null!;
    private TextBox WarehouseCodeTextBox = null!;
    private TextBox WarehouseNameArTextBox = null!;
    private TextBox WarehouseNameEnTextBox = null!;
    private ComboBox WarehouseTypeComboBox = null!;
    private Button SaveWarehouseButton = null!;
    private TextBox PartyCodeTextBox = null!;
    private ComboBox PartyTypeComboBox = null!;
    private TextBox PartyNameArTextBox = null!;
    private TextBox PartyNameEnTextBox = null!;
    private TextBox PartyPhoneTextBox = null!;
    private Button SavePartyButton = null!;
    private TextBox ItemCodeTextBox = null!;
    private TextBox ItemNameArTextBox = null!;
    private TextBox ItemNameEnTextBox = null!;
    private ComboBox ItemTypeComboBox = null!;
    private ComboBox ItemBaseUnitComboBox = null!;
    private ComboBox ItemClassificationComboBox = null!;
    private CheckBox ItemStockTrackedCheckBox = null!;
    private Button SaveItemButton = null!;

    private MasterDataResponse? currentMasterData;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        FindControls();
        InitializeChoices();
        _ = RestoreSessionAsync();
    }

    private void FindControls()
    {
        LoginPanel = this.FindControl<Border>("LoginPanel")!;
        WelcomePanel = this.FindControl<Border>("WelcomePanel")!;
        UsernameTextBox = this.FindControl<TextBox>("UsernameTextBox")!;
        PasswordTextBox = this.FindControl<TextBox>("PasswordTextBox")!;
        LoginMessageTextBlock = this.FindControl<TextBlock>("LoginMessageTextBlock")!;
        WelcomeTextBlock = this.FindControl<TextBlock>("WelcomeTextBlock")!;
        RolesTextBlock = this.FindControl<TextBlock>("RolesTextBlock")!;
        SignInButton = this.FindControl<Button>("SignInButton")!;
        AdminButton = this.FindControl<Button>("AdminButton")!;
        MasterDataButton = this.FindControl<Button>("MasterDataButton")!;
        HomePanel = this.FindControl<ScrollViewer>("HomePanel")!;
        MasterDataPanel = this.FindControl<ScrollViewer>("MasterDataPanel")!;
        InventoryPanel = this.FindControl<ScrollViewer>("InventoryPanel")!;
        OrdersPanel = this.FindControl<ScrollViewer>("OrdersPanel")!;
        InventoryButton = this.FindControl<Button>("InventoryButton")!;
        OrdersButton = this.FindControl<Button>("OrdersButton")!;
        RefreshOrdersButton = this.FindControl<Button>("RefreshOrdersButton")!;
        OrdersMessageTextBlock = this.FindControl<TextBlock>("OrdersMessageTextBlock")!;
        SelectedOrderComboBox = this.FindControl<ComboBox>("SelectedOrderComboBox")!;
        OrderNumberTextBox = this.FindControl<TextBox>("OrderNumberTextBox")!;
        OrderCustomerComboBox = this.FindControl<ComboBox>("OrderCustomerComboBox")!;
        OrderDateTextBox = this.FindControl<TextBox>("OrderDateTextBox")!;
        OrderDeliveryDateTextBox = this.FindControl<TextBox>("OrderDeliveryDateTextBox")!;
        OrderNotesTextBox = this.FindControl<TextBox>("OrderNotesTextBox")!;
        CreateOrderButton = this.FindControl<Button>("CreateOrderButton")!;
        OrdersListTextBlock = this.FindControl<TextBlock>("OrdersListTextBlock")!;
        EngineeringReferenceTextBox = this.FindControl<TextBox>("EngineeringReferenceTextBox")!;
        EngineeringNotesTextBox = this.FindControl<TextBox>("EngineeringNotesTextBox")!;
        CreateEngineeringPackageButton = this.FindControl<Button>("CreateEngineeringPackageButton")!;
        EngineeringPackageComboBox = this.FindControl<ComboBox>("EngineeringPackageComboBox")!;
        EngineeringPackageSummaryTextBlock = this.FindControl<TextBlock>("EngineeringPackageSummaryTextBlock")!;
        EngineeringLineTypeComboBox = this.FindControl<ComboBox>("EngineeringLineTypeComboBox")!;
        EngineeringLineDescriptionTextBox = this.FindControl<TextBox>("EngineeringLineDescriptionTextBox")!;
        EngineeringLineMaterialReferenceTextBox = this.FindControl<TextBox>("EngineeringLineMaterialReferenceTextBox")!;
        EngineeringLineQuantityTextBox = this.FindControl<TextBox>("EngineeringLineQuantityTextBox")!;
        EngineeringLineUnitTextBox = this.FindControl<TextBox>("EngineeringLineUnitTextBox")!;
        EngineeringLineDimensionsTextBox = this.FindControl<TextBox>("EngineeringLineDimensionsTextBox")!;
        EngineeringLineNotesTextBox = this.FindControl<TextBox>("EngineeringLineNotesTextBox")!;
        AddEngineeringLineButton = this.FindControl<Button>("AddEngineeringLineButton")!;
        EngineeringPackageFileComboBox = this.FindControl<ComboBox>("EngineeringPackageFileComboBox")!;
        UploadEngineeringFileButton = this.FindControl<Button>("UploadEngineeringFileButton")!;
        DownloadEngineeringFileButton = this.FindControl<Button>("DownloadEngineeringFileButton")!;
        FinalizeEngineeringPackageButton = this.FindControl<Button>("FinalizeEngineeringPackageButton")!;
        EngineeringLinesTextBlock = this.FindControl<TextBlock>("EngineeringLinesTextBlock")!;
        EngineeringAttachmentTypeComboBox = this.FindControl<ComboBox>("EngineeringAttachmentTypeComboBox")!;
        EngineeringAttachmentDescriptionTextBox = this.FindControl<TextBox>("EngineeringAttachmentDescriptionTextBox")!;
        EngineeringMaterialCodeTextBox = this.FindControl<TextBox>("EngineeringMaterialCodeTextBox")!;
        EngineeringMaterialColorTextBox = this.FindControl<TextBox>("EngineeringMaterialColorTextBox")!;
        EngineeringBoardThicknessTextBox = this.FindControl<TextBox>("EngineeringBoardThicknessTextBox")!;
        EngineeringBoardLengthTextBox = this.FindControl<TextBox>("EngineeringBoardLengthTextBox")!;
        EngineeringBoardWidthTextBox = this.FindControl<TextBox>("EngineeringBoardWidthTextBox")!;
        EngineeringBoardQuantityTextBox = this.FindControl<TextBox>("EngineeringBoardQuantityTextBox")!;
        EngineeringCutListFileComboBox = this.FindControl<ComboBox>("EngineeringCutListFileComboBox")!;
        EngineeringCutListPartNameTextBox = this.FindControl<TextBox>("EngineeringCutListPartNameTextBox")!;
        EngineeringCutListPartLengthTextBox = this.FindControl<TextBox>("EngineeringCutListPartLengthTextBox")!;
        EngineeringCutListPartWidthTextBox = this.FindControl<TextBox>("EngineeringCutListPartWidthTextBox")!;
        EngineeringCutListPartThicknessTextBox = this.FindControl<TextBox>("EngineeringCutListPartThicknessTextBox")!;
        EngineeringCutListPartQuantityTextBox = this.FindControl<TextBox>("EngineeringCutListPartQuantityTextBox")!;
        EngineeringCutListPartNotesTextBox = this.FindControl<TextBox>("EngineeringCutListPartNotesTextBox")!;
        AddEngineeringCutListPartButton = this.FindControl<Button>("AddEngineeringCutListPartButton")!;
        EngineeringCutListPartsTextBlock = this.FindControl<TextBlock>("EngineeringCutListPartsTextBlock")!;
        ProductionOrderNumberTextBox = this.FindControl<TextBox>("ProductionOrderNumberTextBox")!;
        ProductionProductCodeTextBox = this.FindControl<TextBox>("ProductionProductCodeTextBox")!;
        ProductionProductNameTextBox = this.FindControl<TextBox>("ProductionProductNameTextBox")!;
        ProductionDateTextBox = this.FindControl<TextBox>("ProductionDateTextBox")!;
        ProductionPlannedQuantityTextBox = this.FindControl<TextBox>("ProductionPlannedQuantityTextBox")!;
        CreateProductionOrderButton = this.FindControl<Button>("CreateProductionOrderButton")!;
        ProductionOrdersTextBlock = this.FindControl<TextBlock>("ProductionOrdersTextBlock")!;
        ProductionOrderComboBox = this.FindControl<ComboBox>("ProductionOrderComboBox")!;
        ProductionWorkOrderSummaryTextBlock = this.FindControl<TextBlock>("ProductionWorkOrderSummaryTextBlock")!;
        ProductionWorkOrderLinesPanel = this.FindControl<StackPanel>("ProductionWorkOrderLinesPanel")!;
        SaveProductionWorkOrderButton = this.FindControl<Button>("SaveProductionWorkOrderButton")!;
        DownloadProductionWorkOrderPdfButton = this.FindControl<Button>("DownloadProductionWorkOrderPdfButton")!;
        ProductionWorkOrderMessageTextBlock = this.FindControl<TextBlock>("ProductionWorkOrderMessageTextBlock")!;
        RefreshInventoryButton = this.FindControl<Button>("RefreshInventoryButton")!;
        InventoryMessageTextBlock = this.FindControl<TextBlock>("InventoryMessageTextBlock")!;
        InventoryBalancesTextBlock = this.FindControl<TextBlock>("InventoryBalancesTextBlock")!;
        InventoryMovementsTextBlock = this.FindControl<TextBlock>("InventoryMovementsTextBlock")!;
        ReceiptItemComboBox = this.FindControl<ComboBox>("ReceiptItemComboBox")!;
        ReceiptWarehouseComboBox = this.FindControl<ComboBox>("ReceiptWarehouseComboBox")!;
        ReceiptSupplierComboBox = this.FindControl<ComboBox>("ReceiptSupplierComboBox")!;
        ReceiptReferenceTextBox = this.FindControl<TextBox>("ReceiptReferenceTextBox")!;
        ReceiptDateTextBox = this.FindControl<TextBox>("ReceiptDateTextBox")!;
        ReceiptQuantityTextBox = this.FindControl<TextBox>("ReceiptQuantityTextBox")!;
        ReceiptUnitCostTextBox = this.FindControl<TextBox>("ReceiptUnitCostTextBox")!;
        ReceiptNotesTextBox = this.FindControl<TextBox>("ReceiptNotesTextBox")!;
        PostReceiptButton = this.FindControl<Button>("PostReceiptButton")!;
        RefreshMasterDataButton = this.FindControl<Button>("RefreshMasterDataButton")!;
        MasterDataMessageTextBlock = this.FindControl<TextBlock>("MasterDataMessageTextBlock")!;
        UnitsTextBlock = this.FindControl<TextBlock>("UnitsTextBlock")!;
        WarehousesTextBlock = this.FindControl<TextBlock>("WarehousesTextBlock")!;
        ItemsTextBlock = this.FindControl<TextBlock>("ItemsTextBlock")!;
        PartiesTextBlock = this.FindControl<TextBlock>("PartiesTextBlock")!;

        UnitCodeTextBox = this.FindControl<TextBox>("UnitCodeTextBox")!;
        UnitNameArTextBox = this.FindControl<TextBox>("UnitNameArTextBox")!;
        UnitNameEnTextBox = this.FindControl<TextBox>("UnitNameEnTextBox")!;
        UnitPrecisionTextBox = this.FindControl<TextBox>("UnitPrecisionTextBox")!;
        SaveUnitButton = this.FindControl<Button>("SaveUnitButton")!;
        WarehouseCodeTextBox = this.FindControl<TextBox>("WarehouseCodeTextBox")!;
        WarehouseNameArTextBox = this.FindControl<TextBox>("WarehouseNameArTextBox")!;
        WarehouseNameEnTextBox = this.FindControl<TextBox>("WarehouseNameEnTextBox")!;
        WarehouseTypeComboBox = this.FindControl<ComboBox>("WarehouseTypeComboBox")!;
        SaveWarehouseButton = this.FindControl<Button>("SaveWarehouseButton")!;
        PartyCodeTextBox = this.FindControl<TextBox>("PartyCodeTextBox")!;
        PartyTypeComboBox = this.FindControl<ComboBox>("PartyTypeComboBox")!;
        PartyNameArTextBox = this.FindControl<TextBox>("PartyNameArTextBox")!;
        PartyNameEnTextBox = this.FindControl<TextBox>("PartyNameEnTextBox")!;
        PartyPhoneTextBox = this.FindControl<TextBox>("PartyPhoneTextBox")!;
        SavePartyButton = this.FindControl<Button>("SavePartyButton")!;
        ItemCodeTextBox = this.FindControl<TextBox>("ItemCodeTextBox")!;
        ItemNameArTextBox = this.FindControl<TextBox>("ItemNameArTextBox")!;
        ItemNameEnTextBox = this.FindControl<TextBox>("ItemNameEnTextBox")!;
        ItemTypeComboBox = this.FindControl<ComboBox>("ItemTypeComboBox")!;
        ItemBaseUnitComboBox = this.FindControl<ComboBox>("ItemBaseUnitComboBox")!;
        ItemClassificationComboBox = this.FindControl<ComboBox>("ItemClassificationComboBox")!;
        ItemStockTrackedCheckBox = this.FindControl<CheckBox>("ItemStockTrackedCheckBox")!;
        SaveItemButton = this.FindControl<Button>("SaveItemButton")!;
    }

    private void InitializeChoices()
    {
        WarehouseTypeComboBox.ItemsSource = new[]
        {
            new Choice("raw_material", "مخزن الخامات"),
            new Choice("finished_goods", "مخزن المنتجات التامة"),
        };
        WarehouseTypeComboBox.SelectedIndex = 0;

        PartyTypeComboBox.ItemsSource = new[]
        {
            new Choice("customer", "عميل"),
            new Choice("supplier", "مورد"),
            new Choice("both", "عميل ومورد"),
        };
        PartyTypeComboBox.SelectedIndex = 0;

        ItemTypeComboBox.ItemsSource = new[]
        {
            new Choice("raw_material", "خامة"),
            new Choice("accessory", "إكسسوار"),
            new Choice("consumable", "مستهلك"),
            new Choice("finished_good", "منتج تام"),
        };
        ItemTypeComboBox.SelectedIndex = 0;

        ItemClassificationComboBox.ItemsSource = new[]
        {
            new Choice("materials", "خامات بالمخزن"),
            new Choice("industrial_expense", "مصاريف صناعية"),
        };
        ItemClassificationComboBox.SelectedIndex = 0;

        EngineeringLineTypeComboBox.ItemsSource = new[]
        {
            new Choice("bom", "قائمة خامات (BOM)"),
            new Choice("cut_list", "قائمة قص"),
        };
        EngineeringLineTypeComboBox.SelectedIndex = 0;

        EngineeringAttachmentTypeComboBox.ItemsSource = new[]
        {
            new Choice("product_reference", "صورة المنتج المرجعية"),
            new Choice("dimensioned_drawing", "الرسم الهندسي بالأبعاد"),
            new Choice("cut_list", "مخطط الكاتلست (ألواح / لون / سمك)"),
            new Choice("bom_export", "تصدير قائمة المواد (BOM)"),
            new Choice("other", "مرفق آخر"),
        };
        EngineeringAttachmentTypeComboBox.SelectedIndex = 0;
    }

    private async void SignInAsync(object? sender, RoutedEventArgs e)
    {
        LoginMessageTextBlock.Text = string.Empty;
        SignInButton.IsEnabled = false;
        try
        {
            var response = await HttpClient.PostAsJsonAsync("auth/login", new
            {
                username = UsernameTextBox.Text?.Trim(),
                password = PasswordTextBox.Text,
            });

            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                LoginMessageTextBlock.Text = "اسم المستخدم أو كلمة المرور غير صحيحة.";
                return;
            }

            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<LoginResponse>(JsonOptions);
            if (result?.AccessToken is null || result.User is null)
                throw new InvalidOperationException("The server returned an incomplete session.");

            accessToken = result.AccessToken;
            SessionStore.Save(accessToken);
            ShowAuthenticatedWorkspace(result.User.DisplayName, result.User.Roles);
            PasswordTextBox.Text = string.Empty;
        }
        catch (HttpRequestException)
        {
            LoginMessageTextBlock.Text = "تعذر الاتصال بالخادم المحلي. تأكد من أن API يعمل ثم حاول مرة أخرى.";
        }
        catch
        {
            LoginMessageTextBlock.Text = "تعذر إتمام تسجيل الدخول. حاول مرة أخرى.";
        }
        finally
        {
            SignInButton.IsEnabled = true;
        }
    }

    private async Task RestoreSessionAsync()
    {
        var stored = SessionStore.Read();
        if (stored is null) return;

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "auth/me");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", stored);
            using var response = await HttpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                SessionStore.Delete();
                return;
            }

            var session = await response.Content.ReadFromJsonAsync<SessionProfile>(JsonOptions);
            if (session is null)
            {
                SessionStore.Delete();
                return;
            }

            accessToken = stored;
            ShowAuthenticatedWorkspace(session.Username, session.Roles ?? []);
        }
        catch
        {
            SessionStore.Delete();
        }
    }

    private void ShowAuthenticatedWorkspace(string displayName, string[] roles)
    {
        LoginPanel.IsVisible = false;
        WelcomePanel.IsVisible = true;
        WelcomeTextBlock.Text = $"مرحباً، {displayName}";
        RolesTextBlock.Text = $"الصلاحيات: {string.Join("، ", roles)}";
        AdminButton.IsVisible = roles.Contains("system_admin");
        MasterDataButton.IsEnabled = roles.Contains("system_admin");
        HomePanel.IsVisible = true;
        MasterDataPanel.IsVisible = false;
        InventoryPanel.IsVisible = false;
        OrdersPanel.IsVisible = false;
        InventoryButton.IsEnabled = roles.Contains("system_admin");
        OrdersButton.IsEnabled = true;
    }

    private void SignOut(object? sender, RoutedEventArgs e)
    {
        accessToken = null;
        SessionStore.Delete();
        WelcomePanel.IsVisible = false;
        InventoryPanel.IsVisible = false;
        MasterDataPanel.IsVisible = false;
        OrdersPanel.IsVisible = false;
        LoginPanel.IsVisible = true;
        PasswordTextBox.Text = string.Empty;
        LoginMessageTextBlock.Text = "تم تسجيل الخروج.";
    }

    private void ShowHome(object? sender, RoutedEventArgs e)
    {
        MasterDataPanel.IsVisible = false;
        InventoryPanel.IsVisible = false;
        OrdersPanel.IsVisible = false;
        HomePanel.IsVisible = true;
    }

    private async void ShowMasterDataAsync(object? sender, RoutedEventArgs e)
    {
        HomePanel.IsVisible = false;
        InventoryPanel.IsVisible = false;
        OrdersPanel.IsVisible = false;
        MasterDataPanel.IsVisible = true;
        await LoadMasterDataAsync();
    }

    private async void ShowInventoryAsync(object? sender, RoutedEventArgs e)
    {
        HomePanel.IsVisible = false;
        MasterDataPanel.IsVisible = false;
        OrdersPanel.IsVisible = false;
        InventoryPanel.IsVisible = true;
        ReceiptDateTextBox.Text = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        await LoadMasterDataAsync();
        await LoadInventoryAsync();
    }

    private async void RefreshInventoryAsync(object? sender, RoutedEventArgs e) => await LoadInventoryAsync();

    private async void RefreshMasterDataAsync(object? sender, RoutedEventArgs e) => await LoadMasterDataAsync();

    private async Task LoadMasterDataAsync()
    {
        if (string.IsNullOrWhiteSpace(accessToken)) return;

        MasterDataMessageTextBlock.Text = "جارٍ تحميل البيانات…";
        RefreshMasterDataButton.IsEnabled = false;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "master-data");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await HttpClient.SendAsync(request);
            await EnsureApiSuccessAsync(response);

            currentMasterData = await response.Content.ReadFromJsonAsync<MasterDataResponse>(JsonOptions)
                ?? throw new InvalidOperationException("استجابة البيانات الأساسية فارغة.");

            UnitsTextBlock.Text = Describe(currentMasterData.Units, x => $"{x.Code}  —  {x.NameAr} ({x.NameEn}) | الدقة: {x.PrecisionDigits}");
            WarehousesTextBlock.Text = Describe(currentMasterData.Warehouses, x => $"{x.Code}  —  {x.NameAr} ({WarehouseTypeLabel(x.WarehouseType)})");
            PartiesTextBlock.Text = Describe(currentMasterData.Parties, x => $"{x.Code}  —  {x.NameAr} ({PartyTypeLabel(x.PartyType)}){(string.IsNullOrWhiteSpace(x.Phone) ? "" : $" | {x.Phone}")}");
            ItemsTextBlock.Text = Describe(currentMasterData.Items, x =>
            {
                var unit = currentMasterData.Units.FirstOrDefault(u => u.Id == x.BaseUnitId);
                return $"{x.Code}  —  {x.NameAr} ({ItemTypeLabel(x.ItemType)}) | الوحدة: {unit?.NameAr ?? "غير معروفة"} | {ClassificationLabel(x.Classification)} | {(x.StockTracked ? "يتابع المخزون" : "لا يتابع المخزون")}";
            });
            ItemBaseUnitComboBox.ItemsSource = currentMasterData.Units
                .Select(u => new UnitChoice(u.Id, $"{u.Code} — {u.NameAr}"))
                .ToList();
            if (ItemBaseUnitComboBox.SelectedIndex < 0 && currentMasterData.Units.Count > 0)
                ItemBaseUnitComboBox.SelectedIndex = 0;

            MasterDataMessageTextBlock.Text = "تم تحميل البيانات الأساسية.";
        }
        catch (Exception ex)
        {
            MasterDataMessageTextBlock.Text = $"تعذر تحميل البيانات الأساسية: {FriendlyError(ex)}";
        }
        finally
        {
            RefreshMasterDataButton.IsEnabled = true;
        }
    }

    private async Task LoadInventoryAsync()
    {
        if (string.IsNullOrWhiteSpace(accessToken)) return;
        RefreshInventoryButton.IsEnabled = false;
        InventoryMessageTextBlock.Text = "جارٍ تحميل المخزون…";
        try
        {
            if (currentMasterData is null) throw new InvalidOperationException("تعذر تحميل البيانات الأساسية أولاً.");
            ReceiptItemComboBox.ItemsSource = currentMasterData.Items
                .Where(x => x.StockTracked && x.Classification == "materials" && x.ItemType != "finished_good")
                .Select(x => new Choice(x.Id, $"{x.Code} — {x.NameAr}" )).ToList();
            ReceiptWarehouseComboBox.ItemsSource = currentMasterData.Warehouses
                .Where(x => x.WarehouseType == "raw_material")
                .Select(x => new Choice(x.Id, $"{x.Code} — {x.NameAr}")).ToList();
            ReceiptSupplierComboBox.ItemsSource = new[] { new Choice("", "بدون تحديد مورد") }
                .Concat(currentMasterData.Parties.Where(x => x.PartyType is "supplier" or "both")
                .Select(x => new Choice(x.Id, $"{x.Code} — {x.NameAr}"))).ToList();
            if (ReceiptItemComboBox.SelectedIndex < 0 && ReceiptItemComboBox.ItemCount > 0) ReceiptItemComboBox.SelectedIndex = 0;
            if (ReceiptWarehouseComboBox.SelectedIndex < 0 && ReceiptWarehouseComboBox.ItemCount > 0) ReceiptWarehouseComboBox.SelectedIndex = 0;
            if (ReceiptSupplierComboBox.SelectedIndex < 0 && ReceiptSupplierComboBox.ItemCount > 0) ReceiptSupplierComboBox.SelectedIndex = 0;

            using var balancesRequest = new HttpRequestMessage(HttpMethod.Get, "inventory/balances");
            balancesRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var balancesResponse = await HttpClient.SendAsync(balancesRequest);
            await EnsureApiSuccessAsync(balancesResponse);
            var balances = await balancesResponse.Content.ReadFromJsonAsync<List<InventoryBalance>>(JsonOptions) ?? [];
            InventoryBalancesTextBlock.Text = balances.Count == 0 ? "لا توجد أرصدة مخزون بعد." : string.Join(Environment.NewLine, balances.Select(x =>
                $"{x.ItemCode} — {x.ItemNameAr} | المخزن: {x.WarehouseCode} | الكمية بالمخزن: {x.QuantityOnHand:N4} | متوسط تكلفة الوحدة: {x.AverageUnitCost:N4} ج.م | القيمة الإجمالية للصنف: {x.InventoryValue:N4} ج.م"));

            using var movementsRequest = new HttpRequestMessage(HttpMethod.Get, "inventory/movements");
            movementsRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var movementsResponse = await HttpClient.SendAsync(movementsRequest);
            await EnsureApiSuccessAsync(movementsResponse);
            var movements = await movementsResponse.Content.ReadFromJsonAsync<List<InventoryMovement>>(JsonOptions) ?? [];
            InventoryMovementsTextBlock.Text = movements.Count == 0 ? "لا توجد حركات بعد." : string.Join(Environment.NewLine, movements.Select(x =>
                $"{x.MovementDate:yyyy-MM-dd} | {x.DocumentReference} | {x.ItemCode} | كمية: {x.Quantity:N4} | تكلفة الوحدة: {x.UnitCost:N4} | الإجمالي: {x.TotalCost:N4} ج.م | متوسط بعد الحركة: {x.AverageCostAfter:N4}"));
            InventoryMessageTextBlock.Text = "تم تحميل الأرصدة والحركات.";
        }
        catch (Exception ex) { InventoryMessageTextBlock.Text = FriendlyError(ex); }
        finally { RefreshInventoryButton.IsEnabled = true; }
    }

    private async void PostReceiptAsync(object? sender, RoutedEventArgs e)
    {
        if (ReceiptItemComboBox.SelectedItem is not Choice item || string.IsNullOrWhiteSpace(item.Value) ||
            ReceiptWarehouseComboBox.SelectedItem is not Choice warehouse || string.IsNullOrWhiteSpace(warehouse.Value))
        { InventoryMessageTextBlock.Text = "اختر الصنف ومخزن الخامات أولاً."; return; }
        if (!DateOnly.TryParseExact(ReceiptDateTextBox.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
        { InventoryMessageTextBlock.Text = "أدخل التاريخ بالصيغة YYYY-MM-DD."; return; }
        if (!decimal.TryParse(ReceiptQuantityTextBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var quantity) || quantity <= 0 || decimal.Round(quantity, 4) != quantity)
        { InventoryMessageTextBlock.Text = "الكمية يجب أن تكون أكبر من صفر وبحد أقصى 4 خانات عشرية."; return; }
        if (!decimal.TryParse(ReceiptUnitCostTextBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var unitCost) || unitCost < 0 || decimal.Round(unitCost, 4) != unitCost)
        { InventoryMessageTextBlock.Text = "تكلفة الوحدة يجب ألا تكون سالبة وبحد أقصى 4 خانات عشرية."; return; }
        if (string.IsNullOrWhiteSpace(ReceiptReferenceTextBox.Text))
        { InventoryMessageTextBlock.Text = "رقم المستند / المرجع مطلوب."; return; }

        PostReceiptButton.IsEnabled = false;
        InventoryMessageTextBlock.Text = "جارٍ اعتماد استلام الخامات مركزياً…";
        try
        {
            var supplierId = (ReceiptSupplierComboBox.SelectedItem as Choice)?.Value;
            using var request = new HttpRequestMessage(HttpMethod.Post, "inventory/receipts")
            {
                Content = JsonContent.Create(new
                {
                    operationId = Guid.NewGuid(), itemId = item.Value, warehouseId = warehouse.Value,
                    supplierId = string.IsNullOrWhiteSpace(supplierId) ? null : supplierId,
                    documentReference = ReceiptReferenceTextBox.Text.Trim(), movementDate = date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    quantity, unitCost, notes = ReceiptNotesTextBox.Text?.Trim()
                }, options: JsonOptions)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await HttpClient.SendAsync(request);
            await EnsureApiSuccessAsync(response);
            ReceiptReferenceTextBox.Text = ReceiptQuantityTextBox.Text = ReceiptUnitCostTextBox.Text = ReceiptNotesTextBox.Text = string.Empty;
            await LoadInventoryAsync();
            InventoryMessageTextBlock.Text = "تم اعتماد استلام الخامات وتحديث الرصيد ومتوسط التكلفة.";
        }
        catch (Exception ex) { InventoryMessageTextBlock.Text = FriendlyError(ex); }
        finally { PostReceiptButton.IsEnabled = true; }
    }

    private async void ShowOrdersAsync(object? sender, RoutedEventArgs e)
    {
        HomePanel.IsVisible = false;
        MasterDataPanel.IsVisible = false;
        InventoryPanel.IsVisible = false;
        OrdersPanel.IsVisible = true;
        OrderDateTextBox.Text = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        ProductionDateTextBox.Text = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        await LoadMasterDataAsync();
        await LoadOrdersAsync();
    }

    private async void RefreshOrdersAsync(object? sender, RoutedEventArgs e)
    {
        await LoadMasterDataAsync();
        await LoadOrdersAsync();
    }

    private async void SelectedOrderChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!OrdersPanel.IsVisible || isLoadingOrders) return;
        await LoadEngineeringPackagesAsync();
        await LoadProductionOrdersAsync();
    }

    private async void EngineeringPackageChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!OrdersPanel.IsVisible || isLoadingPackages) return;
        await LoadEngineeringPackageDetailsAsync();
    }

    private async Task LoadOrdersAsync(string? selectOrderId = null)
    {
        if (string.IsNullOrWhiteSpace(accessToken)) return;
        var previousOrderId = selectOrderId ?? (SelectedOrderComboBox.SelectedItem as CustomerOrderChoice)?.Id;
        isLoadingOrders = true;
        RefreshOrdersButton.IsEnabled = false;
        OrdersMessageTextBlock.Text = "جارٍ تحميل طلبات العملاء…";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "orders");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await HttpClient.SendAsync(request);
            await EnsureApiSuccessAsync(response);
            currentOrders = await response.Content.ReadFromJsonAsync<List<CustomerOrderView>>(JsonOptions) ?? [];

            var customerChoices = (currentMasterData?.Parties ?? [])
                .Where(x => x.PartyType is "customer" or "both")
                .Select(x => new Choice(x.Id, $"{x.Code} — {x.NameAr}"))
                .ToList();
            OrderCustomerComboBox.ItemsSource = customerChoices;
            if (OrderCustomerComboBox.SelectedIndex < 0 && customerChoices.Count > 0)
                OrderCustomerComboBox.SelectedIndex = 0;

            SelectedOrderComboBox.ItemsSource = currentOrders.Select(x => new CustomerOrderChoice(
                x.Id, $"{x.OrderNo} — {x.CustomerNameAr} ({x.OrderDate})")).ToList();
            var desiredIndex = previousOrderId is null
                ? -1
                : currentOrders.FindIndex(x => x.Id == previousOrderId);
            if (desiredIndex >= 0) SelectedOrderComboBox.SelectedIndex = desiredIndex;
            else if (currentOrders.Count > 0) SelectedOrderComboBox.SelectedIndex = 0;
            else SelectedOrderComboBox.SelectedIndex = -1;

            OrdersListTextBlock.Text = currentOrders.Count == 0
                ? "لا توجد طلبات بعد. أنشئ طلب العميل من النموذج أعلاه."
                : string.Join(Environment.NewLine, currentOrders.Select(x =>
                    $"{x.OrderNo} | العميل: {x.CustomerCode} — {x.CustomerNameAr} | تاريخ الطلب: {x.OrderDate}" +
                    (string.IsNullOrWhiteSpace(x.RequestedDeliveryDate) ? "" : $" | التسليم المطلوب: {x.RequestedDeliveryDate}") +
                    $" | الحالة: {x.Status} | النسخ الفنية: {x.RevisionCount} | أمر إنتاج: {x.ProductionOrderCount}" +
                    (x.CurrentRevision is null ? " | لا توجد نسخة فنية مثبتة" : $" | النسخة الفنية المثبتة: R{x.CurrentRevision}")));

            OrdersMessageTextBlock.Text = "تم تحميل الطلبات.";
        }
        catch (Exception ex)
        {
            OrdersMessageTextBlock.Text = $"تعذر تحميل الطلبات: {FriendlyError(ex)}";
        }
        finally
        {
            isLoadingOrders = false;
            RefreshOrdersButton.IsEnabled = true;
        }

        await LoadEngineeringPackagesAsync();
        await LoadProductionOrdersAsync();
    }

    private async void CreateCustomerOrderAsync(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            OrdersMessageTextBlock.Text = "انتهت الجلسة. سجّل الدخول مرة أخرى.";
            return;
        }
        if (OrderCustomerComboBox.SelectedItem is not Choice customer || string.IsNullOrWhiteSpace(customer.Value))
        {
            OrdersMessageTextBlock.Text = "اختر عميلاً أولاً. أضف العميل من البيانات الأساسية إذا لم يكن مسجلاً.";
            return;
        }
        if (string.IsNullOrWhiteSpace(OrderNumberTextBox.Text))
        {
            OrdersMessageTextBlock.Text = "رقم الطلب مطلوب.";
            return;
        }
        if (!DateOnly.TryParseExact(OrderDateTextBox.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var orderDate))
        {
            OrdersMessageTextBlock.Text = "أدخل تاريخ الطلب بالصيغة YYYY-MM-DD.";
            return;
        }
        string? deliveryDate = null;
        if (!string.IsNullOrWhiteSpace(OrderDeliveryDateTextBox.Text))
        {
            if (!DateOnly.TryParseExact(OrderDeliveryDateTextBox.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedDeliveryDate))
            {
                OrdersMessageTextBlock.Text = "أدخل تاريخ التسليم بالصيغة YYYY-MM-DD أو اتركه فارغاً.";
                return;
            }
            if (parsedDeliveryDate < orderDate)
            {
                OrdersMessageTextBlock.Text = "تاريخ التسليم المطلوب لا يمكن أن يسبق تاريخ الطلب.";
                return;
            }
            deliveryDate = parsedDeliveryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        CreateOrderButton.IsEnabled = false;
        OrdersMessageTextBlock.Text = "جارٍ حفظ طلب العميل…";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, "orders")
            {
                Content = JsonContent.Create(new
                {
                    orderNo = OrderNumberTextBox.Text.Trim(),
                    customerId = customer.Value,
                    orderDate = orderDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    requestedDeliveryDate = deliveryDate,
                    notes = OrderNotesTextBox.Text?.Trim()
                }, options: JsonOptions)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await HttpClient.SendAsync(request);
            await EnsureApiSuccessAsync(response);
            var created = await response.Content.ReadFromJsonAsync<CreatedOrder>(JsonOptions)
                ?? throw new InvalidOperationException("استجابة حفظ الطلب فارغة.");
            OrderNumberTextBox.Text = OrderDeliveryDateTextBox.Text = OrderNotesTextBox.Text = string.Empty;
            OrderDateTextBox.Text = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            await LoadOrdersAsync(created.Id);
            OrdersMessageTextBlock.Text = $"تم حفظ طلب العميل {created.OrderNo}.";
        }
        catch (Exception ex) { OrdersMessageTextBlock.Text = FriendlyError(ex); }
        finally { CreateOrderButton.IsEnabled = true; }
    }

    private async Task LoadEngineeringPackagesAsync(string? selectPackageId = null)
    {
        var order = SelectedOrderComboBox.SelectedItem as CustomerOrderChoice;
        isLoadingPackages = true;
        try
        {
            if (string.IsNullOrWhiteSpace(accessToken) || order is null)
            {
                currentPackages = [];
                EngineeringPackageComboBox.ItemsSource = currentPackages;
                EngineeringPackageComboBox.SelectedIndex = -1;
                EngineeringPackageFileComboBox.ItemsSource = Array.Empty<EngineeringFileChoice>();
                EngineeringCutListFileComboBox.ItemsSource = Array.Empty<EngineeringFileChoice>();
                EngineeringCutListPartsTextBlock.Text = "اختر نسخة فنية لعرض تفاصيل الكاتلست.";
                AddEngineeringCutListPartButton.IsEnabled = false;
                EngineeringLinesTextBlock.Text = "اختر طلباً لعرض نسخه الفنية.";
                EngineeringPackageSummaryTextBlock.Text = string.Empty;
                AddEngineeringLineButton.IsEnabled = UploadEngineeringFileButton.IsEnabled = FinalizeEngineeringPackageButton.IsEnabled = DownloadEngineeringFileButton.IsEnabled = false;
                return;
            }

            var previousPackageId = selectPackageId ?? (EngineeringPackageComboBox.SelectedItem as EngineeringPackageChoice)?.Id;
            using var request = new HttpRequestMessage(HttpMethod.Get, $"orders/{order.Id}/engineering-packages");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await HttpClient.SendAsync(request);
            await EnsureApiSuccessAsync(response);
            currentPackages = await response.Content.ReadFromJsonAsync<List<EngineeringPackageView>>(JsonOptions) ?? [];
            var choices = currentPackages.Select(x => new EngineeringPackageChoice(
                x.Id, x.Revision, x.Status,
                $"R{x.Revision} — {EngineeringStatusLabel(x.Status)} — {x.TechnicalReference}")).ToList();
            EngineeringPackageComboBox.ItemsSource = choices;
            var desiredIndex = previousPackageId is null ? -1 : choices.FindIndex(x => x.Id == previousPackageId);
            if (desiredIndex >= 0) EngineeringPackageComboBox.SelectedIndex = desiredIndex;
            else if (choices.Count > 0) EngineeringPackageComboBox.SelectedIndex = 0;
            else EngineeringPackageComboBox.SelectedIndex = -1;
        }
        catch (Exception ex)
        {
            OrdersMessageTextBlock.Text = $"تعذر تحميل النسخ الفنية: {FriendlyError(ex)}";
        }
        finally { isLoadingPackages = false; }
        await LoadEngineeringPackageDetailsAsync();
    }

    private async void CreateEngineeringPackageAsync(object? sender, RoutedEventArgs e)
    {
        var order = SelectedOrderComboBox.SelectedItem as CustomerOrderChoice;
        if (order is null) { OrdersMessageTextBlock.Text = "اختر طلب العميل أولاً."; return; }
        if (string.IsNullOrWhiteSpace(EngineeringReferenceTextBox.Text))
        { OrdersMessageTextBlock.Text = "مرجع الرسم أو الملف الفني مطلوب."; return; }
        CreateEngineeringPackageButton.IsEnabled = false;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"orders/{order.Id}/engineering-packages")
            {
                Content = JsonContent.Create(new
                {
                    technicalReference = EngineeringReferenceTextBox.Text.Trim(),
                    notes = EngineeringNotesTextBox.Text?.Trim()
                }, options: JsonOptions)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await HttpClient.SendAsync(request);
            await EnsureApiSuccessAsync(response);
            var created = await response.Content.ReadFromJsonAsync<EngineeringPackageView>(JsonOptions)
                ?? throw new InvalidOperationException("استجابة إنشاء النسخة الفنية فارغة.");
            EngineeringReferenceTextBox.Text = EngineeringNotesTextBox.Text = string.Empty;
            await LoadEngineeringPackagesAsync(created.Id);
            OrdersMessageTextBlock.Text = $"تم إنشاء النسخة الفنية R{created.Revision} كمسودة. أضف البنود والمرفقات ثم ثبّتها.";
        }
        catch (Exception ex) { OrdersMessageTextBlock.Text = FriendlyError(ex); }
        finally { CreateEngineeringPackageButton.IsEnabled = true; }
    }

    private async void AddEngineeringLineAsync(object? sender, RoutedEventArgs e)
    {
        if (EngineeringPackageComboBox.SelectedItem is not EngineeringPackageChoice package)
        { OrdersMessageTextBlock.Text = "اختر نسخة فنية أولاً."; return; }
        if (package.Status != "draft") { OrdersMessageTextBlock.Text = "النسخة مثبتة؛ أنشئ نسخة جديدة لإضافة بنود."; return; }
        if (EngineeringLineTypeComboBox.SelectedItem is not Choice lineType || string.IsNullOrWhiteSpace(EngineeringLineDescriptionTextBox.Text))
        { OrdersMessageTextBlock.Text = "اختر نوع البند وأدخل وصفه."; return; }
        if (!decimal.TryParse(EngineeringLineQuantityTextBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var quantity) || quantity <= 0 || decimal.Round(quantity, 4) != quantity)
        { OrdersMessageTextBlock.Text = "الكمية يجب أن تكون أكبر من صفر وبحد أقصى 4 خانات عشرية."; return; }
        if (string.IsNullOrWhiteSpace(EngineeringLineUnitTextBox.Text))
        { OrdersMessageTextBlock.Text = "وحدة القياس مطلوبة."; return; }

        AddEngineeringLineButton.IsEnabled = false;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"orders/engineering-packages/{package.Id}/lines")
            {
                Content = JsonContent.Create(new
                {
                    lineType = lineType.Value,
                    description = EngineeringLineDescriptionTextBox.Text.Trim(),
                    materialReference = EngineeringLineMaterialReferenceTextBox.Text?.Trim(),
                    quantity,
                    unitCode = EngineeringLineUnitTextBox.Text.Trim(),
                    dimensions = EngineeringLineDimensionsTextBox.Text?.Trim(),
                    notes = EngineeringLineNotesTextBox.Text?.Trim()
                }, options: JsonOptions)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await HttpClient.SendAsync(request);
            await EnsureApiSuccessAsync(response);
            EngineeringLineDescriptionTextBox.Text = EngineeringLineMaterialReferenceTextBox.Text = string.Empty;
            EngineeringLineQuantityTextBox.Text = EngineeringLineUnitTextBox.Text = string.Empty;
            EngineeringLineDimensionsTextBox.Text = EngineeringLineNotesTextBox.Text = string.Empty;
            await LoadEngineeringPackagesAsync(package.Id);
            OrdersMessageTextBlock.Text = "تم إضافة البند إلى النسخة الفنية.";
        }
        catch (Exception ex) { OrdersMessageTextBlock.Text = FriendlyError(ex); }
        finally { AddEngineeringLineButton.IsEnabled = true; }
    }

    private async Task LoadEngineeringPackageDetailsAsync()
    {
        if (string.IsNullOrWhiteSpace(accessToken) || EngineeringPackageComboBox.SelectedItem is not EngineeringPackageChoice selectedPackage)
        {
            EngineeringPackageSummaryTextBlock.Text = string.Empty;
            EngineeringLinesTextBlock.Text = "لا توجد نسخة فنية محددة.";
            EngineeringPackageFileComboBox.ItemsSource = Array.Empty<EngineeringFileChoice>();
            EngineeringCutListFileComboBox.ItemsSource = Array.Empty<EngineeringFileChoice>();
            EngineeringCutListPartsTextBlock.Text = "لا توجد نسخة فنية محددة.";
            AddEngineeringCutListPartButton.IsEnabled = false;
            AddEngineeringLineButton.IsEnabled = UploadEngineeringFileButton.IsEnabled = FinalizeEngineeringPackageButton.IsEnabled = DownloadEngineeringFileButton.IsEnabled = false;
            return;
        }

        try
        {
            var package = currentPackages.FirstOrDefault(x => x.Id == selectedPackage.Id);
            var isDraft = selectedPackage.Status == "draft";
            AddEngineeringLineButton.IsEnabled = UploadEngineeringFileButton.IsEnabled = FinalizeEngineeringPackageButton.IsEnabled = isDraft;
            EngineeringPackageSummaryTextBlock.Text = package is null
                ? $"النسخة R{selectedPackage.Revision} — {EngineeringStatusLabel(selectedPackage.Status)}"
                : $"النسخة R{package.Revision} — {EngineeringStatusLabel(package.Status)} | المرجع: {package.TechnicalReference} | البنود: {package.LineCount} | المرفقات: {package.FileCount}";

            using var linesRequest = new HttpRequestMessage(HttpMethod.Get, $"orders/engineering-packages/{selectedPackage.Id}/lines");
            linesRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var linesResponse = await HttpClient.SendAsync(linesRequest);
            await EnsureApiSuccessAsync(linesResponse);
            var lines = await linesResponse.Content.ReadFromJsonAsync<List<EngineeringLineView>>(JsonOptions) ?? [];
            EngineeringLinesTextBlock.Text = lines.Count == 0 ? "لا توجد بنود بعد." : string.Join(Environment.NewLine, lines.Select(x =>
                $"{x.LineNo}. [{(x.LineType == "bom" ? "BOM" : "قائمة قص")}] {x.Description} | {x.Quantity} {x.UnitCode}" +
                (string.IsNullOrWhiteSpace(x.MaterialReference) ? "" : $" | مرجع: {x.MaterialReference}") +
                (string.IsNullOrWhiteSpace(x.Dimensions) ? "" : $" | أبعاد: {x.Dimensions}") +
                (string.IsNullOrWhiteSpace(x.Notes) ? "" : $" | {x.Notes}")));

            using var filesRequest = new HttpRequestMessage(HttpMethod.Get, $"orders/engineering-packages/{selectedPackage.Id}/files");
            filesRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var filesResponse = await HttpClient.SendAsync(filesRequest);
            await EnsureApiSuccessAsync(filesResponse);
            var files = await filesResponse.Content.ReadFromJsonAsync<List<EngineeringFileView>>(JsonOptions) ?? [];
            var fileChoices = files.Select(x => new EngineeringFileChoice(
                x.Id, x.FileName, x.AttachmentType, x.MaterialCode, x.MaterialColor, x.ThicknessMm,
                x.BoardLengthMm, x.BoardWidthMm, x.BoardQuantity,
                $"{AttachmentTypeLabel(x.AttachmentType)} — {x.FileName}" +
                (x.AttachmentType == "cut_list" ? $" | {x.MaterialCode} | {x.MaterialColor} | {x.ThicknessMm} مم | {x.BoardLengthMm}×{x.BoardWidthMm} مم | ألواح: {x.BoardQuantity}" : "") +
                (string.IsNullOrWhiteSpace(x.Description) ? "" : $" | {x.Description}") + $" ({FormatFileSize(x.SizeBytes)})")).ToList();
            var previousFileId = (EngineeringPackageFileComboBox.SelectedItem as EngineeringFileChoice)?.Id;
            EngineeringPackageFileComboBox.ItemsSource = fileChoices;
            var fileIndex = previousFileId is null ? -1 : fileChoices.FindIndex(x => x.Id == previousFileId);
            if (fileIndex >= 0) EngineeringPackageFileComboBox.SelectedIndex = fileIndex;
            else if (fileChoices.Count > 0) EngineeringPackageFileComboBox.SelectedIndex = 0;

            var cutListChoices = fileChoices.Where(x => x.AttachmentType == "cut_list").ToList();
            var previousCutFileId = (EngineeringCutListFileComboBox.SelectedItem as EngineeringFileChoice)?.Id;
            EngineeringCutListFileComboBox.ItemsSource = cutListChoices;
            var cutIndex = previousCutFileId is null ? -1 : cutListChoices.FindIndex(x => x.Id == previousCutFileId);
            if (cutIndex >= 0) EngineeringCutListFileComboBox.SelectedIndex = cutIndex;
            else if (cutListChoices.Count > 0) EngineeringCutListFileComboBox.SelectedIndex = 0;
            DownloadEngineeringFileButton.IsEnabled = EngineeringPackageFileComboBox.ItemCount > 0;
            AddEngineeringCutListPartButton.IsEnabled = isDraft && cutListChoices.Count > 0;
            await LoadEngineeringCutListPartsAsync(selectedPackage.Id);
        }
        catch (Exception ex)
        {
            OrdersMessageTextBlock.Text = $"تعذر تحميل تفاصيل النسخة: {FriendlyError(ex)}";
        }
    }

    private async Task LoadEngineeringCutListPartsAsync(string packageId)
    {
        if (string.IsNullOrWhiteSpace(accessToken)) return;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"orders/engineering-packages/{packageId}/cut-list-parts");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await HttpClient.SendAsync(request);
            await EnsureApiSuccessAsync(response);
            var parts = await response.Content.ReadFromJsonAsync<List<EngineeringCutListPartView>>(JsonOptions) ?? [];
            EngineeringCutListPartsTextBlock.Text = parts.Count == 0
                ? "لا توجد قطع مسجلة من الكاتلست بعد. اختر مخطط كاتلست وأضف القطع ومقاساتها."
                : string.Join(Environment.NewLine, parts.Select(x =>
                    $"{x.FileName} | {x.LineNo}. {x.PartName}: {x.LengthMm} × {x.WidthMm} مم" +
                    (string.IsNullOrWhiteSpace(x.ThicknessMm) ? $" (سمك اللوح {x.BoardThicknessMm} مم)" : $" × {x.ThicknessMm} مم") +
                    $" | الكمية: {x.Quantity} | اللوح: {x.MaterialCode} — {x.MaterialColor} | مقاس اللوح: {x.BoardLengthMm}×{x.BoardWidthMm} مم | عدد الألواح: {x.BoardQuantity}" +
                    (string.IsNullOrWhiteSpace(x.Notes) ? "" : $" | {x.Notes}")));
        }
        catch (Exception ex)
        {
            EngineeringCutListPartsTextBlock.Text = $"تعذر تحميل تفاصيل الكاتلست: {FriendlyError(ex)}";
        }
    }

    private async void AddEngineeringCutListPartAsync(object? sender, RoutedEventArgs e)
    {
        if (EngineeringPackageComboBox.SelectedItem is not EngineeringPackageChoice package || package.Status != "draft")
        { OrdersMessageTextBlock.Text = "اختر نسخة فنية مسودة لإضافة تفاصيل القص."; return; }
        if (EngineeringCutListFileComboBox.SelectedItem is not EngineeringFileChoice file || file.AttachmentType != "cut_list")
        { OrdersMessageTextBlock.Text = "ارفع مخطط كاتلست وحدده أولاً."; return; }
        if (string.IsNullOrWhiteSpace(EngineeringCutListPartNameTextBox.Text))
        { OrdersMessageTextBlock.Text = "اسم قطعة القص مطلوب."; return; }
        if (!decimal.TryParse(EngineeringCutListPartLengthTextBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var length) || length <= 0 ||
            !decimal.TryParse(EngineeringCutListPartWidthTextBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var width) || width <= 0 ||
            !decimal.TryParse(EngineeringCutListPartQuantityTextBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var quantity) || quantity <= 0)
        { OrdersMessageTextBlock.Text = "أدخل الطول والعرض والكمية بقيم أكبر من صفر واستخدم النقطة للفواصل العشرية."; return; }

        decimal? thickness = null;
        if (!string.IsNullOrWhiteSpace(EngineeringCutListPartThicknessTextBox.Text))
        {
            if (!decimal.TryParse(EngineeringCutListPartThicknessTextBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedThickness) || parsedThickness <= 0)
            { OrdersMessageTextBlock.Text = "سمك القطعة، إن أدخلته، يجب أن يكون أكبر من صفر."; return; }
            thickness = parsedThickness;
        }

        AddEngineeringCutListPartButton.IsEnabled = false;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"orders/engineering-packages/{package.Id}/cut-list-parts")
            {
                Content = JsonContent.Create(new
                {
                    sourceFileId = file.Id,
                    partName = EngineeringCutListPartNameTextBox.Text.Trim(),
                    lengthMm = length,
                    widthMm = width,
                    thicknessMm = thickness,
                    quantity,
                    notes = EngineeringCutListPartNotesTextBox.Text?.Trim()
                }, options: JsonOptions)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await HttpClient.SendAsync(request);
            await EnsureApiSuccessAsync(response);
            EngineeringCutListPartNameTextBox.Text = EngineeringCutListPartLengthTextBox.Text = EngineeringCutListPartWidthTextBox.Text = string.Empty;
            EngineeringCutListPartThicknessTextBox.Text = EngineeringCutListPartQuantityTextBox.Text = EngineeringCutListPartNotesTextBox.Text = string.Empty;
            await LoadEngineeringCutListPartsAsync(package.Id);
            OrdersMessageTextBlock.Text = "تم حفظ قطعة القص وربطها بمخطط الكاتلست.";
        }
        catch (Exception ex) { OrdersMessageTextBlock.Text = FriendlyError(ex); }
        finally { AddEngineeringCutListPartButton.IsEnabled = package.Status == "draft" && EngineeringCutListFileComboBox.ItemCount > 0; }
    }

    private async void FinalizeEngineeringPackageAsync(object? sender, RoutedEventArgs e)
    {
        if (EngineeringPackageComboBox.SelectedItem is not EngineeringPackageChoice package)
        { OrdersMessageTextBlock.Text = "اختر نسخة فنية أولاً."; return; }
        if (package.Status != "draft") { OrdersMessageTextBlock.Text = "هذه النسخة ليست مسودة."; return; }
        FinalizeEngineeringPackageButton.IsEnabled = false;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"orders/engineering-packages/{package.Id}/finalize");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await HttpClient.SendAsync(request);
            await EnsureApiSuccessAsync(response);
            await LoadEngineeringPackagesAsync(package.Id);
            await LoadOrdersAsync((SelectedOrderComboBox.SelectedItem as CustomerOrderChoice)?.Id);
            OrdersMessageTextBlock.Text = $"تم تثبيت النسخة الفنية R{package.Revision}. أي تغيير لاحق يجب أن يكون في نسخة جديدة.";
        }
        catch (Exception ex) { OrdersMessageTextBlock.Text = FriendlyError(ex); }
        finally { FinalizeEngineeringPackageButton.IsEnabled = true; }
    }

    private async void UploadEngineeringFileAsync(object? sender, RoutedEventArgs e)
    {
        if (EngineeringPackageComboBox.SelectedItem is not EngineeringPackageChoice package || package.Status != "draft")
        { OrdersMessageTextBlock.Text = "اختر نسخة فنية مسودة قبل رفع المرفقات."; return; }
        if (EngineeringAttachmentTypeComboBox.SelectedItem is not Choice attachmentType)
        { OrdersMessageTextBlock.Text = "اختر نوع المرفق أولاً."; return; }
        if (attachmentType.Value == "cut_list")
        {
            bool Positive(string? text) => decimal.TryParse(text, NumberStyles.Number, CultureInfo.InvariantCulture, out var number) && number > 0;
            if (string.IsNullOrWhiteSpace(EngineeringMaterialCodeTextBox.Text) || string.IsNullOrWhiteSpace(EngineeringMaterialColorTextBox.Text) ||
                !Positive(EngineeringBoardThicknessTextBox.Text) || !Positive(EngineeringBoardLengthTextBox.Text) ||
                !Positive(EngineeringBoardWidthTextBox.Text) || !Positive(EngineeringBoardQuantityTextBox.Text))
            { OrdersMessageTextBlock.Text = "للكاتلست، أدخل نوع اللوح واللون والسمك وطول اللوح وعرضه وعدد الألواح بشكل صحيح."; return; }
        }
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null) { OrdersMessageTextBlock.Text = "اختيار الملفات غير متاح في هذه الجلسة."; return; }
        var picked = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "اختيار ملف فني",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("Engineering documents") { Patterns = new[] { "*.pdf", "*.png", "*.jpg", "*.jpeg", "*.webp", "*.xlsx", "*.csv", "*.dwg", "*.dxf", "*.doc", "*.docx", "*.zip" } }
            }
        });
        var file = picked.FirstOrDefault();
        if (file is null) return;

        UploadEngineeringFileButton.IsEnabled = false;
        try
        {
            await using var fileStream = await file.OpenReadAsync();
            using var multipart = new MultipartFormDataContent();
            using var streamContent = new StreamContent(fileStream);
            streamContent.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
            multipart.Add(streamContent, "file", file.Name);
            void AddMetadata(string name, string? value)
            {
                if (!string.IsNullOrWhiteSpace(value)) multipart.Add(new StringContent(value.Trim()), name);
            }
            AddMetadata("attachmentType", attachmentType.Value);
            AddMetadata("description", EngineeringAttachmentDescriptionTextBox.Text);
            AddMetadata("materialCode", EngineeringMaterialCodeTextBox.Text);
            AddMetadata("materialColor", EngineeringMaterialColorTextBox.Text);
            AddMetadata("thicknessMm", EngineeringBoardThicknessTextBox.Text);
            AddMetadata("boardLengthMm", EngineeringBoardLengthTextBox.Text);
            AddMetadata("boardWidthMm", EngineeringBoardWidthTextBox.Text);
            AddMetadata("boardQuantity", EngineeringBoardQuantityTextBox.Text);
            using var request = new HttpRequestMessage(HttpMethod.Post, $"orders/engineering-packages/{package.Id}/files") { Content = multipart };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await HttpClient.SendAsync(request);
            await EnsureApiSuccessAsync(response);
            await LoadEngineeringPackagesAsync(package.Id);
            OrdersMessageTextBlock.Text = $"تم رفع المرفق: {file.Name}";
        }
        catch (Exception ex) { OrdersMessageTextBlock.Text = FriendlyError(ex); }
        finally { UploadEngineeringFileButton.IsEnabled = true; }
    }

    private async void DownloadEngineeringFileAsync(object? sender, RoutedEventArgs e)
    {
        if (EngineeringPackageComboBox.SelectedItem is not EngineeringPackageChoice package ||
            EngineeringPackageFileComboBox.SelectedItem is not EngineeringFileChoice file)
        { OrdersMessageTextBlock.Text = "اختر مرفقاً لتنزيله."; return; }
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null) { OrdersMessageTextBlock.Text = "حفظ الملفات غير متاح في هذه الجلسة."; return; }
        var destination = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "حفظ الملف الفني",
            SuggestedFileName = file.FileName
        });
        if (destination is null) return;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"orders/engineering-packages/{package.Id}/files/{file.Id}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await HttpClient.SendAsync(request);
            await EnsureApiSuccessAsync(response);
            await using var source = await response.Content.ReadAsStreamAsync();
            await using var target = await destination.OpenWriteAsync();
            await source.CopyToAsync(target);
            OrdersMessageTextBlock.Text = $"تم حفظ المرفق في: {destination.Name}";
        }
        catch (Exception ex) { OrdersMessageTextBlock.Text = FriendlyError(ex); }
    }

    private async void CreateDraftProductionOrderAsync(object? sender, RoutedEventArgs e)
    {
        if (SelectedOrderComboBox.SelectedItem is not CustomerOrderChoice order)
        { OrdersMessageTextBlock.Text = "اختر طلب العميل أولاً."; return; }
        if (EngineeringPackageComboBox.SelectedItem is not EngineeringPackageChoice package || package.Status != "finalized")
        { OrdersMessageTextBlock.Text = "اختر نسخة فنية مثبتة أولاً."; return; }
        if (string.IsNullOrWhiteSpace(ProductionOrderNumberTextBox.Text) ||
            string.IsNullOrWhiteSpace(ProductionProductCodeTextBox.Text) ||
            string.IsNullOrWhiteSpace(ProductionProductNameTextBox.Text))
        { OrdersMessageTextBlock.Text = "رقم أمر الشغل وكود المنتج واسم المنتج مطلوبة."; return; }
        if (!DateOnly.TryParseExact(ProductionDateTextBox.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var productionDate))
        { OrdersMessageTextBlock.Text = "أدخل تاريخ أمر الشغل بالصيغة YYYY-MM-DD."; return; }
        if (!decimal.TryParse(ProductionPlannedQuantityTextBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var plannedQuantity) || plannedQuantity <= 0 || decimal.Round(plannedQuantity, 4) != plannedQuantity)
        { OrdersMessageTextBlock.Text = "الكمية المطلوبة يجب أن تكون أكبر من صفر وبحد أقصى 4 خانات عشرية."; return; }

        CreateProductionOrderButton.IsEnabled = false;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"orders/{order.Id}/production-orders")
            {
                Content = JsonContent.Create(new
                {
                    orderNo = ProductionOrderNumberTextBox.Text.Trim(),
                    engineeringPackageId = package.Id,
                    productCode = ProductionProductCodeTextBox.Text.Trim(),
                    productName = ProductionProductNameTextBox.Text.Trim(),
                    productionDate = productionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    plannedQuantity
                }, options: JsonOptions)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await HttpClient.SendAsync(request);
            await EnsureApiSuccessAsync(response);
            var created = await response.Content.ReadFromJsonAsync<CreatedProductionOrder>(JsonOptions)
                ?? throw new InvalidOperationException("استجابة إنشاء أمر الشغل فارغة.");
            ProductionOrderNumberTextBox.Text = ProductionProductCodeTextBox.Text = ProductionPlannedQuantityTextBox.Text = string.Empty;
            ProductionDateTextBox.Text = DateTime.Today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            await LoadProductionOrdersAsync(created.Id);
            OrdersMessageTextBlock.Text = $"تم إنشاء أمر الشغل المسودة {created.OrderNo} وربطه بالنسخة الفنية R{package.Revision}. أكمل البنود ثم احفظ النموذج.";
            ProductionWorkOrderMessageTextBlock.Text = "أدخل كميات ومواصفات البنود ثم اضغط حفظ أمر الشغل.";
        }
        catch (Exception ex) { OrdersMessageTextBlock.Text = FriendlyError(ex); }
        finally { CreateProductionOrderButton.IsEnabled = true; }
    }

    private async void ProductionOrderChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!OrdersPanel.IsVisible || isLoadingProductionOrders || isLoadingProductionWorkOrder) return;
        await LoadProductionWorkOrderAsync();
    }

    private async Task LoadProductionOrdersAsync(string? selectProductionOrderId = null)
    {
        if (string.IsNullOrWhiteSpace(accessToken)) return;
        var previousId = selectProductionOrderId ?? (ProductionOrderComboBox.SelectedItem as ProductionOrderChoice)?.Id;
        isLoadingProductionOrders = true;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "orders/production-orders");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await HttpClient.SendAsync(request);
            await EnsureApiSuccessAsync(response);
            currentProductionOrders = await response.Content.ReadFromJsonAsync<List<ProductionOrderView>>(JsonOptions) ?? [];

            ProductionOrdersTextBlock.Text = currentProductionOrders.Count == 0
                ? "لا توجد أوامر إنتاج بعد. أنشئ أمر شغل من النموذج أعلاه."
                : string.Join(Environment.NewLine, currentProductionOrders.Select(x =>
                    $"{x.OrderNo} | طلب العميل: {x.CustomerOrderNo ?? "إنتاج للمخزون"} | المنتج: {x.ProductCode} — {x.ProductName} | الكمية: {x.PlannedQuantity} | التاريخ: {x.ProductionDate} | النسخة الفنية: {(x.EngineeringRevision is null ? "غير محددة" : $"R{x.EngineeringRevision}")} | الحالة: {x.Status}"));

            var selectedCustomerOrderId = (SelectedOrderComboBox.SelectedItem as CustomerOrderChoice)?.Id;
            var relevantOrders = selectedCustomerOrderId is null
                ? currentProductionOrders
                : currentProductionOrders.Where(x => x.CustomerOrderId == selectedCustomerOrderId).ToList();
            ProductionOrderComboBox.ItemsSource = relevantOrders.Select(x => new ProductionOrderChoice(
                x.Id, $"{x.OrderNo} — {x.ProductCode} — {x.ProductName} — {x.Status}")).ToList();
            var desiredIndex = previousId is null ? -1 : relevantOrders.FindIndex(x => x.Id == previousId);
            if (desiredIndex >= 0) ProductionOrderComboBox.SelectedIndex = desiredIndex;
            else if (relevantOrders.Count > 0) ProductionOrderComboBox.SelectedIndex = 0;
            else ProductionOrderComboBox.SelectedIndex = -1;
        }
        catch (Exception ex)
        {
            ProductionOrdersTextBlock.Text = $"تعذر تحميل أوامر الإنتاج: {FriendlyError(ex)}";
            ProductionWorkOrderMessageTextBlock.Text = FriendlyError(ex);
        }
        finally { isLoadingProductionOrders = false; }

        if (ProductionOrderComboBox.SelectedItem is ProductionOrderChoice)
            await LoadProductionWorkOrderAsync();
        else
            ClearProductionWorkOrderEditor();
    }

    private async Task<ProductionWorkOrderResponse?> FetchProductionWorkOrderAsync(string productionOrderId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, $"orders/production-orders/{productionOrderId}/work-order");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await HttpClient.SendAsync(request);
        await EnsureApiSuccessAsync(response);
        return await response.Content.ReadFromJsonAsync<ProductionWorkOrderResponse>(JsonOptions);
    }

    private async Task LoadProductionWorkOrderAsync(string? productionOrderId = null)
    {
        var selected = productionOrderId is null ? ProductionOrderComboBox.SelectedItem as ProductionOrderChoice : null;
        var id = productionOrderId ?? selected?.Id;
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(id))
        {
            ClearProductionWorkOrderEditor();
            return;
        }

        isLoadingProductionWorkOrder = true;
        try
        {
            var result = await FetchProductionWorkOrderAsync(id)
                ?? throw new InvalidOperationException("استجابة أمر الشغل فارغة.");
            ProductionProductNameTextBox.Text = result.ProductionOrder.ProductName;
            ProductionDateTextBox.Text = result.ProductionOrder.ProductionDate;
            var po = result.ProductionOrder;
            ProductionWorkOrderSummaryTextBlock.Text =
                $"رقم الأمر: {po.OrderNo} | المنتج: {po.ProductCode} — {po.ProductName} | الكمية: {po.PlannedQuantity} | التاريخ: {po.ProductionDate} | " +
                $"الطلب: {po.CustomerOrderNo ?? "إنتاج للمخزون"} | النسخة الفنية: {(po.EngineeringRevision is null ? "غير محددة" : $"R{po.EngineeringRevision}")} | الحالة: {ProductionStatusLabel(po.Status)}";
            RenderProductionWorkOrderLines(result.Lines);
            SaveProductionWorkOrderButton.IsEnabled = po.Status == "draft";
            DownloadProductionWorkOrderPdfButton.IsEnabled = true;
            ProductionWorkOrderMessageTextBlock.Text = po.Status == "draft"
                ? "عدّل المواصفات والكميات ثم احفظ. ملف PDF يُنشأ من البيانات المحفوظة."
                : "أمر الشغل غير مسودة؛ تنزيل PDF متاح، والتعديل غير متاح بعد بدء التنفيذ.";
        }
        catch (Exception ex)
        {
            ProductionWorkOrderMessageTextBlock.Text = $"تعذر تحميل أمر الشغل: {FriendlyError(ex)}";
            SaveProductionWorkOrderButton.IsEnabled = false;
            DownloadProductionWorkOrderPdfButton.IsEnabled = false;
        }
        finally { isLoadingProductionWorkOrder = false; }
    }

    private void ClearProductionWorkOrderEditor()
    {
        if (ProductionWorkOrderLinesPanel is not null) ProductionWorkOrderLinesPanel.Children.Clear();
        currentWorkOrderLineEditors = [];
        if (ProductionWorkOrderSummaryTextBlock is not null) ProductionWorkOrderSummaryTextBlock.Text = "اختر أمر إنتاج مسودة لعرض نموذج أمر الشغل.";
        if (ProductionWorkOrderMessageTextBlock is not null) ProductionWorkOrderMessageTextBlock.Text = string.Empty;
        if (SaveProductionWorkOrderButton is not null) SaveProductionWorkOrderButton.IsEnabled = false;
        if (DownloadProductionWorkOrderPdfButton is not null) DownloadProductionWorkOrderPdfButton.IsEnabled = false;
    }

    private void RenderProductionWorkOrderLines(List<ProductionWorkOrderLineView> lines)
    {
        ProductionWorkOrderLinesPanel.Children.Clear();
        currentWorkOrderLineEditors = [];
        // Explicit colors keep generated work-order rows readable in dark Windows themes.
        var workOrderTextBrush = new SolidColorBrush(Color.Parse("#172033"));
        var workOrderInputBrush = new SolidColorBrush(Color.Parse("#FFFFFF"));
        var workOrderBorderBrush = new SolidColorBrush(Color.Parse("#94A3B8"));
        var customHeaderAdded = false;
        var visualRow = 0;
        foreach (var line in lines.OrderBy(x => x.LineNo))
        {
            if (line.IsCustom && !customHeaderAdded)
            {
                ProductionWorkOrderLinesPanel.Children.Add(BuildCustomWorkOrderHeader());
                customHeaderAdded = true;
            }

            var custom = line.IsCustom;
            var grid = new Grid { ColumnSpacing = 6, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(95)));
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(105)));
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(4, GridUnitType.Star)));
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(2, GridUnitType.Star)));
            grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(38)));

            var quantityBox = new TextBox { Foreground = workOrderTextBrush, Background = workOrderInputBrush, BorderBrush = workOrderBorderBrush, Text = line.Quantity ?? string.Empty, PlaceholderText = "العدد", MinWidth = 60, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            var specificationBox = new TextBox { Foreground = workOrderTextBrush, Background = workOrderInputBrush, BorderBrush = workOrderBorderBrush, Text = line.Specification ?? string.Empty, PlaceholderText = "المقاس / البيان", MinWidth = 100, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
            TextBox? itemNameBox = null;
            TextBlock? itemNameLabel = null;
            TextBox? unitBox = null;
            TextBlock? unitLabel = null;
            CheckBox? selectedCheckBox = null;

            if (custom)
            {
                itemNameBox = new TextBox { Foreground = workOrderTextBrush, Background = workOrderInputBrush, BorderBrush = workOrderBorderBrush, Text = line.ItemNameAr ?? string.Empty, PlaceholderText = "اسم البند الإضافي", MinWidth = 100, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
                unitBox = new TextBox { Foreground = workOrderTextBrush, Background = workOrderInputBrush, BorderBrush = workOrderBorderBrush, Text = line.UnitCode ?? string.Empty, PlaceholderText = "الوحدة", MinWidth = 70, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
                var itemNumber = new TextBlock { Foreground = workOrderTextBrush, Text = (line.LineNo - lines.Count(x => !x.IsCustom)).ToString(CultureInfo.InvariantCulture), VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
                grid.Children.Add(quantityBox); Grid.SetColumn(quantityBox, 0);
                grid.Children.Add(unitBox); Grid.SetColumn(unitBox, 1);
                grid.Children.Add(specificationBox); Grid.SetColumn(specificationBox, 2);
                grid.Children.Add(itemNameBox); Grid.SetColumn(itemNameBox, 3);
                grid.Children.Add(itemNumber); Grid.SetColumn(itemNumber, 4);
            }
            else
            {
                itemNameLabel = new TextBlock { Foreground = workOrderTextBrush, Text = line.ItemNameAr, FontWeight = FontWeight.SemiBold, TextWrapping = TextWrapping.Wrap, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right };
                unitLabel = new TextBlock { Foreground = workOrderTextBrush, Text = line.UnitCode, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
                selectedCheckBox = new CheckBox { Foreground = workOrderTextBrush, Background = workOrderInputBrush, BorderBrush = workOrderBorderBrush, IsChecked = line.IsSelected, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center, HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center };
                grid.Children.Add(quantityBox); Grid.SetColumn(quantityBox, 0);
                grid.Children.Add(unitLabel); Grid.SetColumn(unitLabel, 1);
                grid.Children.Add(specificationBox); Grid.SetColumn(specificationBox, 2);
                grid.Children.Add(itemNameLabel); Grid.SetColumn(itemNameLabel, 3);
                grid.Children.Add(selectedCheckBox); Grid.SetColumn(selectedCheckBox, 4);
            }

            var rowBorder = new Border
            {
                Padding = new Avalonia.Thickness(5, 3),
                BorderBrush = new SolidColorBrush(Color.Parse("#D8E0EB")),
                BorderThickness = new Avalonia.Thickness(0, 0, 0, 1),
                Background = new SolidColorBrush(Color.Parse(visualRow++ % 2 == 0 ? "#FFFFFF" : "#F6F8FB")),
                Child = grid
            };
            ProductionWorkOrderLinesPanel.Children.Add(rowBorder);
            currentWorkOrderLineEditors.Add(new WorkOrderLineEditor(line, itemNameBox, itemNameLabel, specificationBox, unitBox, unitLabel, quantityBox, selectedCheckBox));
        }
    }

    private static Border BuildCustomWorkOrderHeader()
    {
        var grid = new Grid { ColumnSpacing = 6, VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center };
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(95)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(105)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(4, GridUnitType.Star)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(2, GridUnitType.Star)));
        grid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(38)));
        AddHeaderCell(grid, 0, "العدد");
        AddHeaderCell(grid, 1, "الوحدة");
        AddHeaderCell(grid, 2, "المواصفة / البيان");
        AddHeaderCell(grid, 3, "إضافات أخرى (اكتب البند حسب الحاجة)");
        AddHeaderCell(grid, 4, "م");
        return new Border
        {
            Padding = new Avalonia.Thickness(5),
            Background = new SolidColorBrush(Color.Parse("#11786F")),
            CornerRadius = new Avalonia.CornerRadius(3),
            Child = grid
        };
    }

    private static void AddHeaderCell(Grid grid, int column, string text)
    {
        var cell = new TextBlock
        {
            Text = text,
            Foreground = Brushes.White,
            FontWeight = FontWeight.SemiBold,
            HorizontalAlignment = column is 0 or 1 or 4 ? Avalonia.Layout.HorizontalAlignment.Center : Avalonia.Layout.HorizontalAlignment.Right,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            TextWrapping = TextWrapping.Wrap
        };
        grid.Children.Add(cell);
        Grid.SetColumn(cell, column);
    }

    private async void SaveProductionWorkOrderAsync(object? sender, RoutedEventArgs e)
    {
        await SaveProductionWorkOrderCoreAsync();
    }

    private async Task<bool> SaveProductionWorkOrderCoreAsync()
    {
        if (ProductionOrderComboBox.SelectedItem is not ProductionOrderChoice selected)
        {
            ProductionWorkOrderMessageTextBlock.Text = "اختر أمر شغل أولاً.";
            return false;
        }
        var order = currentProductionOrders.FirstOrDefault(x => x.Id == selected.Id);
        if (order is null || order.Status != "draft")
        {
            ProductionWorkOrderMessageTextBlock.Text = "يمكن تعديل أمر الشغل عندما يكون أمر الإنتاج في حالة مسودة فقط.";
            return false;
        }
        if (string.IsNullOrWhiteSpace(ProductionProductNameTextBox.Text))
        {
            ProductionWorkOrderMessageTextBlock.Text = "اسم المنتج مطلوب.";
            return false;
        }
        if (!DateOnly.TryParseExact(ProductionDateTextBox.Text, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var productionDate))
        {
            ProductionWorkOrderMessageTextBlock.Text = "أدخل التاريخ بالصيغة YYYY-MM-DD.";
            return false;
        }

        var linePayloads = new List<WorkOrderLinePayload>();
        foreach (var editor in currentWorkOrderLineEditors)
        {
            if (!editor.TryCreatePayload(out var payload, out var error))
            {
                ProductionWorkOrderMessageTextBlock.Text = error;
                return false;
            }
            linePayloads.Add(payload!);
        }

        SaveProductionWorkOrderButton.IsEnabled = false;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Put, $"orders/production-orders/{selected.Id}/work-order")
            {
                Content = JsonContent.Create(new
                {
                    productName = ProductionProductNameTextBox.Text.Trim(),
                    productionDate = productionDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    lines = linePayloads
                }, options: JsonOptions)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await HttpClient.SendAsync(request);
            await EnsureApiSuccessAsync(response);
            var saved = await response.Content.ReadFromJsonAsync<ProductionWorkOrderResponse>(JsonOptions)
                ?? throw new InvalidOperationException("استجابة حفظ أمر الشغل فارغة.");
            RenderProductionWorkOrderLines(saved.Lines);
            ProductionWorkOrderSummaryTextBlock.Text = BuildProductionWorkOrderSummary(saved.ProductionOrder);
            ProductionWorkOrderMessageTextBlock.Text = "تم حفظ أمر الشغل وبنوده. يمكنك الآن تنزيل PDF.";
            return true;
        }
        catch (Exception ex)
        {
            ProductionWorkOrderMessageTextBlock.Text = FriendlyError(ex);
            return false;
        }
        finally { SaveProductionWorkOrderButton.IsEnabled = true; }
    }

    private async void DownloadProductionWorkOrderPdfAsync(object? sender, RoutedEventArgs e)
    {
        if (ProductionOrderComboBox.SelectedItem is not ProductionOrderChoice selected)
        {
            ProductionWorkOrderMessageTextBlock.Text = "اختر أمر شغل أولاً.";
            return;
        }
        var existing = currentProductionOrders.FirstOrDefault(x => x.Id == selected.Id);
        if (existing?.Status == "draft" && !await SaveProductionWorkOrderCoreAsync()) return;

        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null)
        {
            ProductionWorkOrderMessageTextBlock.Text = "حفظ الملفات غير متاح في هذه الجلسة.";
            return;
        }

        try
        {
            var result = await FetchProductionWorkOrderAsync(selected.Id)
                ?? throw new InvalidOperationException("تعذر تحميل بيانات أمر الشغل لإنشاء PDF.");
            var po = result.ProductionOrder;
            var pdfModel = new ProductionWorkOrderPdfModel(
                po.OrderNo,
                po.ProductionDate,
                po.ProductCode,
                po.ProductName,
                po.PlannedQuantity,
                po.CustomerOrderNo,
                po.EngineeringRevision,
                result.Lines.OrderBy(x => x.LineNo).Select(x => new ProductionWorkOrderPdfLine(
                    x.CatalogCode, x.ItemNameAr ?? string.Empty, x.Specification ?? string.Empty,
                    x.UnitCode ?? string.Empty, x.Quantity, x.IsSelected, x.IsCustom)).ToList());
            var pdfBytes = ProductionWorkOrderPdf.Generate(pdfModel);
            var safeOrderNo = string.Concat(po.OrderNo.Select(c => Path.GetInvalidFileNameChars().Contains(c) ? '_' : c));
            var destination = await storage.SaveFilePickerAsync(new FilePickerSaveOptions
            {
                Title = "حفظ أمر الشغل PDF",
                SuggestedFileName = $"Job-Order-{safeOrderNo}.pdf",
                FileTypeChoices = new List<FilePickerFileType>
                {
                    new("PDF") { Patterns = new[] { "*.pdf" } }
                }
            });
            if (destination is null) return;
            await using var target = await destination.OpenWriteAsync();
            await target.WriteAsync(pdfBytes);
            ProductionWorkOrderMessageTextBlock.Text = $"تم إنشاء وحفظ ملف PDF: {destination.Name}";
        }
        catch (Exception ex)
        {
                        var diagnosticPath = Path.Combine(Path.GetTempPath(), "wood-factory-pdf-error.txt");
            try
            {
                File.WriteAllText(diagnosticPath, ex.ToString(), new System.Text.UTF8Encoding(false));
            }
            catch
            {
                diagnosticPath = string.Empty;
            }

            var details = $"تعذر إنشاء PDF ({ex.GetType().Name}): {ex.Message}";
            ProductionWorkOrderMessageTextBlock.Text = string.IsNullOrWhiteSpace(diagnosticPath)
                ? details
                : $"{details} | التفاصيل: {diagnosticPath}";
        }
    }

    private static string BuildProductionWorkOrderSummary(ProductionWorkOrderDetails po) =>
        $"رقم الأمر: {po.OrderNo} | المنتج: {po.ProductCode} — {po.ProductName} | الكمية: {po.PlannedQuantity} | التاريخ: {po.ProductionDate} | " +
        $"الطلب: {po.CustomerOrderNo ?? "إنتاج للمخزون"} | النسخة الفنية: {(po.EngineeringRevision is null ? "غير محددة" : $"R{po.EngineeringRevision}")} | الحالة: {ProductionStatusLabel(po.Status)}";

    private static string ProductionStatusLabel(string status) => status switch
    {
        "draft" => "مسودة",
        "open" => "مفتوح",
        "in_progress" => "قيد التنفيذ",
        "completed" => "مكتمل",
        "cancelled" => "ملغي",
        _ => status,
    };

    private static string AttachmentTypeLabel(string type) => type switch
    {
        "product_reference" => "صورة المنتج",
        "dimensioned_drawing" => "رسم هندسي بالأبعاد",
        "cut_list" => "مخطط كاتلست",
        "bom_export" => "تصدير BOM",
        _ => "مرفق آخر",
    };

    private static string EngineeringStatusLabel(string status) => status switch
    {
        "draft" => "مسودة",
        "finalized" => "مثبتة",
        "superseded" => "نسخة سابقة",
        _ => status,
    };

    private static string FormatFileSize(string value)
    {
        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bytes))
            return bytes >= 1024 * 1024 ? $"{bytes / 1024d / 1024d:N1} MB" : $"{bytes / 1024d:N1} KB";
        return value;
    }

    private async void SaveUnitAsync(object? sender, RoutedEventArgs e)
    {
        if (!int.TryParse(UnitPrecisionTextBox.Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var precision) || precision is < 0 or > 6)
        {
            MasterDataMessageTextBlock.Text = "عدد الخانات العشرية يجب أن يكون رقماً من 0 إلى 6.";
            return;
        }

        await CreateRecordAsync(SaveUnitButton, "units", new
        {
            code = UnitCodeTextBox.Text?.Trim(),
            nameAr = UnitNameArTextBox.Text?.Trim(),
            nameEn = UnitNameEnTextBox.Text?.Trim(),
            precisionDigits = precision,
        }, () =>
        {
            UnitCodeTextBox.Text = UnitNameArTextBox.Text = UnitNameEnTextBox.Text = string.Empty;
            UnitPrecisionTextBox.Text = "0";
        }, "تم حفظ وحدة القياس بنجاح.");
    }

    private async void SaveWarehouseAsync(object? sender, RoutedEventArgs e)
    {
        if (WarehouseTypeComboBox.SelectedItem is not Choice type)
        {
            MasterDataMessageTextBlock.Text = "اختر نوع المخزن.";
            return;
        }

        await CreateRecordAsync(SaveWarehouseButton, "warehouses", new
        {
            code = WarehouseCodeTextBox.Text?.Trim(),
            nameAr = WarehouseNameArTextBox.Text?.Trim(),
            nameEn = WarehouseNameEnTextBox.Text?.Trim(),
            warehouseType = type.Value,
        }, () =>
        {
            WarehouseCodeTextBox.Text = WarehouseNameArTextBox.Text = WarehouseNameEnTextBox.Text = string.Empty;
            WarehouseTypeComboBox.SelectedIndex = 0;
        }, "تم حفظ المخزن بنجاح.");
    }

    private async void SavePartyAsync(object? sender, RoutedEventArgs e)
    {
        if (PartyTypeComboBox.SelectedItem is not Choice type)
        {
            MasterDataMessageTextBlock.Text = "اختر نوع الطرف.";
            return;
        }

        await CreateRecordAsync(SavePartyButton, "parties", new
        {
            code = PartyCodeTextBox.Text?.Trim(),
            partyType = type.Value,
            nameAr = PartyNameArTextBox.Text?.Trim(),
            nameEn = PartyNameEnTextBox.Text?.Trim(),
            phone = PartyPhoneTextBox.Text?.Trim(),
        }, () =>
        {
            PartyCodeTextBox.Text = PartyNameArTextBox.Text = PartyNameEnTextBox.Text = PartyPhoneTextBox.Text = string.Empty;
            PartyTypeComboBox.SelectedIndex = 0;
        }, "تم حفظ العميل / المورد بنجاح.");
    }

    private async void SaveItemAsync(object? sender, RoutedEventArgs e)
    {
        if (ItemTypeComboBox.SelectedItem is not Choice type ||
            ItemClassificationComboBox.SelectedItem is not Choice classification ||
            ItemBaseUnitComboBox.SelectedItem is not UnitChoice unit)
        {
            MasterDataMessageTextBlock.Text = "اختر نوع الصنف ووحدة القياس والتصنيف. أضف وحدة قياس أولاً إذا لم توجد وحدات.";
            return;
        }

        await CreateRecordAsync(SaveItemButton, "items", new
        {
            code = ItemCodeTextBox.Text?.Trim(),
            nameAr = ItemNameArTextBox.Text?.Trim(),
            nameEn = ItemNameEnTextBox.Text?.Trim(),
            itemType = type.Value,
            baseUnitId = unit.Id,
            stockTracked = ItemStockTrackedCheckBox.IsChecked == true,
            classification = classification.Value,
        }, () =>
        {
            ItemCodeTextBox.Text = ItemNameArTextBox.Text = ItemNameEnTextBox.Text = string.Empty;
            ItemTypeComboBox.SelectedIndex = 0;
            ItemClassificationComboBox.SelectedIndex = 0;
            ItemStockTrackedCheckBox.IsChecked = true;
        }, "تم حفظ الصنف بنجاح.");
    }

    private async Task CreateRecordAsync(Button saveButton, string endpoint, object body, Action clearForm, string successMessage)
    {
        if (string.IsNullOrWhiteSpace(accessToken))
        {
            MasterDataMessageTextBlock.Text = "انتهت الجلسة. سجّل الدخول مرة أخرى.";
            return;
        }

        saveButton.IsEnabled = false;
        MasterDataMessageTextBlock.Text = "جارٍ الحفظ…";
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"master-data/{endpoint}")
            {
                Content = JsonContent.Create(body, options: JsonOptions),
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await HttpClient.SendAsync(request);
            await EnsureApiSuccessAsync(response);
            clearForm();
            await LoadMasterDataAsync();
            MasterDataMessageTextBlock.Text = successMessage;
        }
        catch (Exception ex)
        {
            MasterDataMessageTextBlock.Text = FriendlyError(ex);
        }
        finally
        {
            saveButton.IsEnabled = true;
        }
    }

    private static async Task EnsureApiSuccessAsync(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        var body = await response.Content.ReadAsStringAsync();
        string? message = null;
        try
        {
            using var json = JsonDocument.Parse(body);
            if (json.RootElement.TryGetProperty("message", out var value))
                message = value.ValueKind == JsonValueKind.Array
                    ? string.Join("، ", value.EnumerateArray().Select(x => x.GetString()))
                    : value.GetString();
        }
        catch { /* The API may return a plain-text error body. */ }

        if (response.StatusCode == HttpStatusCode.Unauthorized)
            throw new InvalidOperationException("انتهت الجلسة أو لم تعد صالحة. سجّل الدخول مرة أخرى.");
        if (response.StatusCode == HttpStatusCode.Conflict)
            throw new InvalidOperationException(message ?? "يوجد سجل بنفس الكود أو أن أحد المراجع غير صالح.");
        if (response.StatusCode == HttpStatusCode.BadRequest)
            throw new InvalidOperationException(message ?? "راجع البيانات المدخلة وحاول مرة أخرى.");

        throw new HttpRequestException(message ?? $"فشل الطلب إلى الخادم (HTTP {(int)response.StatusCode}).");
    }

    private static string FriendlyError(Exception exception) => exception switch
    {
        InvalidOperationException => exception.Message,
        HttpRequestException => $"تعذر إكمال الطلب: {exception.Message}",
        _ => "حدث خطأ غير متوقع أثناء الاتصال بالخادم. تحقق من تشغيل API ثم حاول مرة أخرى.",
    };

    private static string Describe<T>(IReadOnlyList<T> values, Func<T, string> describe) =>
        values.Count == 0 ? "لا توجد بيانات بعد." : string.Join(Environment.NewLine, values.Select(describe));

    private static string WarehouseTypeLabel(string value) => value switch
    {
        "raw_material" => "خامات",
        "finished_goods" => "منتجات تامة",
        _ => value,
    };

    private static string PartyTypeLabel(string value) => value switch
    {
        "customer" => "عميل",
        "supplier" => "مورد",
        "both" => "عميل ومورد",
        _ => value,
    };

    private static string ItemTypeLabel(string value) => value switch
    {
        "raw_material" => "خامة",
        "accessory" => "إكسسوار",
        "consumable" => "مستهلك",
        "finished_good" => "منتج تام",
        _ => value,
    };

    private static string ClassificationLabel(string value) => value switch
    {
        "materials" => "خامات بالمخزن",
        "industrial_expense" => "مصاريف صناعية",
        _ => value,
    };

    private sealed record Choice(string Value, string Label);
    private sealed record CustomerOrderChoice(string Id, string Label);
    private sealed record EngineeringPackageChoice(string Id, int Revision, string Status, string Label);
    private sealed record EngineeringFileChoice(string Id, string FileName, string AttachmentType, string? MaterialCode, string? MaterialColor, string? ThicknessMm, string? BoardLengthMm, string? BoardWidthMm, string? BoardQuantity, string Label);
    private sealed record CreatedOrder(string Id, string OrderNo, string CustomerId, string OrderDate, string? RequestedDeliveryDate, string Status, string? Notes, DateTime CreatedAt);
    private sealed record CustomerOrderView(string Id, string OrderNo, string CustomerId, string CustomerCode, string CustomerNameAr, string OrderDate, string? RequestedDeliveryDate, string Status, string? Notes, DateTime CreatedAt, string? CurrentPackageId, int? CurrentRevision, int RevisionCount, int ProductionOrderCount);
    private sealed record EngineeringPackageView(string Id, string CustomerOrderId, int Revision, string TechnicalReference, string? Notes, string Status, DateTime CreatedAt, DateTime? FinalizedAt, int LineCount, int FileCount);
    private sealed record EngineeringLineView(string Id, string PackageId, int LineNo, string LineType, string Description, string? MaterialReference, string Quantity, string UnitCode, string? Dimensions, string? Notes, DateTime CreatedAt);
    private sealed record EngineeringFileView(string Id, string PackageId, string FileName, string MimeType, string SizeBytes, string ChecksumSha256, string AttachmentType, string? Description, string? MaterialCode, string? MaterialColor, string? ThicknessMm, string? BoardLengthMm, string? BoardWidthMm, string? BoardQuantity, DateTime CreatedAt);
    private sealed record EngineeringCutListPartView(string Id, string PackageId, string SourceFileId, int LineNo, string PartName, string LengthMm, string WidthMm, string? ThicknessMm, string Quantity, string? Notes, string FileName, string MaterialCode, string MaterialColor, string BoardThicknessMm, string BoardLengthMm, string BoardWidthMm, string BoardQuantity);
    private sealed record CreatedProductionOrder(string Id, string OrderNo, string ProductionMode, string CustomerOrderId, string EngineeringPackageId, string ProductCode, string ProductName, string ProductionDate, string PlannedQuantity, string Status, DateTime CreatedAt);
    private sealed record ProductionOrderView(string Id, string OrderNo, string ProductionMode, string? CustomerOrderId, string? CustomerOrderNo, string? CustomerNameAr, string? EngineeringPackageId, int? EngineeringRevision, string ProductCode, string ProductName, string ProductionDate, string PlannedQuantity, string Status, DateTime CreatedAt);
    private sealed record ProductionOrderChoice(string Id, string Label);
    private sealed record ProductionWorkOrderResponse(ProductionWorkOrderDetails ProductionOrder, List<ProductionWorkOrderLineView> Lines);
    private sealed record ProductionWorkOrderDetails(string Id, string OrderNo, string ProductionMode, string? CustomerOrderId, string? CustomerOrderNo, string? CustomerNameAr, string? EngineeringPackageId, int? EngineeringRevision, string? TechnicalReference, string ProductCode, string ProductName, string ProductionDate, string PlannedQuantity, string Status, DateTime CreatedAt);
    private sealed record ProductionWorkOrderLineView(int LineNo, string? CatalogCode, string ItemNameAr, string Specification, string UnitCode, string? Quantity, bool IsSelected, bool IsCustom);
    private sealed record WorkOrderLinePayload(string? CatalogCode, string ItemNameAr, string Specification, string UnitCode, decimal? Quantity, bool IsSelected, bool IsCustom);
    private sealed record InventoryBalance(string ItemId, string ItemCode, string ItemNameAr, string UnitCode, string WarehouseId, string WarehouseCode, string WarehouseNameAr, decimal QuantityOnHand, decimal AverageUnitCost, decimal InventoryValue);
    private sealed record InventoryMovement(string Id, string OperationId, string DocumentReference, DateTime MovementDate, string ItemId, string ItemCode, string ItemNameAr, string WarehouseId, string WarehouseCode, string? PartyId, string? SupplierCode, string? SupplierNameAr, decimal Quantity, decimal UnitCost, decimal TotalCost, decimal AverageCostAfter, decimal QuantityAfter, decimal ValueAfter, string? Notes, DateTime PostedAt);
    private sealed record UnitChoice(string Id, string Label);
    private sealed record SessionProfile(string Sub, string Username, string[]? Roles, long Exp);
    private sealed record LoginResponse(string AccessToken, UserProfile User);
    private sealed record UserProfile(string Id, string Username, string DisplayName, string[] Roles);

    private sealed class WorkOrderLineEditor
    {
        private readonly TextBox? itemNameInput;
        private readonly TextBlock? itemNameDisplay;
        private readonly TextBox specificationInput;
        private readonly TextBox? unitInput;
        private readonly TextBlock? unitDisplay;
        private readonly TextBox quantityInput;
        private readonly CheckBox? selectionCheckBox;

        public string? CatalogCode { get; }
        public bool IsCustom { get; }

        public WorkOrderLineEditor(
            ProductionWorkOrderLineView line,
            TextBox? itemNameInput,
            TextBlock? itemNameDisplay,
            TextBox specificationInput,
            TextBox? unitInput,
            TextBlock? unitDisplay,
            TextBox quantityInput,
            CheckBox? selectionCheckBox)
        {
            CatalogCode = line.CatalogCode;
            IsCustom = line.IsCustom;
            this.itemNameInput = itemNameInput;
            this.itemNameDisplay = itemNameDisplay;
            this.specificationInput = specificationInput;
            this.unitInput = unitInput;
            this.unitDisplay = unitDisplay;
            this.quantityInput = quantityInput;
            this.selectionCheckBox = selectionCheckBox;
        }

        public bool TryCreatePayload(out WorkOrderLinePayload? payload, out string error)
        {
            payload = null;
            error = string.Empty;
            var itemName = (itemNameInput?.Text ?? itemNameDisplay?.Text ?? string.Empty).Trim();
            var unit = (unitInput?.Text ?? unitDisplay?.Text ?? string.Empty).Trim();
            var quantityText = quantityInput.Text?.Trim();
            decimal? quantity = null;
            if (!string.IsNullOrWhiteSpace(quantityText))
            {
                if (!decimal.TryParse(quantityText, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) &&
                    !decimal.TryParse(quantityText, NumberStyles.Number, CultureInfo.CurrentCulture, out parsed))
                {
                    error = "يوجد عدد غير صالح في نموذج أمر الشغل. أدخل أرقاماً صحيحة أو عشرية.";
                    return false;
                }
                if (parsed <= 0 || decimal.Round(parsed, 4) != parsed)
                {
                    error = "الكمية يجب أن تكون أكبر من صفر وبحد أقصى 4 خانات عشرية.";
                    return false;
                }
                quantity = parsed;
            }

            var isSelected = IsCustom ? !string.IsNullOrWhiteSpace(itemName) : selectionCheckBox?.IsChecked == true;
            if (isSelected && (string.IsNullOrWhiteSpace(itemName) || string.IsNullOrWhiteSpace(unit) || quantity is null))
            {
                error = IsCustom
                    ? "كل إضافة أخرى يجب أن تحتوي على اسم البند والوحدة والكمية."
                    : $"أدخل الكمية ووحدة القياس للبند المحدد: {itemName}.";
                return false;
            }

            payload = new WorkOrderLinePayload(
                CatalogCode,
                itemName,
                specificationInput.Text?.Trim() ?? string.Empty,
                unit,
                quantity,
                isSelected,
                IsCustom);
            return true;
        }
    }

    private sealed record MasterDataResponse(
        List<MasterDataUnit> Units,
        List<MasterDataWarehouse> Warehouses,
        List<MasterDataParty> Parties,
        List<MasterDataItem> Items);
    private sealed record MasterDataUnit(string Id, string Code, string NameAr, string NameEn, int PrecisionDigits);
    private sealed record MasterDataWarehouse(string Id, string Code, string NameAr, string NameEn, string WarehouseType);
    private sealed record MasterDataParty(string Id, string Code, string PartyType, string NameAr, string? NameEn, string? Phone);
    private sealed record MasterDataItem(string Id, string Code, string NameAr, string? NameEn, string ItemType, string BaseUnitId, bool StockTracked, string Classification);
}
