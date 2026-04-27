using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using RestauranteMVP.Core;

namespace RestauranteMVP.UI
{
    public class HistoryDialog : Form
    {
        private DataGridView grid;
        private Label lblTitle;
        private Button btnClose;

        public HistoryDialog(List<Comanda> comandasFechadas)
        {
            Text = "Histórico de Comandas";
            Width = 900;
            Height = 600;
            StartPosition = FormStartPosition.CenterParent;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            
            // Tema Escuro
            BackColor = Color.FromArgb(0x0f, 0x17, 0x2a);
            ForeColor = Color.Gainsboro;

            BuildUI(comandasFechadas);
        }

        private void BuildUI(List<Comanda> lista)
        {
            // Título
            lblTitle = new Label
            {
                Text = $"Comandas Pagas / Fechadas ({lista.Count})",
                Font = new Font("Segoe UI", 16, FontStyle.Bold),
                ForeColor = Color.White,
                Dock = DockStyle.Top,
                Height = 50,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(10,0,0,0)
            };
            Controls.Add(lblTitle);

            // Botão Fechar (Embaixo)
            var bottomPanel = new Panel { Dock = DockStyle.Bottom, Height = 60, Padding = new Padding(10) };
            btnClose = new Button
            {
                Text = "Fechar",
                Dock = DockStyle.Right,
                Width = 120,
                FlatStyle = FlatStyle.Flat,
                BackColor = Color.FromArgb(31, 41, 55),
                ForeColor = Color.White
            };
            btnClose.Click += (_, __) => Close();
            bottomPanel.Controls.Add(btnClose);
            Controls.Add(bottomPanel);

            // Grid (Tabela)
            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                BackgroundColor = Color.FromArgb(17, 24, 39),
                BorderStyle = BorderStyle.None,
                EnableHeadersVisualStyles = false,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill
            };

            // Estilos da Tabela (Dark)
            grid.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(31, 41, 55);
            grid.ColumnHeadersDefaultCellStyle.ForeColor = Color.White;
            grid.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 10, FontStyle.Bold);
            grid.ColumnHeadersHeight = 40;

            grid.DefaultCellStyle.BackColor = Color.FromArgb(17, 24, 39);
            grid.DefaultCellStyle.ForeColor = Color.Gainsboro;
            grid.DefaultCellStyle.SelectionBackColor = Color.FromArgb(37, 99, 235);
            grid.DefaultCellStyle.SelectionForeColor = Color.White;
            grid.DefaultCellStyle.Font = new Font("Segoe UI", 10);
            grid.RowTemplate.Height = 35;

            // Colunas
            grid.Columns.Add("id", "ID");
            grid.Columns.Add("mesa", "Mesa / Cliente");
            grid.Columns.Add("abertura", "Abertura");
            grid.Columns.Add("fechamento", "Fechamento");
            grid.Columns.Add("total", "Total R$");
            grid.Columns.Add("status", "Status");

            // Preencher dados
            foreach (var c in lista)
            {
                grid.Rows.Add(
                    $"#{c.Id}",
                    c.TableName,
                    c.CreatedAt.ToString("dd/MM HH:mm"),
                    c.ClosedAt?.ToString("dd/MM HH:mm") ?? "-",
                    c.Totals.Total.ToString("C"),
                    c.Status.ToUpper()
                );
            }

            Controls.Add(grid);
            grid.BringToFront(); // Garante que a grid fique no meio
        }
    }
}