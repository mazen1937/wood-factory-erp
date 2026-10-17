using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;

namespace WoodFactoryErp.Desktop;

public partial class MainWindow : Window
{
    private static readonly HttpClient HttpClient = new() { BaseAddress = new Uri("http://localhost:3000/api/") };
    private string? accessToken;
    private Border LoginPanel = null!; private Border WelcomePanel = null!; private TextBox UsernameTextBox = null!; private TextBox PasswordTextBox = null!; private TextBlock LoginMessageTextBlock = null!; private TextBlock WelcomeTextBlock = null!; private TextBlock RolesTextBlock = null!; private Button SignInButton = null!;

    public MainWindow() { AvaloniaXamlLoader.Load(this); LoginPanel = this.FindControl<Border>("LoginPanel")!; WelcomePanel = this.FindControl<Border>("WelcomePanel")!; UsernameTextBox = this.FindControl<TextBox>("UsernameTextBox")!; PasswordTextBox = this.FindControl<TextBox>("PasswordTextBox")!; LoginMessageTextBlock = this.FindControl<TextBlock>("LoginMessageTextBlock")!; WelcomeTextBlock = this.FindControl<TextBlock>("WelcomeTextBlock")!; RolesTextBlock = this.FindControl<TextBlock>("RolesTextBlock")!; SignInButton = this.FindControl<Button>("SignInButton")!; }

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
            accessToken = result.AccessToken;
            LoginPanel.IsVisible = false;
            WelcomePanel.IsVisible = true;
            WelcomeTextBlock.Text = $"مرحباً، {result.User.DisplayName}";
            RolesTextBlock.Text = $"الصلاحيات: {string.Join("، ", result.User.Roles)}";
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

    private void SignOut(object? sender, RoutedEventArgs e)
    {
        accessToken = null;
        WelcomePanel.IsVisible = false;
        LoginPanel.IsVisible = true;
        LoginMessageTextBlock.Text = "تم تسجيل الخروج.";
    }

    private sealed record LoginResponse(string AccessToken, UserProfile User);
    private sealed record UserProfile(string Id, string Username, string DisplayName, string[] Roles);
}


