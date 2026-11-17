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
    private TextBox EngineeringProductDescriptionTextBox = null!;
    private CheckBox EngineeringUpholsteryRequiredCheckBox = null!;
    private Border EngineeringCutListMetadataPanel = null!;
    private ComboBox EngineeringBoardItemComboBox = null!;
    private ComboBox EngineeringProductMaterialItemComboBox = null!;
    private ComboBox EngineeringProductMaterialLineComboBox = null!;
    private Button DeleteEngineeringProductMaterialLineButton = null!;
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
    private TextBox EngineeringWastePercentageTextBox = null!;
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

    public MainWindow() : this(true) { }

    internal MainWindow(bool restoreSession)
    {
        AvaloniaXamlLoader.Load(this);
        FindControls();
        InitializeChoices();
        InitializeOrderWorkspace();
        if (restoreSession) _ = RestoreSessionAsync();
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
        EngineeringProductDescriptionTextBox = this.FindControl<TextBox>("EngineeringProductDescriptionTextBox")!;
        EngineeringUpholsteryRequiredCheckBox = this.FindControl<CheckBox>("EngineeringUpholsteryRequiredCheckBox")!;
        EngineeringCutListMetadataPanel = this.FindControl<Border>("EngineeringCutListMetadataPanel")!;
        EngineeringBoardItemComboBox = this.FindControl<ComboBox>("EngineeringBoardItemComboBox")!;
        EngineeringProductMaterialItemComboBox = this.FindControl<ComboBox>("EngineeringProductMaterialItemComboBox")!;
        EngineeringProductMaterialLineComboBox = this.FindControl<ComboBox>("EngineeringProductMaterialLineComboBox")!;
        DeleteEngineeringProductMaterialLineButton = this.FindControl<Button>("DeleteEngineeringProductMaterialLineButton")!;
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
        EngineeringWastePercentageTextBox = this.FindControl<TextBox>("EngineeringWastePercentageTextBox")!;
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
            new Choice("accessory", "إكسسوار"),
            new Choice("raw_material", "خامة"),
            new Choice("paint", "أدهان"),
        };
        EngineeringLineTypeComboBox.SelectedIndex = 0;

        EngineeringAttachmentTypeComboBox.ItemsSource = new[]
        {
            new Choice("product_reference", "صورة المنتج المرجعية"),
            new Choice("dimensioned_drawing", "الرسم الهندسي بالأبعاد"),
            new Choice("cut_list", "كاتلست لون"),
            new Choice("bom_export", "تصدير قائمة المواد (BOM)"),
            new Choice("other", "مرفق آخر"),
        };
        EngineeringAttachmentTypeComboBox.SelectedIndex = 0;
        EngineeringCutListMetadataPanel.IsVisible = false;
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
        HideFinance();
        financeTimer?.Stop();
        ClearFinanceSession();
        HideProductionExecution();
        executionTimer?.Stop();
        ++executionDetailVersion;
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
        HideFinance();
        HideProductionExecution();
        MasterDataPanel.IsVisible = false;
        InventoryPanel.IsVisible = false;
        OrdersPanel.IsVisible = false;
        HomePanel.IsVisible = true;
    }

    private async void ShowMasterDataAsync(object? sender, RoutedEventArgs e)
    {
        HideFinance();
        HideProductionExecution();
        HomePanel.IsVisible = false;
        InventoryPanel.IsVisible = false;
        OrdersPanel.IsVisible = false;
        MasterDataPanel.IsVisible = true;
        await LoadMasterDataAsync();
    }

    private async void ShowInventoryAsync(object? sender, RoutedEventArgs e)
    {
        HideFinance();
        HideProductionExecution();
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
            RefreshEngineeringProductMaterialItemChoices();
            RefreshOrderAccessoryChoices();

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
                $"{x.MovementDate:yyyy-MM-dd} | {InventoryMovementLabel(x.MovementType)} | {x.DocumentReference} | {x.ItemCode} | كمية: {x.Quantity:N4} | تكلفة الوحدة: {x.UnitCost:N4} | الإجمالي: {x.TotalCost:N4} ج.م | متوسط بعد الحركة: {x.AverageCostAfter:N4}"));
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
        HideFinance();
        HideProductionExecution();
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
        SyncWorkspaceOrderPickers(); HideWorkspaceDetails(); PrepareJobHeaderForSelectedOrder();
        await LoadProductionOrdersAsync();
    }

    private async void EngineeringPackageChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!OrdersPanel.IsVisible || isLoadingPackages) return;
        await LoadEngineeringPackageDetailsAsync();
    }

    private async Task LoadOrdersAsync(string? selectOrderId = null)
    {
        if(string.IsNullOrWhiteSpace(accessToken))return;
        var previous=selectOrderId??(SelectedOrderComboBox.SelectedItem as CustomerOrderChoice)?.Id;
        isLoadingOrders=true;RefreshOrdersButton.IsEnabled=false;HideWorkspaceDetails();
        try{
            using var request=new HttpRequestMessage(HttpMethod.Get,"orders");request.Headers.Authorization=new("Bearer",accessToken);
            using var response=await HttpClient.SendAsync(request);await EnsureApiSuccessAsync(response);
            currentOrders=await response.Content.ReadFromJsonAsync<List<CustomerOrderView>>(JsonOptions)??[];
            var customers=(currentMasterData?.Parties??[]).Where(p=>p.PartyType is "customer" or "both").Select(p=>new Choice(p.Id,p.Code+" — "+p.NameAr)).ToList();
            var customer=pendingOrderCustomerId??(OrderCustomerComboBox.SelectedItem as Choice)?.Value;OrderCustomerComboBox.ItemsSource=customers;OrderCustomerComboBox.SelectedIndex=customers.FindIndex(c=>c.Value==customer);pendingOrderCustomerId=null;
            if(OrderCustomerComboBox.SelectedIndex<0&&customers.Count>0)OrderCustomerComboBox.SelectedIndex=0;
            var choices=currentOrders.Select(o=>new CustomerOrderChoice(o.Id,OrderLabel(o))).ToList();SelectedOrderComboBox.ItemsSource=choices;SelectedOrderComboBox.SelectedIndex=choices.FindIndex(c=>c.Id==previous);
            SyncWorkspaceOrderPickers();OrdersListTextBlock.Text="";OrdersMessageTextBlock.Text="تم تحديث الطلبات. ابحث عن الطلب بكوده أو اسمه.";
        }catch(Exception ex){OrdersMessageTextBlock.Text=FriendlyError(ex);}
        finally{isLoadingOrders=false;RefreshOrdersButton.IsEnabled=true;}
        PrepareJobHeaderForSelectedOrder();await LoadProductionOrdersAsync();
    }

    private async void CreateCustomerOrderAsync(object? sender, RoutedEventArgs e)
    {
        await ConfirmCompleteOrderAsync();
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
                EngineeringLinesTextBlock.Text = "اختر نسخة فنية لعرض تفاصيل الكاتلست.";
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
                $"{x.ProductCode ?? "(قديم)"} — R{x.Revision} — {EngineeringStatusLabel(x.Status)} — {x.ProductDescription ?? x.TechnicalReference}")).ToList();
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
        var productCode = EngineeringReferenceTextBox.Text?.Trim();
        var productDescription = EngineeringProductDescriptionTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(productCode) || string.IsNullOrWhiteSpace(productDescription))
        { OrdersMessageTextBlock.Text = "كود المنتج ووصفه مطلوبان."; return; }
        CreateEngineeringPackageButton.IsEnabled = false;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"orders/{order.Id}/engineering-packages")
            {
                Content = JsonContent.Create(new
                {
                    productCode,
                    productDescription,
                    upholsteryRequired = EngineeringUpholsteryRequiredCheckBox.IsChecked == true,
                    notes = EngineeringNotesTextBox.Text?.Trim()
                }, options: JsonOptions)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await HttpClient.SendAsync(request);
            await EnsureApiSuccessAsync(response);
            var created = await response.Content.ReadFromJsonAsync<EngineeringPackageView>(JsonOptions)
                ?? throw new InvalidOperationException("استجابة إنشاء النسخة الفنية فارغة.");
            EngineeringReferenceTextBox.Text = EngineeringProductDescriptionTextBox.Text = EngineeringNotesTextBox.Text = string.Empty;
            EngineeringUpholsteryRequiredCheckBox.IsChecked = false;
            await LoadEngineeringPackagesAsync(created.Id);
            OrdersMessageTextBlock.Text = $"تم إنشاء النسخة الفنية R{created.Revision} للمنتج {created.ProductCode} كمسودة. ارفع الصورة المرجعية ثم كاتلستات الألوان ومكونات المنتج.";
        }
        catch (Exception ex) { OrdersMessageTextBlock.Text = FriendlyError(ex); }
        finally { CreateEngineeringPackageButton.IsEnabled = true; }
    }

    private async void AddEngineeringLineAsync(object? sender, RoutedEventArgs e)
    {
        if (EngineeringPackageComboBox.SelectedItem is not EngineeringPackageChoice package)
        { OrdersMessageTextBlock.Text = "اختر نسخة فنية أولاً."; return; }
        if (package.Status != "draft") { OrdersMessageTextBlock.Text = "النسخة مثبتة؛ أنشئ نسخة جديدة لإضافة مكونات."; return; }
        if (EngineeringLineTypeComboBox.SelectedItem is not Choice lineType ||
            EngineeringProductMaterialItemComboBox.SelectedItem is not Choice itemChoice)
        { OrdersMessageTextBlock.Text = "اختر نوع المكون والصنف من البيانات الأساسية."; return; }
        if (!decimal.TryParse(EngineeringLineQuantityTextBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out var quantity) || quantity <= 0 || decimal.Round(quantity, 4) != quantity)
        { OrdersMessageTextBlock.Text = "الكمية لكل وحدة يجب أن تكون أكبر من صفر وبحد أقصى 4 خانات عشرية."; return; }

        AddEngineeringLineButton.IsEnabled = false;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"orders/engineering-packages/{package.Id}/product-material-lines")
            {
                Content = JsonContent.Create(new
                {
                    lineType = lineType.Value,
                    itemId = itemChoice.Value,
                    quantityPerUnit = quantity,
                    notes = EngineeringLineNotesTextBox.Text?.Trim()
                }, options: JsonOptions)
            };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await HttpClient.SendAsync(request);
            await EnsureApiSuccessAsync(response);
            EngineeringLineQuantityTextBox.Text = EngineeringLineNotesTextBox.Text = string.Empty;
            await LoadEngineeringProductMaterialLinesAsync(package.Id);
            OrdersMessageTextBlock.Text = "تم إضافة المكون إلى وصفة المنتج.";
        }
        catch (Exception ex) { OrdersMessageTextBlock.Text = FriendlyError(ex); }
        finally { AddEngineeringLineButton.IsEnabled = package.Status == "draft"; }
    }

    private async void DeleteEngineeringProductMaterialLineAsync(object? sender, RoutedEventArgs e)
    {
        if (EngineeringPackageComboBox.SelectedItem is not EngineeringPackageChoice package ||
            EngineeringProductMaterialLineComboBox.SelectedItem is not EngineeringProductMaterialLineChoice line)
        { OrdersMessageTextBlock.Text = "اختر مكوناً مسجلاً لحذفه."; return; }
        if (package.Status != "draft") { OrdersMessageTextBlock.Text = "لا يمكن تعديل نسخة مثبتة. أنشئ نسخة جديدة."; return; }
        DeleteEngineeringProductMaterialLineButton.IsEnabled = false;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Delete,
                $"orders/engineering-packages/{package.Id}/product-material-lines/{line.Id}");
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await HttpClient.SendAsync(request);
            await EnsureApiSuccessAsync(response);
            await LoadEngineeringProductMaterialLinesAsync(package.Id);
            OrdersMessageTextBlock.Text = "تم حذف المكون من المسودة.";
        }
        catch (Exception ex) { OrdersMessageTextBlock.Text = FriendlyError(ex); }
        finally { DeleteEngineeringProductMaterialLineButton.IsEnabled = package.Status == "draft"; }
    }

    private void EngineeringProductMaterialTypeChanged(object? sender, SelectionChangedEventArgs e)
    {
        RefreshEngineeringProductMaterialItemChoices();
    }

    private void EngineeringAttachmentTypeChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selected = EngineeringAttachmentTypeComboBox.SelectedItem as Choice;
        EngineeringCutListMetadataPanel.IsVisible = selected?.Value == "cut_list";
    }

    private void RefreshEngineeringProductMaterialItemChoices()
    {
        var lineType = (EngineeringLineTypeComboBox.SelectedItem as Choice)?.Value;
        var items = currentMasterData?.Items ?? [];
        var eligible = lineType switch
        {
            "accessory" => items.Where(x => x.ItemType == "accessory"),
            "raw_material" => items.Where(x => x.ItemType == "raw_material"),
            "paint" => items.Where(x => x.ItemType is "consumable" or "raw_material"),
            _ => Enumerable.Empty<MasterDataItem>()
        };
        var choices = eligible.OrderBy(x => x.Code)
            .Select(x => new Choice(x.Id, $"{x.Code} — {x.NameAr}"))
            .ToList();
        var oldId = (EngineeringProductMaterialItemComboBox.SelectedItem as Choice)?.Value;
        EngineeringProductMaterialItemComboBox.ItemsSource = choices;
        var index = oldId is null ? -1 : choices.FindIndex(x => x.Value == oldId);
        if (index >= 0) EngineeringProductMaterialItemComboBox.SelectedIndex = index;
        else EngineeringProductMaterialItemComboBox.SelectedIndex = choices.Count > 0 ? 0 : -1;

        var boards = items.Where(x => x.ItemType == "raw_material" && x.StockTracked && x.Classification == "materials")
            .OrderBy(x => x.Code)
            .Select(x => new Choice(x.Id, $"{x.Code} — {x.NameAr}"))
            .ToList();
        var oldBoardId = (EngineeringBoardItemComboBox.SelectedItem as Choice)?.Value;
        EngineeringBoardItemComboBox.ItemsSource = boards;
        var boardIndex = oldBoardId is null ? -1 : boards.FindIndex(x => x.Value == oldBoardId);
        if (boardIndex >= 0) EngineeringBoardItemComboBox.SelectedIndex = boardIndex;
        else EngineeringBoardItemComboBox.SelectedIndex = boards.Count > 0 ? 0 : -1;
    }

    private async Task LoadEngineeringProductMaterialLinesAsync(string packageId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get,
            $"orders/engineering-packages/{packageId}/product-material-lines");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await HttpClient.SendAsync(request);
        await EnsureApiSuccessAsync(response);
        var lines = await response.Content.ReadFromJsonAsync<List<EngineeringProductMaterialLineView>>(JsonOptions) ?? [];
        var choices = lines.Select(x => new EngineeringProductMaterialLineChoice(
            x.Id, x.LineType, x.ItemId,
            $"{x.LineNo}. [{ProductMaterialTypeLabel(x.LineType)}] {x.ItemCode} — {x.ItemNameAr} | {x.QuantityPerUnit} {x.UnitCode}" +
            (string.IsNullOrWhiteSpace(x.Notes) ? string.Empty : $" | {x.Notes}"))).ToList();
        var previousId = (EngineeringProductMaterialLineComboBox.SelectedItem as EngineeringProductMaterialLineChoice)?.Id;
        EngineeringProductMaterialLineComboBox.ItemsSource = choices;
        var desiredIndex = previousId is null ? -1 : choices.FindIndex(x => x.Id == previousId);
        if (desiredIndex >= 0) EngineeringProductMaterialLineComboBox.SelectedIndex = desiredIndex;
        else EngineeringProductMaterialLineComboBox.SelectedIndex = choices.Count > 0 ? 0 : -1;
        EngineeringLinesTextBlock.Text = choices.Count == 0
            ? "لا توجد مكونات مسجلة بعد. أضف الإكسسوارات والخامات والأدهان اللازمة لكل وحدة من المنتج."
            : string.Join(Environment.NewLine, choices.Select(x => x.Label));
        var isDraft = (EngineeringPackageComboBox.SelectedItem as EngineeringPackageChoice)?.Status == "draft";
        DeleteEngineeringProductMaterialLineButton.IsEnabled = isDraft && choices.Count > 0;
        AddEngineeringLineButton.IsEnabled = isDraft;
    }

    private static string ProductMaterialTypeLabel(string value) => value switch
    {
        "accessory" => "إكسسوار",
        "raw_material" => "خامة",
        "paint" => "أدهان",
        _ => value,
    };

    private async Task LoadEngineeringPackageDetailsAsync()
    {
        if (string.IsNullOrWhiteSpace(accessToken) || EngineeringPackageComboBox.SelectedItem is not EngineeringPackageChoice selectedPackage)
        {
            EngineeringPackageSummaryTextBlock.Text = string.Empty;
            EngineeringLinesTextBlock.Text = "لا توجد نسخة فنية محددة.";
            EngineeringPackageFileComboBox.ItemsSource = Array.Empty<EngineeringFileChoice>();
            EngineeringProductMaterialLineComboBox.ItemsSource = Array.Empty<EngineeringProductMaterialLineChoice>();
            EngineeringProductMaterialLineComboBox.SelectedIndex = -1;
            EngineeringLinesTextBlock.Text = "لا توجد مكونات مسجلة بعد.";
            AddEngineeringLineButton.IsEnabled = DeleteEngineeringProductMaterialLineButton.IsEnabled = false;
            EngineeringCutListMetadataPanel.IsVisible = false;
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
                : $"المنتج: {package.ProductCode ?? "(قديم بلا كود)"} — {package.ProductDescription ?? package.TechnicalReference} | النسخة R{package.Revision} — {EngineeringStatusLabel(package.Status)} | تنجيد: {(package.UpholsteryRequired == true ? "نعم" : package.UpholsteryRequired == false ? "لا" : "غير محدد في النسخة القديمة")} | المرفقات: {package.FileCount}";

            await LoadEngineeringProductMaterialLinesAsync(selectedPackage.Id);

            using var filesRequest = new HttpRequestMessage(HttpMethod.Get, $"orders/engineering-packages/{selectedPackage.Id}/files");
            filesRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var filesResponse = await HttpClient.SendAsync(filesRequest);
            await EnsureApiSuccessAsync(filesResponse);
            var files = await filesResponse.Content.ReadFromJsonAsync<List<EngineeringFileView>>(JsonOptions) ?? [];
            var fileChoices = files.Select(x => new EngineeringFileChoice(
                x.Id, x.FileName, x.AttachmentType, x.MaterialCode, x.MaterialItemId, x.MaterialColor,
                x.ThicknessMm, x.BoardLengthMm, x.BoardWidthMm, x.BoardQuantity, x.WastePercentage,
                $"{AttachmentTypeLabel(x.AttachmentType)} — {x.FileName}" +
                (x.AttachmentType == "cut_list" ? $" | {x.MaterialCode} | اللون: {x.MaterialColor} | الألواح: {x.BoardQuantity} | الهالك: {(x.WastePercentage is null ? "غير محدد" : x.WastePercentage + "%")}" : "") +
                (string.IsNullOrWhiteSpace(x.Description) ? "" : $" | {x.Description}") + $" ({FormatFileSize(x.SizeBytes)})")).ToList();
            var previousFileId = (EngineeringPackageFileComboBox.SelectedItem as EngineeringFileChoice)?.Id;
            EngineeringPackageFileComboBox.ItemsSource = fileChoices;
            var fileIndex = previousFileId is null ? -1 : fileChoices.FindIndex(x => x.Id == previousFileId);
            if (fileIndex >= 0) EngineeringPackageFileComboBox.SelectedIndex = fileIndex;
            else if (fileChoices.Count > 0) EngineeringPackageFileComboBox.SelectedIndex = 0;
            DownloadEngineeringFileButton.IsEnabled = fileChoices.Count > 0;
            FinalizeEngineeringPackageButton.IsEnabled = isDraft;
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
        { OrdersMessageTextBlock.Text = "اختر نوع المستند أولاً."; return; }

        decimal boardQuantity = 0;
        decimal wastePercentage = 0;
        Choice? boardItem = null;
        if (attachmentType.Value == "cut_list")
        {
            boardItem = EngineeringBoardItemComboBox.SelectedItem as Choice;
            if (boardItem is null || string.IsNullOrWhiteSpace(EngineeringMaterialColorTextBox.Text))
            { OrdersMessageTextBlock.Text = "اختر خامة اللوح من البيانات الأساسية وأدخل اللون."; return; }
            if (!decimal.TryParse(EngineeringBoardQuantityTextBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out boardQuantity) || boardQuantity <= 0)
            { OrdersMessageTextBlock.Text = "عدد الألواح يجب أن يكون أكبر من صفر."; return; }
            if (!decimal.TryParse(EngineeringWastePercentageTextBox.Text, NumberStyles.Number, CultureInfo.InvariantCulture, out wastePercentage) || wastePercentage < 0 || wastePercentage > 100)
            { OrdersMessageTextBlock.Text = "نسبة الهالك يجب أن تكون من 0 إلى 100. استخدم النقطة للفواصل العشرية."; return; }
        }
        var storage = TopLevel.GetTopLevel(this)?.StorageProvider;
        if (storage is null) { OrdersMessageTextBlock.Text = "اختيار الملفات غير متاح في هذه الجلسة."; return; }
        var picked = await storage.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "اختيار مستند المنتج أو الكاتلست",
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
            if (attachmentType.Value == "cut_list")
            {
                AddMetadata("materialItemId", boardItem!.Value);
                AddMetadata("materialColor", EngineeringMaterialColorTextBox.Text);
                AddMetadata("boardQuantity", boardQuantity.ToString(CultureInfo.InvariantCulture));
                AddMetadata("wastePercentage", wastePercentage.ToString(CultureInfo.InvariantCulture));
            }
            using var request = new HttpRequestMessage(HttpMethod.Post, $"orders/engineering-packages/{package.Id}/files") { Content = multipart };
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var response = await HttpClient.SendAsync(request);
            await EnsureApiSuccessAsync(response);
            EngineeringAttachmentDescriptionTextBox.Text = string.Empty;
            await LoadEngineeringPackagesAsync(package.Id);
            OrdersMessageTextBlock.Text = $"تم رفع المستند: {file.Name}";
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

    private async void CreateDraftProductionOrderAsync(object? sender, RoutedEventArgs e) => await CreateLinkedJobCoreAsync();

    private async void ProductionOrderChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (!OrdersPanel.IsVisible || isLoadingProductionOrders) return;
        jobSearchPicker.Select((ProductionOrderComboBox.SelectedItem as ProductionOrderChoice)?.Id);
        await LoadProductionWorkOrderAsync();
    }

    private async Task LoadProductionOrdersAsync(string? selectProductionOrderId=null) => await LoadLinkedJobOrdersAsync(selectProductionOrderId);

    private async Task LoadProductionWorkOrderAsync(string? productionOrderId = null)
    {
        var selected = productionOrderId is null ? ProductionOrderComboBox.SelectedItem as ProductionOrderChoice : null;
        var id = productionOrderId ?? selected?.Id;
        if (string.IsNullOrWhiteSpace(accessToken) || string.IsNullOrWhiteSpace(id))
        {
            ClearProductionWorkOrderEditor();
            return;
        }

        var loadVersion = ++productionWorkOrderLoadVersion;
        isLoadingProductionWorkOrder = true;
        try
        {
            var result = await FetchProductionWorkOrderAsync(id)
                ?? throw new InvalidOperationException("استجابة أمر الشغل فارغة.");
            if (loadVersion != productionWorkOrderLoadVersion) return;
            ProductionProductCodeTextBox.Text = result.ProductionOrder.ProductCode;
            ProductionPlannedQuantityTextBox.Text = result.ProductionOrder.PlannedQuantity;
            ProductionProductNameTextBox.Text = result.ProductionOrder.ProductName;
            ProductionDateTextBox.Text = result.ProductionOrder.ProductionDate;
            var po = result.ProductionOrder;
            ProductionWorkOrderSummaryTextBlock.Text = BuildLinkedJobSummary(po);
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
        finally { if (loadVersion == productionWorkOrderLoadVersion) isLoadingProductionWorkOrder = false; }
    }

    private void ClearProductionWorkOrderEditor()
    {
        ++productionWorkOrderLoadVersion;
        isLoadingProductionWorkOrder = false;
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
        if (isLoadingProductionWorkOrder) return false;
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
            target.SetLength(0);
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

    private static string BuildProductionWorkOrderSummary(ProductionWorkOrderDetails po) => BuildLinkedJobSummary(po);

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
            string? createdCustomerId = null;
            if (endpoint == "parties" && returnToOrderAfterCustomer)
            {
                using var created = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
                createdCustomerId = created.RootElement.GetProperty("id").GetString();
            }
            clearForm();
            await LoadMasterDataAsync();
            MasterDataMessageTextBlock.Text = successMessage;
            if (createdCustomerId is not null)
            {
                pendingOrderCustomerId = createdCustomerId;
                returnToOrderAfterCustomer = false;
                MasterDataPanel.IsVisible = false;
                OrdersPanel.IsVisible = true;
                await LoadOrdersAsync();
            }
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
    private sealed record EngineeringFileChoice(string Id, string FileName, string AttachmentType, string? MaterialCode, string? MaterialItemId, string? MaterialColor, string? ThicknessMm, string? BoardLengthMm, string? BoardWidthMm, string? BoardQuantity, string? WastePercentage, string Label);
    private sealed record CreatedOrder(string Id, string OrderNo, string CustomerId, string OrderDate, string? RequestedDeliveryDate, string Status, string? Notes, DateTime CreatedAt);
    private sealed record CustomerOrderView(string Id, string OrderNo, string CustomerId, string CustomerCode, string CustomerNameAr, string OrderDate, string? RequestedDeliveryDate, string Status, string? Notes, DateTime CreatedAt, string? CurrentPackageId, int? CurrentRevision, int RevisionCount, int ProductionOrderCount, string? ProductCode, string? ProductName, string? OrderName = null);
    private sealed record EngineeringPackageView(string Id, string CustomerOrderId, int Revision, string? ProductCode, string? ProductDescription, bool? UpholsteryRequired, string TechnicalReference, string? Notes, string Status, DateTime CreatedAt, DateTime? FinalizedAt, int LineCount, int ProductMaterialLineCount, int FileCount);
    private sealed record EngineeringLineView(string Id, string PackageId, int LineNo, string LineType, string Description, string? MaterialReference, string Quantity, string UnitCode, string? Dimensions, string? Notes, DateTime CreatedAt);
    private sealed record EngineeringFileView(string Id, string PackageId, string FileName, string MimeType, string SizeBytes, string ChecksumSha256, string AttachmentType, string? Description, string? MaterialCode, string? MaterialItemId, string? MaterialColor, string? WastePercentage, string? ThicknessMm, string? BoardLengthMm, string? BoardWidthMm, string? BoardQuantity, DateTime CreatedAt);
    private sealed record EngineeringProductMaterialLineView(string Id, string PackageId, int LineNo, string LineType, string ItemId, string ItemCode, string ItemNameAr, string UnitCode, string UnitNameAr, string QuantityPerUnit, string? Notes, DateTime CreatedAt);
    private sealed record EngineeringProductMaterialLineChoice(string Id, string LineType, string ItemId, string Label);
    private sealed record EngineeringCutListPartView(string Id, string PackageId, string SourceFileId, int LineNo, string PartName, string LengthMm, string WidthMm, string? ThicknessMm, string Quantity, string? Notes, string FileName, string MaterialCode, string MaterialColor, string BoardThicknessMm, string BoardLengthMm, string BoardWidthMm, string BoardQuantity);
    private sealed record CreatedProductionOrder(string Id, string OrderNo, string ProductionMode, string CustomerOrderId, string EngineeringPackageId, string ProductCode, string ProductName, string ProductionDate, string PlannedQuantity, string Status, DateTime CreatedAt);
    private sealed record ProductionOrderView(string Id, string OrderNo, string ProductionMode, string? CustomerOrderId, string? CustomerOrderNo, string? CustomerNameAr, string? EngineeringPackageId, int? EngineeringRevision, string ProductCode, string ProductName, string ProductionDate, string PlannedQuantity, string Status, DateTime CreatedAt, string? JobName = null, string? PlannedEndDate = null);
    private sealed record ProductionOrderChoice(string Id, string Label);
    private sealed record ProductionWorkOrderResponse(ProductionWorkOrderDetails ProductionOrder, List<ProductionWorkOrderLineView> Lines);
    private sealed record ProductionWorkOrderDetails(string Id, string OrderNo, string ProductionMode, string? CustomerOrderId, string? CustomerOrderNo, string? CustomerNameAr, string? EngineeringPackageId, int? EngineeringRevision, string? TechnicalReference, string ProductCode, string ProductName, string ProductionDate, string PlannedQuantity, string Status, DateTime CreatedAt, string? JobName = null, string? PlannedEndDate = null);
    private sealed record ProductionWorkOrderLineView(int LineNo, string? CatalogCode, string ItemNameAr, string Specification, string UnitCode, string? Quantity, bool IsSelected, bool IsCustom);
    private sealed record WorkOrderLinePayload(string? CatalogCode, string ItemNameAr, string Specification, string UnitCode, decimal? Quantity, bool IsSelected, bool IsCustom);
    private sealed record InventoryBalance(string ItemId, string ItemCode, string ItemNameAr, string UnitCode, string WarehouseId, string WarehouseCode, string WarehouseNameAr, decimal QuantityOnHand, decimal AverageUnitCost, decimal InventoryValue);
    private sealed record InventoryMovement(string Id, string OperationId, string DocumentReference, DateTime MovementDate, string ItemId, string ItemCode, string ItemNameAr, string WarehouseId, string WarehouseCode, string? PartyId, string? SupplierCode, string? SupplierNameAr, decimal Quantity, decimal UnitCost, decimal TotalCost, decimal AverageCostAfter, decimal QuantityAfter, decimal ValueAfter, string? Notes, DateTime PostedAt, string? MovementType);
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
