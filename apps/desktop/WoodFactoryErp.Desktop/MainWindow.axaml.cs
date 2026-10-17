using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using WoodFactoryErp.Desktop.Security;

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
    private Button InventoryButton = null!;
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
        InventoryButton = this.FindControl<Button>("InventoryButton")!;
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
        InventoryButton.IsEnabled = roles.Contains("system_admin");
    }

    private void SignOut(object? sender, RoutedEventArgs e)
    {
        accessToken = null;
        SessionStore.Delete();
        WelcomePanel.IsVisible = false;
        InventoryPanel.IsVisible = false;
        MasterDataPanel.IsVisible = false;
        LoginPanel.IsVisible = true;
        PasswordTextBox.Text = string.Empty;
        LoginMessageTextBlock.Text = "تم تسجيل الخروج.";
    }

    private void ShowHome(object? sender, RoutedEventArgs e)
    {
        MasterDataPanel.IsVisible = false;
        InventoryPanel.IsVisible = false;
        HomePanel.IsVisible = true;
    }

    private async void ShowMasterDataAsync(object? sender, RoutedEventArgs e)
    {
        HomePanel.IsVisible = false;
        InventoryPanel.IsVisible = false;
        MasterDataPanel.IsVisible = true;
        await LoadMasterDataAsync();
    }

    private async void ShowInventoryAsync(object? sender, RoutedEventArgs e)
    {
        HomePanel.IsVisible = false;
        MasterDataPanel.IsVisible = false;
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
    private sealed record InventoryBalance(string ItemId, string ItemCode, string ItemNameAr, string UnitCode, string WarehouseId, string WarehouseCode, string WarehouseNameAr, decimal QuantityOnHand, decimal AverageUnitCost, decimal InventoryValue);
    private sealed record InventoryMovement(string Id, string OperationId, string DocumentReference, DateTime MovementDate, string ItemId, string ItemCode, string ItemNameAr, string WarehouseId, string WarehouseCode, string? PartyId, string? SupplierCode, string? SupplierNameAr, decimal Quantity, decimal UnitCost, decimal TotalCost, decimal AverageCostAfter, decimal QuantityAfter, decimal ValueAfter, string? Notes, DateTime PostedAt);
    private sealed record UnitChoice(string Id, string Label);
    private sealed record SessionProfile(string Sub, string Username, string[]? Roles, long Exp);
    private sealed record LoginResponse(string AccessToken, UserProfile User);
    private sealed record UserProfile(string Id, string Username, string DisplayName, string[] Roles);
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
