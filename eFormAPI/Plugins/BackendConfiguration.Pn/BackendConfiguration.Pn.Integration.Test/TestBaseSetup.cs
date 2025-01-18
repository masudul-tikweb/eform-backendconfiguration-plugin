using BackendConfiguration.Pn.Services.RebusService;
using eFormCore;
using Microsoft.EntityFrameworkCore;
using Microting.eForm.Infrastructure;
using Microting.EformBackendConfigurationBase.Infrastructure.Data;
using Microting.eFormCaseTemplateBase.Infrastructure.Data;
using Microting.ItemsPlanningBase.Infrastructure.Data;
using Microting.TimePlanningBase.Infrastructure.Data;
using Rebus.Bus;
using Testcontainers.MariaDb;

namespace BackendConfiguration.Pn.Integration.Test;

public class TestBaseSetup
{
    private readonly MariaDbContainer _mariadbTestcontainer = new MariaDbBuilder()
        .WithDatabase(
            "myDb").WithUsername("bla").WithPassword("secretpassword")
        .WithEnvironment("MYSQL_ROOT_PASSWORD", "Qq1234567$")
        .Build();

    protected MicrotingDbContext? DbContext;

    protected BackendConfigurationPnDbContext? BackendConfigurationPnDbContext;
    protected ItemsPlanningPnDbContext? ItemsPlanningPnDbContext;
    protected TimePlanningPnDbContext? TimePlanningPnDbContext;
    protected MicrotingDbContext? MicrotingDbContext;
    protected CaseTemplatePnDbContext? CaseTemplatePnDbContext;
    protected IBus? Bus;

    private BackendConfigurationPnDbContext GetBackendDbContext(string connectionStr)
    {
        var optionsBuilder = new DbContextOptionsBuilder<BackendConfigurationPnDbContext>();

        optionsBuilder.UseMySql(
            connectionStr.Replace("myDb", "420_eform-backend-configuration-plugin").Replace("bla", "root"),
            new MariaDbServerVersion(
                ServerVersion.AutoDetect(connectionStr)),
            mySqlOptionsAction: builder => {
                builder.EnableRetryOnFailure();
                builder.TranslateParameterizedCollectionsToConstants();
            });

        var backendConfigurationPnDbContext = new BackendConfigurationPnDbContext(optionsBuilder.Options);
        var file = Path.Combine("SQL", "420_eform-backend-configuration-plugin.sql");
        var rawSql = File.ReadAllText(file);

        try
        {
            backendConfigurationPnDbContext.Database.EnsureCreated();
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }

        backendConfigurationPnDbContext.Database.ExecuteSqlRaw(rawSql);

        return backendConfigurationPnDbContext;
    }

    private ItemsPlanningPnDbContext GetItemsPlanningPnDbContext(string connectionStr)
    {
        var optionsBuilder = new DbContextOptionsBuilder<ItemsPlanningPnDbContext>();

        optionsBuilder.UseMySql(
            connectionStr.Replace("myDb", "420_eform-angular-items-planning-plugin").Replace("bla", "root"),
            new MariaDbServerVersion(
                ServerVersion.AutoDetect(connectionStr)),
            mySqlOptionsAction: builder => {
                builder.EnableRetryOnFailure();
                builder.TranslateParameterizedCollectionsToConstants();
            });

        var backendConfigurationPnDbContext = new ItemsPlanningPnDbContext(optionsBuilder.Options);
        var file = Path.Combine("SQL", "420_eform-angular-items-planning-plugin.sql");
        var rawSql = File.ReadAllText(file);

        backendConfigurationPnDbContext.Database.EnsureCreated();
        backendConfigurationPnDbContext.Database.ExecuteSqlRaw(rawSql);

        return backendConfigurationPnDbContext;
    }

    private TimePlanningPnDbContext GetTimePlanningPnDbContext(string connectionStr)
    {
        var optionsBuilder = new DbContextOptionsBuilder<TimePlanningPnDbContext>();

        optionsBuilder.UseMySql(
            connectionStr.Replace("myDb", "420_eform-angular-items-planning-plugin").Replace("bla", "root"),
            new MariaDbServerVersion(
                ServerVersion.AutoDetect(connectionStr)),
            mySqlOptionsAction: builder => {
                builder.EnableRetryOnFailure();
                builder.TranslateParameterizedCollectionsToConstants();
            });

        var backendConfigurationPnDbContext = new TimePlanningPnDbContext(optionsBuilder.Options);
        var file = Path.Combine("SQL", "420_eform-angular-time-planning-plugin.sql");
        var rawSql = File.ReadAllText(file);

        backendConfigurationPnDbContext.Database.EnsureCreated();
        backendConfigurationPnDbContext.Database.ExecuteSqlRaw(rawSql);

        return backendConfigurationPnDbContext;
    }

    private CaseTemplatePnDbContext GetCaseTemplatePnDbContext(string connectionStr)
    {
        var optionsBuilder = new DbContextOptionsBuilder<CaseTemplatePnDbContext>();

        optionsBuilder.UseMySql(
            connectionStr.Replace("myDb", "420_eform-angular-case-template-plugin").Replace("bla", "root"),
            new MariaDbServerVersion(
                ServerVersion.AutoDetect(connectionStr)),
            mySqlOptionsAction: builder => {
                builder.EnableRetryOnFailure();
                builder.TranslateParameterizedCollectionsToConstants();
            });

        var backendConfigurationPnDbContext = new CaseTemplatePnDbContext(optionsBuilder.Options);
        var file = Path.Combine("SQL", "420_eform-angular-case-template-plugin.sql");
        var rawSql = File.ReadAllText(file);

        backendConfigurationPnDbContext.Database.EnsureCreated();
        backendConfigurationPnDbContext.Database.ExecuteSqlRaw(rawSql);

        return backendConfigurationPnDbContext;
    }

    private MicrotingDbContext GetContext(string connectionStr)
    {
        var dbContextOptionsBuilder = new DbContextOptionsBuilder();

        dbContextOptionsBuilder.UseMySql(connectionStr.Replace("myDb", "420_SDK").Replace("bla", "root")
            , new MariaDbServerVersion(
                ServerVersion.AutoDetect(connectionStr)),
            mySqlOptionsAction: builder => {
                builder.EnableRetryOnFailure();
                builder.TranslateParameterizedCollectionsToConstants();
            });
        var microtingDbContext = new MicrotingDbContext(dbContextOptionsBuilder.Options);
        var file = Path.Combine("SQL", "420_SDK.sql");
        var rawSql = File.ReadAllText(file);

        microtingDbContext.Database.EnsureCreated();
        microtingDbContext.Database.ExecuteSqlRaw(rawSql);

        return microtingDbContext;
    }

    protected async Task<Core> GetCore()
    {
        var core = new Core();
        await core.StartSqlOnly(_mariadbTestcontainer.GetConnectionString().Replace("myDb", "420_SDK")
            .Replace("bla", "root"));
        return core;
    }

    [SetUp]
    public async Task Setup()
    {
        Console.WriteLine($"{DateTime.Now} : Starting MariaDb Container...");
        await _mariadbTestcontainer.StartAsync();
        Console.WriteLine($"{DateTime.Now} : Started MariaDb Container");

        BackendConfigurationPnDbContext = GetBackendDbContext(_mariadbTestcontainer.GetConnectionString());

        BackendConfigurationPnDbContext!.Database.SetCommandTimeout(300);

        ItemsPlanningPnDbContext = GetItemsPlanningPnDbContext(_mariadbTestcontainer.GetConnectionString());

        ItemsPlanningPnDbContext.Database.SetCommandTimeout(300);

        TimePlanningPnDbContext = GetTimePlanningPnDbContext(_mariadbTestcontainer.GetConnectionString());

        TimePlanningPnDbContext.Database.SetCommandTimeout(300);

        MicrotingDbContext = GetContext(_mariadbTestcontainer.GetConnectionString());

        MicrotingDbContext.Database.SetCommandTimeout(300);

        CaseTemplatePnDbContext = GetCaseTemplatePnDbContext(_mariadbTestcontainer.GetConnectionString());

        CaseTemplatePnDbContext.Database.SetCommandTimeout(300);

        var rebusService =
            new RebusService(
                new EFormCoreService(_mariadbTestcontainer.GetConnectionString().Replace("myDb", "420_SDK")
                    .Replace("bla", "root")), new BackendConfigurationLocalizationService());
        rebusService
            .Start(_mariadbTestcontainer.GetConnectionString().Replace("myDb", "420_SDK").Replace("bla", "root"))
            .GetAwaiter().GetResult();
        Bus = rebusService.GetBus();
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        Console.WriteLine($"{DateTime.Now} : Stopping MariaDb Container...");
        await _mariadbTestcontainer.StopAsync();
        await _mariadbTestcontainer.DisposeAsync();
        Console.WriteLine($"{DateTime.Now} : Stopped MariaDb Container");
    }

    [TearDown]
    public async Task TearDown()
    {
        await BackendConfigurationPnDbContext!.DisposeAsync();
        await ItemsPlanningPnDbContext!.DisposeAsync();
        await TimePlanningPnDbContext!.DisposeAsync();
        await MicrotingDbContext!.DisposeAsync();
        await CaseTemplatePnDbContext!.DisposeAsync();
        if (Bus != null) Bus.Dispose();
    }
}