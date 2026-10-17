using System.Net;
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
    private string? accessToken;
    private Border LoginPanel = null!; private Border WelcomePanel = null!; private TextBox UsernameTextBox = null!; private TextBox PasswordTextBox = null!; private TextBlock LoginMessageTextBlock = null!; private TextBlock WelcomeTextBlock = null!; private TextBlock RolesTextBlock = null!; private Button SignInButton = null!; private Button AdminButton = null!;

    public MainWindow() { AvaloniaXamlLoader.Load(this); LoginPanel = this.FindControl<Border>("LoginPanel")!; WelcomePanel = this.FindControl<Border>("WelcomePanel")!; UsernameTextBox = this.FindControl<TextBox>("UsernameTextBox")!; PasswordTextBox = this.FindControl<TextBox>("PasswordTextBox")!; LoginMessageTextBlock = this.FindControl<TextBlock>("LoginMessageTextBlock")!; WelcomeTextBlock = this.FindControl<TextBlock>("WelcomeTextBlock")!; RolesTextBlock = this.FindControl<TextBlock>("RolesTextBlock")!; SignInButton = this.FindControl<Button>("SignInButton")!; AdminButton = this.FindControl<Button>("AdminButton")!; _ = RestoreSessionAsync(); }

    private async void SignInAsync(object? sender, RoutedEventArgs e)
    {
        LoginMessageTextBlock.Text = string.Empty;
        SignInButton.IsEnabled = false;
        try
        {
            var response = await HttpClient.PostAsJsonAsync("auth/login", new { username = UsernameTextBox.Text?.Trim(), password = PasswordTextBox.Text });
            if (response.StatusCode == HttpStatusCode.Unauthorized)
            {
                LoginMessageTextBlock.Text = "اسم المستخدم أو كلمة المرور غير صحيحة.";
                return;
            }
            response.EnsureSuccessStatusCode();
            var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
            if (result?.AccessToken is null || result.User is null) throw new InvalidOperationException("The server returned an incomplete session.");
            accessToken = result.AccessToken; SessionStore.Save(accessToken);
            LoginPanel.IsVisible = false;
            WelcomePanel.IsVisible = true;
            WelcomeTextBlock.Text = $"مرحباً، {result.User.DisplayName}";
            RolesTextBlock.Text = $"الصلاحيات: {string.Join("، ", result.User.Roles)}"; AdminButton.IsVisible = result.User.Roles.Contains("system_admin");
            PasswordTextBox.Text = string.Empty;
        }
        catch (HttpRequestException)
        {
            LoginMessageTextBlock.Text = "تعذر الاتصال بالخادم المحلي. تأكد من أن API يعمل ثم حاول مرة أخرى.";
        }
        catch (Exception)
        {
            LoginMessageTextBlock.Text = "تعذر إتمام تسجيل الدخول. حاول مرة أخرى.";
        }
        finally { SignInButton.IsEnabled = true; }
    }

    private async Task RestoreSessionAsync()
    {
        var stored = SessionStore.Read();
        if (stored is null) return;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, "auth/me");
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", stored);
            var response = await HttpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode) { SessionStore.Delete(); return; }
            var session = await response.Content.ReadFromJsonAsync<SessionProfile>();
            if (session is null) { SessionStore.Delete(); return; }
            accessToken = stored; LoginPanel.IsVisible = false; WelcomePanel.IsVisible = true;
            WelcomeTextBlock.Text = $"مرحباً، {session.Username}";
            RolesTextBlock.Text = $"الصلاحيات: {string.Join("، ", session.Roles)}"; AdminButton.IsVisible = session.Roles.Contains("system_admin");
        }
        catch { SessionStore.Delete(); }
    }
    private void SignOut(object? sender, RoutedEventArgs e)
    {
        accessToken = null; SessionStore.Delete();
        WelcomePanel.IsVisible = false;
        LoginPanel.IsVisible = true;
        LoginMessageTextBlock.Text = "تم تسجيل الخروج.";
    }

    private sealed record SessionProfile(string Sub, string Username, string[] Roles, long Exp);
    private sealed record LoginResponse(string AccessToken, UserProfile User);
    private sealed record UserProfile(string Id, string Username, string DisplayName, string[] Roles);
}




