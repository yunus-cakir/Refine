namespace Refine.App;

public partial class App : Application
{
    private readonly MainPage _mainPage;

    // Constructor Injection ile MainPage'i alıyoruz
    public App(MainPage mainPage)
    {
        InitializeComponent();
        _mainPage = mainPage;
    }
 
    protected override Window CreateWindow(IActivationState? activationState)
    {
        return new Window(_mainPage);
    }
}