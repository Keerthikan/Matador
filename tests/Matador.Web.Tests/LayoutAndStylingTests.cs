using Microsoft.Playwright;
using Xunit;

namespace Matador.Web.Tests;

public class LayoutAndStylingTests
{
    private const string WebRootPath = "../../../../../src/Matador.Web/wwwroot";

    [Theory]
    [InlineData(1920, 1080)] // Desktop
    [InlineData(390, 844)]   // Mobil (iPhone 13/14)
    [InlineData(768, 1024)]  // Tablet
    public async Task LastEventBox_DoesNotClipOrOverflowText_AcrossViewports(int width, int height)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = width, Height = height }
        });

        var page = await context.NewPageAsync();

        var fullHtmlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, WebRootPath, "index.html"));
        var fileUri = new Uri(fullHtmlPath).AbsoluteUri;

        await page.GotoAsync(fileUri);

        // Skjul lobby-modal og vis spil-view
        await page.EvaluateAsync(@"() => {
            const lobby = document.getElementById('lobby-modal');
            if (lobby) lobby.style.display = 'none';

            const gameView = document.getElementById('game-view');
            if (gameView) gameView.style.display = 'flex';

            const lastEventBox = document.getElementById('last-event-box');
            // Simuler en lang hændelsestekst som den brugeren oplevede
            lastEventBox.innerText = 'Robot Mads opkrævede leje på Brandts Torv: kr. 550 fra Keerthikan!';
        }");

        // Tjek at last-event-box ikke har horisontalt overflow (dvs. ingen utilsigtet tekstafskæring)
        var isOverflowingHorizontally = await page.EvaluateAsync<bool>(@"() => {
            const el = document.getElementById('last-event-box');
            // Med margin på 2px for subpixel/zoom rendering
            return el.scrollWidth > (el.clientWidth + 2);
        }");

        Assert.False(isOverflowingHorizontally, $"Boksen under slå-terninger klipper tekst af på {width}x{height}!");
    }

    [Theory]
    [InlineData(1920, 1080)]
    [InlineData(390, 844)]
    public async Task DiceArea_AndActionButtons_AreVisibleAndNotCovered(int width, int height)
    {
        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var context = await browser.NewContextAsync(new BrowserNewContextOptions
        {
            ViewportSize = new ViewportSize { Width = width, Height = height }
        });

        var page = await context.NewPageAsync();
        var fullHtmlPath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, WebRootPath, "index.html"));
        await page.GotoAsync(new Uri(fullHtmlPath).AbsoluteUri);

        await page.EvaluateAsync(@"() => {
            const lobby = document.getElementById('lobby-modal');
            if (lobby) lobby.style.display = 'none';

            const gameView = document.getElementById('game-view');
            if (gameView) gameView.style.display = 'flex';

            const rollBtn = document.getElementById('btn-roll');
            if (rollBtn) rollBtn.classList.remove('hidden');
        }");

        var rollButton = page.Locator("#btn-roll");
        var diceContainer = page.Locator("#dice-container");
        var lastEventBox = page.Locator("#last-event-box");

        Assert.True(await rollButton.IsVisibleAsync(), "Slå med terninger knappen skal være synlig");
        Assert.True(await diceContainer.IsVisibleAsync(), "Terningerne skal være synlige");
        Assert.True(await lastEventBox.IsVisibleAsync(), "Hændelsesboksen skal være synlig");

        // Verificer at terningerne ligger over knappen, og hændelsesboksen ligger under knappen (korrekt lodret rækkefølge)
        var diceBox = await diceContainer.BoundingBoxAsync();
        var btnBox = await rollButton.BoundingBoxAsync();
        var eventBox = await lastEventBox.BoundingBoxAsync();

        Assert.NotNull(diceBox);
        Assert.NotNull(btnBox);
        Assert.NotNull(eventBox);

        Assert.True(diceBox.Y < btnBox.Y, "Terningerne skal være placeret over knappen");
        Assert.True(btnBox.Y < eventBox.Y, "Knappen skal være placeret over hændelsesboksen");
    }
}
