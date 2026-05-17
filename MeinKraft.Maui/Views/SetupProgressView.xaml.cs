namespace MeinKraft.Maui.Views;

public partial class SetupProgressView : ContentView
{
	public SetupProgressView()
	{
		InitializeComponent();
	}

	internal void UpdateProgress(SetupProgressEventArgs e)
	{
		Title.StatusText = e.Title;
	}
}