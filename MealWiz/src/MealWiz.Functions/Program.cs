using MealWiz.Functions;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices(services => services.AddMealWizFunctions())
    .Build();

host.Run();
