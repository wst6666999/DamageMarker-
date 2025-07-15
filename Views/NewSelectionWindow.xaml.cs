using System.Windows;

namespace DamageMarker.Views
{
    public partial class NewSelectionWindow : Window
    {

        Thickness thickness;
        public NewSelectionWindow()
        {

            thickness = new Thickness();
            InitializeComponent();
        }
        void Window_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            App.mouseStartX = e.GetPosition(this).X;
            App.mouseStartY = e.GetPosition(this).Y;
            selectionBorder.Width = 0;
            selectionBorder.Height = 0;
            selectionBorder.Visibility = Visibility.Visible;
        }
        void Window_MouseUp(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            App.mouseEndX = e.GetPosition(this).X;
            App.mouseEndY = e.GetPosition(this).Y;
            Close();
        }
        void Window_MouseMove(object sender, System.Windows.Input.MouseEventArgs e)
        {
            thickness.Left = (e.GetPosition(this).X >= App.mouseStartX) ? App.mouseStartX : e.GetPosition(this).X;
            thickness.Top = (e.GetPosition(this).Y >= App.mouseStartY) ? App.mouseStartY : e.GetPosition(this).Y;
            selectionBorder.Margin = thickness;
            selectionBorder.Width = Math.Abs(e.GetPosition(this).X - App.mouseStartX);
            selectionBorder.Height = Math.Abs(e.GetPosition(this).Y - App.mouseStartY);
        }

    }
}
