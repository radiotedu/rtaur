using System.Windows.Forms;
using RadioTEDU.AutoReset.Forms;

namespace RadioTEDU.AutoReset;

internal static class Program
{
    [STAThread]
    static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.Run(new MainDashboardForm());
    }
}
