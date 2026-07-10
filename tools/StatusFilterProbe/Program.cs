using kingsightapi.Services;

using Microsoft.Extensions.Configuration;

using Microsoft.Extensions.Logging.Abstractions;

using Microsoft.Extensions.Options;

using kingsightapi.Configuration;



var config = new ConfigurationBuilder()

    .SetBasePath(@"c:\Code\kingsightapi")

    .AddJsonFile("appsettings.development..json", optional: true)

    .Build();



var tables = new FabricWarehouseTables(Options.Create(new FabricWarehouseOptions

{

    Database = config["FabricWarehouse:Database"] ?? "wh_gold",

    SubjectiveInputDatabase = config["FabricWarehouse:SubjectiveInputDatabase"] ?? "wh_gold1",

}));



var statuses = new[] { "0" };

var noopNotifications = new NoopNotificationService();



await Probe("DefaultDateCapture", async () =>

    (await new DefaultDateCaptureService(config, NullLogger<DefaultDateCaptureService>.Instance, tables, noopNotifications)

        .GetAsync(null, statuses)).Count);



await Probe("OtherCostCapture", async () =>

    (await new OtherCostCaptureService(config, NullLogger<OtherCostCaptureService>.Instance, tables)

        .GetAsync(null, statuses)).Count);



await Probe("DefaultSubjectiveAnalytics", async () =>

    (await new DefaultSubjectiveAnalyticsService(config, NullLogger<DefaultSubjectiveAnalyticsService>.Instance, tables)

        .GetAsync(null, statuses)).Count);



await Probe("LtvValidation", async () =>

    (await new LtvValidationService(config, NullLogger<LtvValidationService>.Instance, tables, noopNotifications)

        .GetAsync([], statuses)).Count);



await Probe("TaxArrears", async () =>

    (await new TaxArrearsService(config, NullLogger<TaxArrearsService>.Instance, tables)

        .GetAsync(null, statuses)).Count);



await Probe("LoanSecurityValue", async () =>

    (await new LoanSecurityValueService(config, NullLogger<LoanSecurityValueService>.Instance, tables)

        .GetAllAsync(null, statuses)).Count);



static async Task Probe(string name, Func<Task<int>> run)

{

    try

    {

        var count = await run();

        Console.WriteLine($"{name} statuses=0: {count}");

    }

    catch (Exception ex)

    {

        Console.WriteLine($"{name} ERROR: {ex.Message}");

        if (ex.InnerException is not null)

        {

            Console.WriteLine($"  INNER: {ex.InnerException.Message}");

        }

    }

}



sealed class NoopNotificationService : INotificationService

{

    public Task<IReadOnlyList<kingsightapi.Entities.NotificationDto>> GetAllAsync(

        CancellationToken cancellationToken = default) =>

        Task.FromResult<IReadOnlyList<kingsightapi.Entities.NotificationDto>>([]);

    public Task<bool> MarkAsReadAsync(

        IReadOnlyList<long> notificationIds, CancellationToken cancellationToken = default) =>

        Task.FromResult(false);

    public Task<int> MarkAllAsReadAsync(CancellationToken cancellationToken = default) =>

        Task.FromResult(0);

    public Task CreateRankingUpdateAsync(

        string loanCode, short? priorRanking, short? currentRanking, string updatedBy,

        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task CreateDefaultDateUpdateAsync(

        string loanCode, DateTime? priorDefaultDate, DateTime? currentDefaultDate, string updatedBy,

        CancellationToken cancellationToken = default) => Task.CompletedTask;

    public Task CreateLtvReviewedAsync(string updatedBy, CancellationToken cancellationToken = default) =>

        Task.CompletedTask;

}


