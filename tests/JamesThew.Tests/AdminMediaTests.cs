using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.RegularExpressions;
using JamesThew.Data;
using JamesThew.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace JamesThew.Tests;

[Collection("Foundation")]
public class AdminMediaTests(FoundationFixture fixture)
{
    private static string NewEmail() => Guid.NewGuid().ToString("N") + "@example.test";
    private static string NewPassword() => Convert.ToHexString(RandomNumberGenerator.GetBytes(20)) + "a!9";

    private static readonly byte[] ValidJpegBytes =
    [
        0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46, 0x00, 0x01, 0x01, 0x00,
        0x00, 0x01, 0x00, 0x01, 0x00, 0x00, 0xFF, 0xDB, 0x00, 0x43, 0x00, 0x08, 0x06, 0x06,
        0x07, 0x06, 0x05, 0x08, 0x07, 0x07, 0x07, 0x09, 0x09, 0x08, 0x0A, 0x0C, 0x14, 0x0D,
        0x0C, 0x0B, 0x0B, 0x0C, 0x19, 0x12, 0x13, 0x0F, 0x14, 0x1D, 0x1A, 0x1F, 0x1E, 0x1D,
        0x1A, 0x1C, 0x1C, 0x20, 0x24, 0x2E, 0x27, 0x20, 0x22, 0x2C, 0x23, 0x1C, 0x1C, 0x28,
        0x37, 0x29, 0x2C, 0x30, 0x31, 0x34, 0x34, 0x34, 0x1F, 0x27, 0x39, 0x3D, 0x38, 0x32,
        0x3C, 0x2E, 0x33, 0x34, 0x32, 0xFF, 0xD9
    ];

    private static readonly byte[] ValidPngBytes =
    [
        0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x0D, 0x49, 0x48,
        0x44, 0x52, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x08, 0x06, 0x00, 0x00,
        0x00, 0x1F, 0x15, 0xC4, 0x89, 0x00, 0x00, 0x00, 0x0A, 0x49, 0x44, 0x41, 0x54, 0x78,
        0x9C, 0x63, 0x00, 0x01, 0x00, 0x00, 0x05, 0x00, 0x01, 0x0D, 0x0A, 0x2D, 0xB4, 0x00,
        0x00, 0x00, 0x00, 0x49, 0x45, 0x4E, 0x44, 0xAE, 0x42, 0x60, 0x82
    ];

    private static readonly byte[] ValidWebpBytes =
    [
        0x52, 0x49, 0x46, 0x46, 0x1A, 0x00, 0x00, 0x00, 0x57, 0x45, 0x42, 0x50, 0x56, 0x50,
        0x38, 0x4C, 0x0E, 0x00, 0x00, 0x00, 0x2F, 0x00, 0x00, 0x00, 0x00, 0x07, 0x10, 0x70,
        0x88, 0x88, 0x08, 0x00, 0x00, 0x00
    ];

    private static async Task<string> Token(HttpClient client, string path)
    {
        var html = await client.GetStringAsync(path);
        var match = Regex.Match(html, "name=\"__RequestVerificationToken\"[^>]*value=\"([^\"]+)\"");
        Assert.True(match.Success, $"Expected an antiforgery token in the rendered form at {path}.");
        return WebUtility.HtmlDecode(match.Groups[1].Value);
    }

    private static async Task RegisterAndLogin(HttpClient client, string email, string password, string displayName = "Test Member")
    {
        var values = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await Token(client, "/account/register"),
            ["DisplayName"] = displayName,
            ["Email"] = email,
            ["Password"] = password,
            ["ConfirmPassword"] = password
        };
        var registerResponse = await client.PostAsync("/account/register", new FormUrlEncodedContent(values));
        Assert.Equal(HttpStatusCode.Redirect, registerResponse.StatusCode);
    }

    private static async Task Login(HttpClient client, string email, string password)
    {
        var values = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = await Token(client, "/account/login"),
            ["Email"] = email,
            ["Password"] = password,
            ["ReturnUrl"] = "/"
        };
        var response = await client.PostAsync("/account/login", new FormUrlEncodedContent(values));
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
    }

    private static IConfiguration SeedConfig(string email, string password) => new ConfigurationBuilder()
        .AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["LocalAdmin:Email"] = email,
            ["LocalAdmin:Password"] = password
        }).Build();

    private sealed class DevEnv : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;
        public string ApplicationName { get; set; } = "JamesThew";
        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private async Task<(HttpClient Client, string Email, string Password)> CreateAdminClientAsync()
    {
        var email = NewEmail();
        var password = NewPassword();
        using (var scope = fixture.Services.CreateScope())
        {
            await IdentitySeeder.SeedLocalAdminAsync(scope.ServiceProvider, SeedConfig(email, password), new DevEnv());
        }

        var client = fixture.NewClient();
        await Login(client, email, password);
        return (client, email, password);
    }

    // =========================================================================
    // 1. Authorization: Guest and Member cannot access Media Admin
    // =========================================================================

    [Fact]
    public async Task Guest_CannotAccessMediaAdminEndpoints()
    {
        using var guest = fixture.NewClient();

        // GET /admin/media
        var getMedia = await guest.GetAsync("/admin/media");
        Assert.Equal(HttpStatusCode.Redirect, getMedia.StatusCode);
        Assert.Contains("/account/login", getMedia.Headers.Location?.ToString());

        // GET /admin/media/picker
        var getPicker = await guest.GetAsync("/admin/media/picker");
        Assert.Equal(HttpStatusCode.Redirect, getPicker.StatusCode);
        Assert.Contains("/account/login", getPicker.Headers.Location?.ToString());

        // POST /admin/media/upload
        var uploadContent = new MultipartFormDataContent();
        var postUpload = await guest.PostAsync("/admin/media/upload", uploadContent);
        Assert.Equal(HttpStatusCode.Redirect, postUpload.StatusCode);
        Assert.Contains("/account/login", postUpload.Headers.Location?.ToString());

        // POST /admin/media/delete
        var deleteContent = new FormUrlEncodedContent(new Dictionary<string, string> { ["fileName"] = "sample.jpg" });
        var postDelete = await guest.PostAsync("/admin/media/delete", deleteContent);
        Assert.Equal(HttpStatusCode.Redirect, postDelete.StatusCode);
        Assert.Contains("/account/login", postDelete.Headers.Location?.ToString());
    }

    [Fact]
    public async Task Member_CannotAccessMediaAdminEndpoints()
    {
        using var memberClient = fixture.NewClient();
        var email = NewEmail();
        var pass = NewPassword();
        await RegisterAndLogin(memberClient, email, pass);

        // GET /admin/media
        var getMedia = await memberClient.GetAsync("/admin/media");
        Assert.Equal(HttpStatusCode.Redirect, getMedia.StatusCode);
        Assert.Contains("/account/access-denied", getMedia.Headers.Location?.ToString());

        // GET /admin/media/picker
        var getPicker = await memberClient.GetAsync("/admin/media/picker");
        Assert.Equal(HttpStatusCode.Redirect, getPicker.StatusCode);
        Assert.Contains("/account/access-denied", getPicker.Headers.Location?.ToString());

        // POST /admin/media/upload
        var token = await Token(memberClient, "/membership");
        var uploadContent = new MultipartFormDataContent();
        uploadContent.Add(new StringContent(token), "__RequestVerificationToken");
        var postUpload = await memberClient.PostAsync("/admin/media/upload", uploadContent);
        Assert.Equal(HttpStatusCode.Redirect, postUpload.StatusCode);
        Assert.Contains("/account/access-denied", postUpload.Headers.Location?.ToString());

        // POST /admin/media/delete
        var deleteContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["fileName"] = "sample.jpg"
        });
        var postDelete = await memberClient.PostAsync("/admin/media/delete", deleteContent);
        Assert.Equal(HttpStatusCode.Redirect, postDelete.StatusCode);
        Assert.Contains("/account/access-denied", postDelete.Headers.Location?.ToString());
    }

    // =========================================================================
    // 2. Admin can upload valid images (.jpg, .png, .webp)
    // =========================================================================

    [Theory]
    [InlineData("culinary-delight.jpg", "image/jpeg", 0)] // JPEG
    [InlineData("plating-art.png", "image/png", 1)]       // PNG
    [InlineData("pan-sear.webp", "image/webp", 2)]       // WEBP
    public async Task Admin_CanUploadValidImages(string originalFileName, string mimeType, int formatIndex)
    {
        var (admin, _, _) = await CreateAdminClientAsync();

        var token = await Token(admin, "/admin/media");
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(token), "__RequestVerificationToken");

        byte[] bytes = formatIndex switch
        {
            0 => ValidJpegBytes,
            1 => ValidPngBytes,
            _ => ValidWebpBytes
        };

        var fileContent = new ByteArrayContent(bytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
        form.Add(fileContent, "File", originalFileName);

        var response = await admin.PostAsync("/admin/media/upload", form);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/admin/media", response.Headers.Location?.ToString());

        // Follow redirect and check media gallery
        var galleryHtml = await admin.GetStringAsync("/admin/media");
        Assert.Contains("successfully uploaded", galleryHtml);

        // Check picker API returns the uploaded asset
        var pickerRes = await admin.GetAsync("/admin/media/picker");
        Assert.Equal(HttpStatusCode.OK, pickerRes.StatusCode);
        var pickerJson = await pickerRes.Content.ReadAsStringAsync();
        Assert.Contains("/uploads/editorial/", pickerJson);
    }

    // =========================================================================
    // 3. Validation: Invalid file types rejected
    // =========================================================================

    [Theory]
    [InlineData("script.sh", "application/x-sh", "echo 'hello'")]
    [InlineData("malicious.exe", "application/octet-stream", "MZP\x00\x02")]
    [InlineData("vector.svg", "image/svg+xml", "<svg xmlns='http://www.w3.org/2000/svg'><script>alert(1)</script></svg>")]
    [InlineData("document.pdf", "application/pdf", "%PDF-1.4")]
    public async Task Admin_InvalidFileTypes_Rejected(string fileName, string mimeType, string rawContent)
    {
        var (admin, _, _) = await CreateAdminClientAsync();

        var token = await Token(admin, "/admin/media");
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(token), "__RequestVerificationToken");

        var fileContent = new ByteArrayContent(System.Text.Encoding.UTF8.GetBytes(rawContent));
        fileContent.Headers.ContentType = new MediaTypeHeaderValue(mimeType);
        form.Add(fileContent, "File", fileName);

        var response = await admin.PostAsync("/admin/media/upload", form);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var galleryHtml = await admin.GetStringAsync("/admin/media");
        Assert.Contains("Unsupported file format", galleryHtml);
    }

    [Fact]
    public async Task Admin_SpoofedExtensionWithInvalidHeader_Rejected()
    {
        var (admin, _, _) = await CreateAdminClientAsync();

        var token = await Token(admin, "/admin/media");
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(token), "__RequestVerificationToken");

        // An ASCII text file disguised as a JPEG
        var fakeJpgBytes = System.Text.Encoding.UTF8.GetBytes("This is plain text pretending to be a JPG file.");
        var fileContent = new ByteArrayContent(fakeJpgBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(fileContent, "File", "pretend.jpg");

        var response = await admin.PostAsync("/admin/media/upload", form);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var galleryHtml = await admin.GetStringAsync("/admin/media");
        Assert.Contains("File signature verification failed", galleryHtml);
    }

    // =========================================================================
    // 4. Validation: Oversized files rejected (> 5 MB)
    // =========================================================================

    [Fact]
    public async Task Admin_OversizedFile_Rejected()
    {
        var (admin, _, _) = await CreateAdminClientAsync();

        var token = await Token(admin, "/admin/media");
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(token), "__RequestVerificationToken");

        // 5.2 MB array with valid JPEG header at start
        var largeBytes = new byte[5 * 1024 * 1024 + 200 * 1024];
        Array.Copy(ValidJpegBytes, largeBytes, ValidJpegBytes.Length);

        var fileContent = new ByteArrayContent(largeBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(fileContent, "File", "oversized-hero.jpg");

        var response = await admin.PostAsync("/admin/media/upload", form);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var galleryHtml = await admin.GetStringAsync("/admin/media");
        Assert.Contains("File size exceeds the 5 MB limit", galleryHtml);
    }

    // =========================================================================
    // 5. Security: Path Traversal attempts are rejected
    // =========================================================================

    [Theory]
    [InlineData("../../traversal.jpg")]
    [InlineData("..\\..\\win.ini")]
    [InlineData("folder/sub/attack.png")]
    public async Task Admin_PathTraversalInUpload_Rejected(string maliciousFileName)
    {
        var (admin, _, _) = await CreateAdminClientAsync();

        var token = await Token(admin, "/admin/media");
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(token), "__RequestVerificationToken");

        var fileContent = new ByteArrayContent(ValidJpegBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(fileContent, "File", maliciousFileName);

        var response = await admin.PostAsync("/admin/media/upload", form);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var galleryHtml = await admin.GetStringAsync("/admin/media");
        Assert.Contains("Path traversal characters are not permitted", galleryHtml);
    }

    [Theory]
    [InlineData("../../appsettings.json")]
    [InlineData("..\\..\\Windows\\System32\\calc.exe")]
    [InlineData("/etc/passwd")]
    public async Task Admin_PathTraversalInDelete_Rejected(string maliciousFileName)
    {
        var (admin, _, _) = await CreateAdminClientAsync();

        var token = await Token(admin, "/admin/media");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["fileName"] = maliciousFileName
        });

        var response = await admin.PostAsync("/admin/media/delete", form);
        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);

        var galleryHtml = await admin.GetStringAsync("/admin/media");
        Assert.Contains("Path traversal characters are not permitted", galleryHtml);
    }

    // =========================================================================
    // 6. Recipe & Tip Integration: Selected image renders publicly, removal is clean
    // =========================================================================

    [Fact]
    public async Task Recipe_CanAttachUploadedImage_AndRendersPublicly()
    {
        var (admin, _, _) = await CreateAdminClientAsync();

        // 1. Upload an image via media endpoint
        var token = await Token(admin, "/admin/media");
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(token), "__RequestVerificationToken");
        var fileContent = new ByteArrayContent(ValidJpegBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        form.Add(fileContent, "File", "dish-presentation.jpg");
        var uploadRes = await admin.PostAsync("/admin/media/upload", form);
        Assert.Equal(HttpStatusCode.Redirect, uploadRes.StatusCode);

        // Extract uploaded filename from media gallery
        var galleryHtml = await admin.GetStringAsync("/admin/media");
        var match = Regex.Match(galleryHtml, "dish-presentation_[a-f0-9]+\\.jpg");
        Assert.True(match.Success, "Expected uploaded unique filename in gallery HTML.");
        var uploadedFileName = match.Value;
        var imageUrl = $"/uploads/editorial/{uploadedFileName}";

        // 2. Create a published recipe referencing this image
        var recipeToken = await Token(admin, "/admin/content/recipes/new");
        var recipeTitle = "Crispy Duck with Cherry Gastrique " + Guid.NewGuid().ToString("N")[..6];
        var recipeValues = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = recipeToken,
            ["Title"] = recipeTitle,
            ["Summary"] = "A magnificent masterclass dish showcasing crispy skin and savory jus.",
            ["ImageUrl"] = imageUrl,
            ["Servings"] = "4",
            ["PrepMinutes"] = "25",
            ["CookMinutes"] = "40",
            ["IngredientsText"] = "2 duck breasts\n1 cup fresh cherries\n2 tbsp aged vinegar",
            ["StepsText"] = "Score the skin evenly.\nSear gently skin side down.\nRest and slice.",
            ["Visibility"] = ContentVisibility.Free.ToString(),
            ["PublicationStatus"] = PublicationStatus.Published.ToString()
        };

        var saveRes = await admin.PostAsync("/admin/content/recipes/new", new FormUrlEncodedContent(recipeValues));
        Assert.Equal(HttpStatusCode.Redirect, saveRes.StatusCode);

        // 3. Verify public recipe renders the image tag
        using var guest = fixture.NewClient();
        var catalogHtml = await guest.GetStringAsync("/recipes");
        Assert.Contains(recipeTitle, catalogHtml);
        Assert.Contains(imageUrl, catalogHtml);

        await using var db = fixture.CreateDb();
        var item = await db.ContentItems.FirstOrDefaultAsync(x => x.Title == recipeTitle);
        Assert.NotNull(item);
        Assert.Equal(imageUrl, item.ImageUrl);

        var detailHtml = await guest.GetStringAsync($"/recipes/{item.Slug}");
        Assert.Contains(imageUrl, detailHtml);
    }

    [Fact]
    public async Task Recipe_RemovingImageReference_DoesNotBreakPublicPages()
    {
        var (admin, _, _) = await CreateAdminClientAsync();

        // 1. Create a recipe with an image
        var initialImageUrl = "/uploads/editorial/temp-recipe-asset.jpg";
        var recipeToken = await Token(admin, "/admin/content/recipes/new");
        var recipeTitle = "Classic French Omelette " + Guid.NewGuid().ToString("N")[..6];
        var recipeValues = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = recipeToken,
            ["Title"] = recipeTitle,
            ["Summary"] = "Silky, custardy French-style rolled omelette with fresh fines herbes.",
            ["ImageUrl"] = initialImageUrl,
            ["Servings"] = "2",
            ["PrepMinutes"] = "5",
            ["CookMinutes"] = "5",
            ["IngredientsText"] = "3 fresh eggs\n1 tbsp cultured butter\nChives, minced",
            ["StepsText"] = "Beat eggs until uniform.\nVigorously stir in foaming butter.\nRoll and plate.",
            ["Visibility"] = ContentVisibility.Free.ToString(),
            ["PublicationStatus"] = PublicationStatus.Published.ToString()
        };

        var saveRes = await admin.PostAsync("/admin/content/recipes/new", new FormUrlEncodedContent(recipeValues));
        Assert.Equal(HttpStatusCode.Redirect, saveRes.StatusCode);

        await using var db = fixture.CreateDb();
        var item = await db.ContentItems.FirstOrDefaultAsync(x => x.Title == recipeTitle);
        Assert.NotNull(item);
        Assert.Equal(initialImageUrl, item.ImageUrl);

        // 2. Edit recipe to REMOVE the image (RemoveImage = true, ImageUrl = "")
        var editToken = await Token(admin, $"/admin/content/recipes/{item.Id}/edit");
        var editValues = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = editToken,
            ["Id"] = item.Id.ToString(),
            ["Title"] = recipeTitle,
            ["Slug"] = item.Slug,
            ["Summary"] = item.Summary,
            ["ImageUrl"] = "",
            ["RemoveImage"] = "true",
            ["Servings"] = "2",
            ["PrepMinutes"] = "5",
            ["CookMinutes"] = "5",
            ["IngredientsText"] = "3 fresh eggs\n1 tbsp cultured butter\nChives, minced",
            ["StepsText"] = "Beat eggs until uniform.\nVigorously stir in foaming butter.\nRoll and plate.",
            ["Visibility"] = ContentVisibility.Free.ToString(),
            ["PublicationStatus"] = PublicationStatus.Published.ToString()
        };

        var editRes = await admin.PostAsync($"/admin/content/recipes/{item.Id}/edit", new FormUrlEncodedContent(editValues));
        Assert.Equal(HttpStatusCode.Redirect, editRes.StatusCode);

        // 3. Verify in DB ImageUrl is null
        await using var db2 = fixture.CreateDb();
        var updatedItem = await db2.ContentItems.FirstOrDefaultAsync(x => x.Id == item.Id);
        Assert.NotNull(updatedItem);
        Assert.Null(updatedItem.ImageUrl);

        // 4. Verify public page renders HTTP 200 without broken image tag
        using var guest = fixture.NewClient();
        var publicDetailRes = await guest.GetAsync($"/recipes/{item.Slug}");
        Assert.Equal(HttpStatusCode.OK, publicDetailRes.StatusCode);
        var detailHtml = await publicDetailRes.Content.ReadAsStringAsync();
        Assert.Contains(recipeTitle, detailHtml);
        Assert.DoesNotContain("<img src=\"\" ", detailHtml);
        Assert.DoesNotContain(initialImageUrl, detailHtml);
    }

    [Fact]
    public async Task SafeDeletionPolicy_UnlinksReferencedContentItems_WhenImageDeleted()
    {
        var (admin, _, _) = await CreateAdminClientAsync();

        // 1. Upload valid image
        var token = await Token(admin, "/admin/media");
        var form = new MultipartFormDataContent();
        form.Add(new StringContent(token), "__RequestVerificationToken");
        var fileContent = new ByteArrayContent(ValidPngBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/png");
        form.Add(fileContent, "File", "stock-sauce.png");
        await admin.PostAsync("/admin/media/upload", form);

        var galleryHtml = await admin.GetStringAsync("/admin/media");
        var match = Regex.Match(galleryHtml, "stock-sauce_[a-f0-9]+\\.png");
        Assert.True(match.Success, "Expected uploaded filename in media gallery.");
        var fileName = match.Value;
        var imageUrl = $"/uploads/editorial/{fileName}";

        // 2. Create Tip referencing this image
        var tipToken = await Token(admin, "/admin/content/tips/new");
        var tipTitle = "Deglazing Pan Fond " + Guid.NewGuid().ToString("N")[..6];
        var tipValues = new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = tipToken,
            ["Title"] = tipTitle,
            ["Summary"] = "How to unlock deep caramelization from the bottom of your pan using wine.",
            ["ImageUrl"] = imageUrl,
            ["Body"] = "Deglazing dissolves browned food residue from the bottom of the pan to create a flavorful sauce base.",
            ["Visibility"] = ContentVisibility.Free.ToString(),
            ["PublicationStatus"] = PublicationStatus.Published.ToString()
        };
        await admin.PostAsync("/admin/content/tips/new", new FormUrlEncodedContent(tipValues));

        await using var db = fixture.CreateDb();
        var tipItem = await db.ContentItems.FirstOrDefaultAsync(x => x.Title == tipTitle);
        Assert.NotNull(tipItem);
        Assert.Equal(imageUrl, tipItem.ImageUrl);

        // 3. Admin deletes the media file from /admin/media
        var deleteToken = await Token(admin, "/admin/media");
        var deleteContent = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = deleteToken,
            ["fileName"] = fileName
        });
        var deleteRes = await admin.PostAsync("/admin/media/delete", deleteContent);
        Assert.Equal(HttpStatusCode.Redirect, deleteRes.StatusCode);

        // Gallery shows safe removal message
        var postDeleteGallery = await admin.GetStringAsync("/admin/media");
        Assert.Contains("safely removed", postDeleteGallery);

        // 4. Verify in DB that tipItem.ImageUrl was unlinked (set to null)
        await using var db2 = fixture.CreateDb();
        var tipAfterDelete = await db2.ContentItems.FirstOrDefaultAsync(x => x.Id == tipItem.Id);
        Assert.NotNull(tipAfterDelete);
        Assert.Null(tipAfterDelete.ImageUrl);

        // 5. Verify public detail page renders without error
        using var guest = fixture.NewClient();
        var publicTipRes = await guest.GetAsync($"/tips/{tipItem.Slug}");
        Assert.Equal(HttpStatusCode.OK, publicTipRes.StatusCode);
        var tipHtml = await publicTipRes.Content.ReadAsStringAsync();
        Assert.Contains(tipTitle, tipHtml);
        Assert.DoesNotContain(imageUrl, tipHtml);
    }
}
