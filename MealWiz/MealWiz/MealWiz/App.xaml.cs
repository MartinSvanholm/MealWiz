namespace MealWiz
{
    public partial class App : Application
    {
        public App()
        {
            InitializeComponent();
        }

        protected override Window CreateWindow(IActivationState? activationState)
        {
            var window = new Window(new MainPage()) { Title = "MealWiz" };

#if DEBUG
            if (DeviceInfo.Idiom == DeviceIdiom.Desktop)
            {
                window.Width = 400;
                window.Height = 800;
            }
#endif

            return window;
        }
    }
}
