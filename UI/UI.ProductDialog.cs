#nullable enable
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using RestauranteMVP.Core;
using RestauranteMVP.Data; 

namespace RestauranteMVP.UI
{
    public class ProductDialog : Form
    {
        // MODO CESTA
        public List<ComandaItem> SelectedItems { get; private set; } = new List<ComandaItem>();

        // MODO COMPATIBILIDADE
        public ProductDialog() { InitializeConfig(); }
        public ProductDialog(object? p1) : this() { }
        public ProductDialog(object? p1, object? p2) : this() { }

        public Product Result 
        { 
            get 
            {
                var item = SelectedItems.LastOrDefault();
                if (item == null) return null;
                return DataStore.Products.FirstOrDefault(p => p.Id == item.ProductId);
            } 
        }

        public void PresetAsIngredient(bool value = true) { }
        public List<RecipeComponent> ResultRecipeComponents { get; set; } = new List<RecipeComponent>(); 

        // Componentes
        private TextBox txtSearchName;
        private TextBox txtQuickCode; 
        private DataGridView gridProducts; 
        private DataGridView gridBasket;   
        private Label lblTotalBasket;

        private void InitializeConfig()
        {
            if (txtSearchName != null) return; 

            Text = "Localizar / Adicionar Produtos";
            Size = new Size(1100, 650);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(15, 23, 42);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10F, FontStyle.Regular);

            BuildLayout();
            LoadProducts();
        }

        private void BuildLayout()
        {
            var mainLayout = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(10) };
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60)); 
            mainLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40)); 
            Controls.Add(mainLayout);

            var leftPanel = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 0, 10, 0) };
            var searchPanel = new FlowLayoutPanel { Dock = DockStyle.Top, Height = 70, FlowDirection = FlowDirection.LeftToRight, AutoSize = false };
            
            var lblCode = new Label { Text = "Cód. Rápido (Enter):", AutoSize = true, ForeColor = Color.Yellow, Font = new Font("Segoe UI", 10, FontStyle.Bold), Margin = new Padding(0, 5, 0, 0) };
            txtQuickCode = new TextBox { Width = 100, Font = new Font("Segoe UI", 14, FontStyle.Bold), BackColor = Color.FromArgb(50, 50, 20), ForeColor = Color.Yellow };
            txtQuickCode.KeyDown += TxtQuickCode_KeyDown; 

            var lblName = new Label { Text = "Buscar Nome:", AutoSize = true, ForeColor = Color.LightGray, Margin = new Padding(20, 5, 0, 0) };
            txtSearchName = new TextBox { Width = 250, Font = new Font("Segoe UI", 12), BackColor = Color.FromArgb(30, 41, 59), ForeColor = Color.White };
            txtSearchName.TextChanged += (s, e) => LoadProducts(txtSearchName.Text);

            searchPanel.Controls.Add(lblCode); searchPanel.Controls.Add(txtQuickCode);
            searchPanel.Controls.Add(lblName); searchPanel.Controls.Add(txtSearchName);

            gridProducts = CreateGrid();
            gridProducts.Columns.Add("id", "Cód");
            gridProducts.Columns.Add("name", "Produto");
            gridProducts.Columns.Add("price", "Preço");
            gridProducts.Columns[0].Width = 60;
            gridProducts.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            gridProducts.Columns[2].Width = 80;
            gridProducts.CellDoubleClick += GridProducts_CellDoubleClick;

            var lblHint = new Label { Text = "Dica: Digite '2*50' para lançar 2x o item 50.", Dock = DockStyle.Bottom, ForeColor = Color.Gray, Font = new Font("Segoe UI", 8) };

            leftPanel.Controls.Add(gridProducts); leftPanel.Controls.Add(searchPanel); leftPanel.Controls.Add(lblHint);
            mainLayout.Controls.Add(leftPanel, 0, 0);

            var rightPanel = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(11, 15, 25), Padding = new Padding(15) };
            var lblBasketTitle = new Label { Text = "🛒 Itens Selecionados", Dock = DockStyle.Top, Font = new Font("Segoe UI", 14, FontStyle.Bold), Height = 40, ForeColor = Color.LightGreen };
            
            gridBasket = CreateGrid();
            gridBasket.Columns.Add("qty", "Qtd");
            gridBasket.Columns.Add("name", "Produto");
            gridBasket.Columns.Add("total", "Total");
            gridBasket.Columns[0].Width = 50;
            gridBasket.Columns[1].AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill;
            gridBasket.Columns[2].Width = 80;
            gridBasket.CellDoubleClick += GridBasket_CellDoubleClick; 

            lblTotalBasket = new Label { Text = "Total: R$ 0,00", Dock = DockStyle.Bottom, Font = new Font("Segoe UI", 16, FontStyle.Bold), ForeColor = Color.Cyan, TextAlign = ContentAlignment.MiddleRight, Height = 50 };

            var btnConfirm = new Button { Text = "CONFIRMAR (F5)", Dock = DockStyle.Bottom, Height = 60, BackColor = Color.FromArgb(34, 197, 94), FlatStyle = FlatStyle.Flat, ForeColor = Color.Black, Font = new Font("Segoe UI", 12, FontStyle.Bold), Cursor = Cursors.Hand };
            btnConfirm.Click += (s, e) => ConfirmAndClose();

            rightPanel.Controls.Add(gridBasket); rightPanel.Controls.Add(lblTotalBasket);
            rightPanel.Controls.Add(btnConfirm); rightPanel.Controls.Add(lblBasketTitle);

            mainLayout.Controls.Add(rightPanel, 1, 0);
        }

        private void TxtQuickCode_KeyDown(object? sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true; 
                string input = txtQuickCode.Text.Trim();
                if (string.IsNullOrEmpty(input)) return;

                int qty = 1; int code = 0;

                if (input.Contains("*")) {
                    var parts = input.Split('*');
                    if (parts.Length == 2 && int.TryParse(parts[0], out int q) && int.TryParse(parts[1], out int c)) {
                        qty = q; code = c;
                    }
                } else {
                    int.TryParse(input, out code);
                }

                if (code > 0) {
                    var product = DataStore.Products.FirstOrDefault(p => p.Id == code);
                    if (product != null) {
                        AddToBasket(product, qty);
                        txtQuickCode.Text = ""; 
                    } else {
                        MessageBox.Show("Produto não encontrado.");
                        txtQuickCode.SelectAll();
                    }
                }
            }
        }

        private void LoadProducts(string search = "")
        {
            gridProducts.Rows.Clear();
            var query = DataStore.Products.AsEnumerable();
            if (!string.IsNullOrEmpty(search))
                query = query.Where(p => p.Name.ToLower().Contains(search.ToLower()) || p.Id.ToString() == search);

            foreach (var p in query)
            {
                int idx = gridProducts.Rows.Add(p.Id, p.Name, p.Price.ToString("N2"));
                gridProducts.Rows[idx].Tag = p;
            }
        }

        private void GridProducts_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            var product = gridProducts.Rows[e.RowIndex].Tag as Product;
            if (product != null) AddToBasket(product, 1);
        }

        private void AddToBasket(Product p, int qty)
        {
            var existingItem = SelectedItems.FirstOrDefault(i => i.ProductId == p.Id);
            if (existingItem != null) existingItem.Qty += qty;
            else SelectedItems.Add(new ComandaItem { ProductId = p.Id, Name = p.Name, Price = p.Price, Qty = qty });
            UpdateBasketGrid();
        }

        private void GridBasket_CellDoubleClick(object? sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0) return;
            SelectedItems.RemoveAt(e.RowIndex);
            UpdateBasketGrid();
        }

        private void UpdateBasketGrid()
        {
            gridBasket.Rows.Clear();
            decimal total = 0;
            foreach(var item in SelectedItems) {
                decimal t = item.Price * item.Qty;
                gridBasket.Rows.Add(item.Qty, item.Name, t.ToString("N2"));
                total += t;
            }
            lblTotalBasket.Text = $"Total: {total:C}";
            if (gridBasket.Rows.Count > 0) gridBasket.FirstDisplayedScrollingRowIndex = gridBasket.Rows.Count - 1;
        }

        private void ConfirmAndClose()
        {
            if (SelectedItems.Count == 0) { MessageBox.Show("Nenhum item."); return; }
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
        {
            if (keyData == Keys.F5) ConfirmAndClose();
            if (keyData == Keys.Escape) Close();
            return base.ProcessCmdKey(ref msg, keyData);
        }

        private DataGridView CreateGrid()
        {
            return new DataGridView {
                Dock = DockStyle.Fill, BackgroundColor = Color.FromArgb(30, 41, 59), BorderStyle = BorderStyle.None,
                AllowUserToAddRows = false, AllowUserToDeleteRows = false, AllowUserToResizeRows = false,
                RowHeadersVisible = false, EnableHeadersVisualStyles = false, SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None, GridColor = Color.FromArgb(51, 65, 85), ReadOnly = true,
                ColumnHeadersDefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(15, 23, 42), ForeColor = Color.LightGray, Font = new Font("Segoe UI", 9, FontStyle.Bold), Alignment = DataGridViewContentAlignment.MiddleLeft },
                DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(30, 41, 59), ForeColor = Color.White, Padding = new Padding(5), SelectionBackColor = Color.FromArgb(59, 130, 246), SelectionForeColor = Color.White },
                RowTemplate = { Height = 35 }
            };
        }
    }
}