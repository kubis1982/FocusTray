using System.Net;
using System.Text;

namespace FocusTray.Infrastructure.Jira;

public class JiraCallbackResult
{
    public string? Code { get; set; }
    public string? State { get; set; }
    public string? Error { get; set; }
    public string? ErrorDescription { get; set; }
}

/// <summary>
/// Captures the OAuth2 authorization redirect on a local loopback HTTP listener
/// (Atlassian has no built-in loopback helper like MSAL provides for Teams).
/// </summary>
public class JiraCallbackListener
{
    public async Task<JiraCallbackResult> WaitForCallbackAsync(string redirectUri, CancellationToken cancellationToken)
    {
        var parsedRedirectUri = new Uri(redirectUri);
        var prefix = $"{parsedRedirectUri.Scheme}://{parsedRedirectUri.Authority}/";

        using var listener = new HttpListener();
        listener.Prefixes.Add(prefix);
        listener.Start();

        using var registration = cancellationToken.Register(() => listener.Stop());

        try
        {
            var context = await listener.GetContextAsync();
            var result = ParseCallbackQuery(context.Request.Url!.Query);

            var isSuccess = result.Error == null;
            var responseHtml = isSuccess
                ? "<html><body><h2>Signed in successfully. You can close this tab.</h2></body></html>"
                : "<html><body><h2>Sign-in failed. You can close this tab.</h2></body></html>";

            var buffer = Encoding.UTF8.GetBytes(responseHtml);
            context.Response.ContentType = "text/html; charset=utf-8";
            context.Response.ContentLength64 = buffer.Length;
            await context.Response.OutputStream.WriteAsync(buffer, CancellationToken.None);
            context.Response.OutputStream.Close();

            return result;
        }
        finally
        {
            listener.Stop();
        }
    }

    public static JiraCallbackResult ParseCallbackQuery(string query)
    {
        var result = new JiraCallbackResult();
        var trimmed = query.TrimStart('?');

        if (string.IsNullOrEmpty(trimmed))
        {
            return result;
        }

        foreach (var pair in trimmed.Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var parts = pair.Split('=', 2);
            var key = Uri.UnescapeDataString(parts[0]);
            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;

            switch (key)
            {
                case "code": result.Code = value; break;
                case "state": result.State = value; break;
                case "error": result.Error = value; break;
                case "error_description": result.ErrorDescription = value; break;
            }
        }

        return result;
    }
}
