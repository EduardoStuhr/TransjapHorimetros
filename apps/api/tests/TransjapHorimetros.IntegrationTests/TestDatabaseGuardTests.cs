namespace TransjapHorimetros.IntegrationTests;

public sealed class TestDatabaseGuardTests
{
    private const string SafeTestConn = "Host=127.0.0.1;Port=5432;Database=transjap_horimetros_tests;Username=postgres;Password=secret";
    private const string AppConn = "Host=127.0.0.1;Port=5432;Database=transjap_horimetros;Username=postgres;Password=secret";
    private const string DangerProdConn = "Host=127.0.0.1;Port=5432;Database=transjap_horimetros;Username=postgres;Password=secret";

    [Fact]
    public void EnsureSafeTestDatabase_WhenPointsToRealApplicationDatabase_ThrowsInvalidOperationException()
    {
        var ex = Assert.Throws<InvalidOperationException>(() =>
            TestDatabaseGuard.EnsureSafeTestDatabase(DangerProdConn, AppConn));

        Assert.Contains("OPERAÇÃO ABORTADA", ex.Message);
        Assert.Contains(TestDatabaseGuard.ExpectedTestDatabaseName, ex.Message);
    }

    [Fact]
    public void EnsureSafeTestDatabase_WhenPointsToArbitraryDatabase_ThrowsInvalidOperationException()
    {
        const string otherConn = "Host=127.0.0.1;Port=5432;Database=transjap_other;Username=postgres";
        var ex = Assert.Throws<InvalidOperationException>(() =>
            TestDatabaseGuard.EnsureSafeTestDatabase(otherConn, AppConn));

        Assert.Contains("OPERAÇÃO ABORTADA", ex.Message);
        Assert.Contains("transjap_other", ex.Message);
    }

    [Fact]
    public void EnsureSafeTestDatabase_WhenTestAndAppDatabasesAreIdentical_ThrowsInvalidOperationException()
    {
        const string identicalConn = "Host=127.0.0.1;Port=5432;Database=transjap_horimetros_tests;Username=postgres";
        var ex = Assert.Throws<InvalidOperationException>(() =>
            TestDatabaseGuard.EnsureSafeTestDatabase(identicalConn, identicalConn));

        Assert.Contains("OPERAÇÃO ABORTADA", ex.Message);
        Assert.Contains("mesmo banco da aplicação", ex.Message);
    }

    [Fact]
    public void EnsureSafeTestDatabase_WhenValidTestDatabase_SucceedsWithoutException()
    {
        TestDatabaseGuard.EnsureSafeTestDatabase(SafeTestConn, AppConn);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void EnsureSafeTestDatabase_WhenEmptyString_ThrowsInvalidOperationException(string? emptyConn)
    {
        Assert.Throws<InvalidOperationException>(() =>
            TestDatabaseGuard.EnsureSafeTestDatabase(emptyConn!, AppConn));
    }
}
