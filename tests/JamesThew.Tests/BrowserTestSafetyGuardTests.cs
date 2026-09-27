using Xunit;

namespace JamesThew.Tests;

public class BrowserTestSafetyGuardTests
{
    [Fact]
    public void AssertSafeTestDatabase_ValidMatchingRunId_Passes()
    {
        var runId = "a1b2c3d4";
        var dbName = $"JamesThew_BrowserQA_{runId}";

        // Should not throw
        BrowserTestServer.AssertSafeTestDatabase(dbName, runId);
    }

    [Theory]
    [InlineData("JamesThew_Development")]
    [InlineData("JamesThew")]
    [InlineData("master")]
    [InlineData("model")]
    [InlineData("msdb")]
    [InlineData("tempdb")]
    public void AssertSafeTestDatabase_ProtectedDatabases_ThrowsInvalidOperationException(string protectedDb)
    {
        var runId = "a1b2c3d4";

        var ex = Assert.Throws<InvalidOperationException>(() =>
            BrowserTestServer.AssertSafeTestDatabase(protectedDb, runId));

        Assert.Contains("Refusing to delete", ex.Message);
    }

    [Fact]
    public void AssertSafeTestDatabase_MismatchedRunId_ThrowsInvalidOperationException()
    {
        var runId = "a1b2c3d4";
        var wrongDb = "JamesThew_BrowserQA_99999999";

        var ex = Assert.Throws<InvalidOperationException>(() =>
            BrowserTestServer.AssertSafeTestDatabase(wrongDb, runId));

        Assert.Contains("does not match the expected ephemeral test database name", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AssertSafeTestDatabase_NullOrEmpty_ThrowsInvalidOperationException(string? invalidDb)
    {
        Assert.Throws<InvalidOperationException>(() =>
            BrowserTestServer.AssertSafeTestDatabase(invalidDb!, "a1b2c3d4"));
    }

    [Fact]
    public void AssertSafeTestUploadDirectory_ValidMatchingTempDir_Passes()
    {
        var runId = "e5f6g7h8";
        var validPath = Path.Combine(Path.GetTempPath(), $"JamesThew_QA_Uploads_{runId}");

        // Should not throw
        BrowserTestServer.AssertSafeTestUploadDirectory(validPath, runId);
    }

    [Theory]
    [InlineData(@"c:\Users\ADVANCES  PC\source\repos\JamesThew\JamesThew\wwwroot\uploads\editorial")]
    [InlineData(@"c:\repos\JamesThew\JamesThew_QA_Uploads_e5f6g7h8")]
    [InlineData(@"c:\Windows\Temp\OtherApp_Uploads_e5f6g7h8")]
    public void AssertSafeTestUploadDirectory_OutsideExpectedTemp_ThrowsInvalidOperationException(string invalidDir)
    {
        var runId = "e5f6g7h8";

        Assert.Throws<InvalidOperationException>(() =>
            BrowserTestServer.AssertSafeTestUploadDirectory(invalidDir, runId));
    }

    [Fact]
    public void AssertSafeTestUploadDirectory_MismatchedRunId_ThrowsInvalidOperationException()
    {
        var runId = "e5f6g7h8";
        var mismatchedDir = Path.Combine(Path.GetTempPath(), "JamesThew_QA_Uploads_00000000");

        var ex = Assert.Throws<InvalidOperationException>(() =>
            BrowserTestServer.AssertSafeTestUploadDirectory(mismatchedDir, runId));

        Assert.Contains("does not match expected test pattern", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AssertSafeTestUploadDirectory_NullOrEmpty_ThrowsInvalidOperationException(string? invalidDir)
    {
        Assert.Throws<InvalidOperationException>(() =>
            BrowserTestServer.AssertSafeTestUploadDirectory(invalidDir!, "e5f6g7h8"));
    }
}
