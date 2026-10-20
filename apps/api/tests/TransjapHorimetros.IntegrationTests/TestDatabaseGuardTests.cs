namespace TransjapHorimetros.IntegrationTests;

using TransjapHorimetros.Infrastructure.Persistence;

public sealed class TestDatabaseGuardTests
{
    private const string SafeTestConn = "Server=127.0.0.1,1433;Database=transjap_horimetros_tests;User Id=sa;Password=secret;Encrypt=False";
    private const string AppConn = "Server=127.0.0.1,1433;Database=transjap_horimetros;User Id=sa;Password=secret;Encrypt=False";
    private const string DangerProdConn = "Server=127.0.0.1,1433;Database=transjap_horimetros;User Id=sa;Password=secret;Encrypt=False";

    [Fact]
    public void EnsureSafeTestDatabase_WhenPointsToRealApplicationDatabase_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            TestDatabaseGuard.EnsureSafeTestDatabase("Testing", DangerProdConn, AppConn));

        Assert.Contains("OPERAÇÃO ABORTADA", ex.Message);
        Assert.Contains(TestDatabaseGuard.ExpectedTestDatabaseName, ex.Message);
    }

    [Fact]
    public void EnsureSafeTestDatabase_WhenPointsToArbitraryDatabase_ThrowsInvalidOperationException()
    {
        const string otherConn = "Server=127.0.0.1,1433;Database=transjap_other;User Id=sa;Encrypt=False";
        var ex = Assert.Throws<InvalidOperationException>(() =>
            TestDatabaseGuard.EnsureSafeTestDatabase("Testing", otherConn, AppConn));

        Assert.Contains("OPERAÇÃO ABORTADA", ex.Message);
        Assert.Contains("transjap_other", ex.Message);
    }

    [Fact]
    public void EnsureSafeTestDatabase_WhenTestAndAppDatabasesAreIdentical_ThrowsInvalidOperationException()
    {
        const string identicalConn = "Server=127.0.0.1,1433;Database=transjap_horimetros_tests;User Id=sa;Encrypt=False";
        var ex = Assert.Throws<InvalidOperationException>(() =>
            TestDatabaseGuard.EnsureSafeTestDatabase("Testing", identicalConn, identicalConn));

        Assert.Contains("OPERAÇÃO ABORTADA", ex.Message);
        Assert.Contains("mesmo banco da aplicação", ex.Message);
    }

    [Fact]
    public void EnsureSafeTestDatabase_WhenValidTestDatabase_SucceedsWithoutException()
    {
        TestDatabaseGuard.EnsureSafeTestDatabase("Testing", SafeTestConn, AppConn);
    }

    [Fact]
    public void EnsureSafeTestDatabase_WhenAzureSqlIsUsed_ThrowsInvalidOperationException()
    {
        const string azureConn = "Server=tcp:example.database.windows.net,1433;Database=transjap_horimetros_tests;User Id=test;Password=secret;Encrypt=True";
        var ex = Assert.Throws<InvalidOperationException>(() =>
            TestDatabaseGuard.EnsureSafeTestDatabase("Testing", azureConn, AppConn));

        Assert.Contains("Azure SQL Database", ex.Message);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EnsureSafeTestDatabase_WhenEmptyString_ThrowsInvalidOperationException(string? emptyConn)
    {
        Assert.Throws<InvalidOperationException>(() =>
            TestDatabaseGuard.EnsureSafeTestDatabase("Testing", emptyConn!, AppConn));
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Production")]
    public void EnsureSafeTestDatabase_WhenEnvironmentIsNotTesting_ThrowsInvalidOperationException(
        string environmentName)
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            TestDatabaseGuard.EnsureSafeTestDatabase(environmentName, SafeTestConn, AppConn));

        Assert.Contains("ambiente Testing", ex.Message);
    }

    [Theory]
    [InlineData("transjap_horimetros")]
    [InlineData("production")]
    [InlineData("master")]
    [InlineData("tempdb")]
    [InlineData("model")]
    [InlineData("msdb")]
    [InlineData("transjap_integration")]
    public void EnsureSafeTestDatabase_WhenDatabaseIsNotTheDedicatedTestDatabase_ThrowsInvalidOperationException(
        string databaseName)
    {
        var connectionString = $"Server=127.0.0.1,1433;Database={databaseName};User Id=sa;Encrypt=False";

        Assert.Throws<InvalidOperationException>(() =>
            TestDatabaseGuard.EnsureSafeTestDatabase("Testing", connectionString, AppConn));
    }
}
