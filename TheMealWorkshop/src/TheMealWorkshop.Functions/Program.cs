using TheMealWorkshop.Functions;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services => services.AddTheMealWorkshopFunctions())
    .Build();

host.Run();
