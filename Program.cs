using System;
using System.Windows.Forms;
using RestauranteMVP.UI; 

namespace RestauranteMVP
{
    internal static class Program
    {
        [STAThread]
        static void Main()
        {            
            try
            {
                ApplicationConfiguration.Initialize();
                Application.Run(new MainForm());
            }
            catch (Exception ex)
            {
                MessageBox.Show($"O programa falhou ao iniciar.\n\nErro: {ex.Message}", "Erro Fatal", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }
    }
}