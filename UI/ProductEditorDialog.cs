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
    public class ProductEditorDialog : Form
    {
        public Product? ResultProduct { get; private set; }
        public List<RecipeComponent> ResultRecipe { get; private set; } = new();

        private TextBox txtName = new();
        private NumericUpDown numPrice = new();
        private ComboBox cboCategory = new();
        private TextBox txtUnit = new(); // Unidade (un, kg, lt)
        private NumericUpDown numStock = new(); // Estoque Inicial

        // Parte da Receita
        private TextBox txtSearchIng = new();
        private ListBox listIngredients = new();
        private NumericUpDown numQtyIng = new();
        private DataGridView gridRecipe = new();
        private Button btnAddIng = new();
        private Button btnRemIng = new();

        private bool _isIngredientMode = false;

        public ProductEditorDialog(Product? existing = null)
        {
            Text = existing == null ? "Cadastrar Produto" : $"Editar: {existing.Name}";
            Size = new Size(900, 600);
            StartPosition = FormStartPosition.CenterParent;
            BackColor = Color.FromArgb(15, 23, 42);
            ForeColor = Color.White;
            Font = new Font("Segoe UI", 10F, FontStyle.Regular);

            BuildLayout();

            if (existing != null) LoadData(existing);
        }

        public void PresetAsIngredient()
        {
            _isIngredientMode = true;
            cboCategory.Text = "Ingredientes";
            cboCategory.Enabled = false;
            numPrice.Value = 0;
            numPrice.Enabled = false;
            Text = "Cadastrar Ingrediente";
            
            // Esconde aba de receita pois ingrediente não costuma ter receita
            // (Simplesmente desabilitamos os controles da direita)
            gridRecipe.Enabled = false;
            txtSearchIng.Enabled = false;
            btnAddIng.Enabled = false;
        }

        private void BuildLayout()
        {
            var main = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 2, Padding = new Padding(15) };
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 40)); // Esquerda (Dados)
            main.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 60)); // Direita (Receita)
            Controls.Add(main);

            // === ESQUERDA: DADOS BÁSICOS ===
            var pnlLeft = new FlowLayoutPanel { Dock = DockStyle.Fill, FlowDirection = FlowDirection.TopDown, AutoSize = true };
            
            pnlLeft.Controls.Add(MkLabel("Nome do Produto:"));
            txtName = new TextBox { Width = 300, BackColor = Color.FromArgb(30, 41, 59), ForeColor = Color.White };
            pnlLeft.Controls.Add(txtName);

            pnlLeft.Controls.Add(MkLabel("Preço de Venda (R$):"));
            numPrice = MkNum(300, 2);
            pnlLeft.Controls.Add(numPrice);

            pnlLeft.Controls.Add(MkLabel("Categoria:"));
            cboCategory = new ComboBox { Width = 300, BackColor = Color.FromArgb(30, 41, 59), ForeColor = Color.White, DropDownStyle = ComboBoxStyle.DropDown };
            cboCategory.Items.AddRange(new[] { "Lanches", "Bebidas", "Porções", "Pratos", "Sobremesas", "Ingredientes", "Outros" });
            pnlLeft.Controls.Add(cboCategory);

            pnlLeft.Controls.Add(MkLabel("Unidade (ex: un, kg, lt):"));
            txtUnit = new TextBox { Width = 100, Text = "un", BackColor = Color.FromArgb(30, 41, 59), ForeColor = Color.White };
            pnlLeft.Controls.Add(txtUnit);

            pnlLeft.Controls.Add(MkLabel("Estoque Inicial (Atual):"));
            numStock = MkNum(150, 0); // Inteiro
            pnlLeft.Controls.Add(numStock);

            main.Controls.Add(pnlLeft, 0, 0);

            // === DIREITA: FICHA TÉCNICA (RECEITA) ===
            var pnlRight = new Panel { Dock = DockStyle.Fill, BackColor = Color.FromArgb(11, 18, 32), Padding = new Padding(10) };
            
            var lblRecipe = MkLabel("Ficha Técnica / Ingredientes (Opcional)");
            lblRecipe.Font = new Font("Segoe UI", 12, FontStyle.Bold);
            lblRecipe.Dock = DockStyle.Top;
            
            // Busca de ingredientes
            var boxSearch = new GroupBox { Text = "Adicionar Ingrediente", Dock = DockStyle.Top, Height = 140, ForeColor = Color.Gray };
            txtSearchIng = new TextBox { Width = 200, PlaceholderText = "Buscar ingrediente...", Top = 25, Left = 10, BackColor = Color.FromArgb(30, 41, 59), ForeColor = Color.White };
            txtSearchIng.TextChanged += (s, e) => RefreshIngredientList();
            
            listIngredients = new ListBox { Width = 200, Height = 80, Top = 55, Left = 10, BackColor = Color.FromArgb(30, 41, 59), ForeColor = Color.White };
            
            var lblQtd = new Label { Text = "Qtd Usada:", Top = 25, Left = 230, AutoSize = true };
            numQtyIng = MkNum(80, 0); numQtyIng.Top = 50; numQtyIng.Left = 230; numQtyIng.Value = 1;

            btnAddIng = new Button { Text = "Adicionar", Top = 90, Left = 230, BackColor = Color.SeaGreen, FlatStyle = FlatStyle.Flat, ForeColor = Color.White, AutoSize = true };
            btnAddIng.Click += AddIngredientToGrid;

            boxSearch.Controls.AddRange(new Control[] { txtSearchIng, listIngredients, lblQtd, numQtyIng, btnAddIng });

            // Grid da Receita (ESTILIZADO PARA TEMA ESCURO)
            gridRecipe = new DataGridView { 
                Dock = DockStyle.Fill, 
                BackgroundColor = Color.FromArgb(30, 41, 59), // Cor do fundo vazio
                BorderStyle = BorderStyle.None,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                AllowUserToAddRows = false,
                EnableHeadersVisualStyles = false, // Permite customizar o cabeçalho
                SelectionMode = DataGridViewSelectionMode.FullRowSelect // Seleciona a linha toda
            };

            // 1. Estilo do Cabeçalho (Topo)
            gridRecipe.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(15, 23, 42); // Azul bem escuro
            gridRecipe.ColumnHeadersDefaultCellStyle.ForeColor = Color.LightGray; // Texto cinza claro
            gridRecipe.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9, FontStyle.Bold);
            gridRecipe.ColumnHeadersHeight = 35;

            // 2. Estilo das Linhas (Onde estão os dados) <--- AQUI RESOLVE O PROBLEMA
            gridRecipe.DefaultCellStyle.BackColor = Color.FromArgb(30, 41, 59); // Fundo azul acinzentado (igual aos campos de texto)
            gridRecipe.DefaultCellStyle.ForeColor = Color.White; // Texto Branco
            gridRecipe.DefaultCellStyle.SelectionBackColor = Color.FromArgb(37, 99, 235); // Azul destaque quando clica
            gridRecipe.DefaultCellStyle.SelectionForeColor = Color.White;

            gridRecipe.Columns.Add("id", "ID");
            gridRecipe.Columns.Add("name", "Ingrediente");
            gridRecipe.Columns.Add("qty", "Qtd");

            // Botão remover
            btnRemIng = new Button { Text = "Remover Selecionado", Dock = DockStyle.Bottom, BackColor = Color.IndianRed, FlatStyle = FlatStyle.Flat, ForeColor = Color.White };
            btnRemIng.Click += (s,e) => {
                if(gridRecipe.CurrentRow != null) gridRecipe.Rows.Remove(gridRecipe.CurrentRow);
            };

            var pnlGridContainer = new Panel { Dock = DockStyle.Fill, Padding = new Padding(0, 10, 0, 10) };
            pnlGridContainer.Controls.Add(gridRecipe);

            pnlRight.Controls.Add(pnlGridContainer);
            pnlRight.Controls.Add(btnRemIng);
            pnlRight.Controls.Add(boxSearch);
            pnlRight.Controls.Add(lblRecipe);

            main.Controls.Add(pnlRight, 1, 0);

            // === RODAPÉ (SALVAR) ===
            var pnlBottom = new FlowLayoutPanel { Dock = DockStyle.Bottom, FlowDirection = FlowDirection.RightToLeft, Height = 50 };
            var btnSave = new Button { Text = "SALVAR PRODUTO", Width = 200, Height = 40, BackColor = Color.FromArgb(37, 99, 235), ForeColor = Color.White, FlatStyle = FlatStyle.Flat, Font = new Font("Segoe UI", 10, FontStyle.Bold) };
            btnSave.Click += SaveData;
            pnlBottom.Controls.Add(btnSave);
            
            Controls.Add(pnlBottom);

            RefreshIngredientList();
        }

        private void LoadData(Product p)
        {
            ResultProduct = p; // Mantém ID original
            txtName.Text = p.Name;
            numPrice.Value = p.Price;
            cboCategory.Text = p.Category;
            txtUnit.Text = p.Unit;
            numStock.Value = p.Stock;

            // Carregar receita
            var existingRecipe = DataStore.Recipes.Where(r => r.ParentProductId == p.Id).ToList();
            foreach(var item in existingRecipe)
            {
                var ingName = DataStore.Products.FirstOrDefault(x => x.Id == item.IngredientProductId)?.Name ?? "???";
                gridRecipe.Rows.Add(item.IngredientProductId, ingName, item.QuantityPerUnit);
            }
        }

        private void RefreshIngredientList()
        {
            listIngredients.Items.Clear();
            var search = txtSearchIng.Text.Trim().ToLower();
            var ings = DataStore.Products
                .Where(p => p.Category == "Ingredientes" || DataStore.Recipes.Any(r => r.IngredientProductId == p.Id))
                .Where(p => string.IsNullOrEmpty(search) || p.Name.ToLower().Contains(search))
                .OrderBy(p => p.Name)
                .ToList();

            if (ings.Count == 0 && !string.IsNullOrEmpty(search)) 
            {
                // Se não achou ingrediente, mostra todos os produtos caso queira usar um produto como ingrediente (ex: Coca na receita de Drink)
                ings = DataStore.Products.Where(p => p.Name.ToLower().Contains(search)).ToList();
            }

            foreach(var i in ings) listIngredients.Items.Add($"{i.Id} - {i.Name}");
        }

        private void AddIngredientToGrid(object? sender, EventArgs e)
        {
            if (listIngredients.SelectedItem == null) return;
            var text = listIngredients.SelectedItem.ToString()!;
            var id = int.Parse(text.Split('-')[0].Trim());
            var name = text.Split('-')[1].Trim();
            var qty = (int)numQtyIng.Value;

            if (qty <= 0) { MessageBox.Show("Quantidade deve ser maior que 0"); return; }

            // Verifica duplicidade visual
            foreach(DataGridViewRow row in gridRecipe.Rows)
            {
                if (int.Parse(row.Cells[0].Value.ToString()!) == id)
                {
                    row.Cells[2].Value = int.Parse(row.Cells[2].Value.ToString()!) + qty;
                    return;
                }
            }

            gridRecipe.Rows.Add(id, name, qty);
        }

        private void SaveData(object? sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(txtName.Text)) { MessageBox.Show("Nome obrigatório"); return; }

            // Cria/Atualiza objeto Produto
            if (ResultProduct == null) ResultProduct = new Product { Id = DataStore.NextProductId() };
            
            ResultProduct.Name = txtName.Text.Trim();
            ResultProduct.Price = numPrice.Value;
            ResultProduct.Category = string.IsNullOrWhiteSpace(cboCategory.Text) ? "Outros" : cboCategory.Text;
            ResultProduct.Unit = txtUnit.Text;
            ResultProduct.Stock = (int)numStock.Value;

            // Monta lista de receita
            ResultRecipe.Clear();
            foreach(DataGridViewRow row in gridRecipe.Rows)
            {
                ResultRecipe.Add(new RecipeComponent
                {
                    ParentProductId = ResultProduct.Id,
                    IngredientProductId = int.Parse(row.Cells[0].Value.ToString()!),
                    QuantityPerUnit = int.Parse(row.Cells[2].Value.ToString()!)
                });
            }

            DialogResult = DialogResult.OK;
            Close();
        }

        private Label MkLabel(string t) => new Label { Text = t, AutoSize = true, ForeColor = Color.LightGray, Margin = new Padding(0, 10, 0, 0) };
        private NumericUpDown MkNum(int w, int decimals) => new NumericUpDown { Width = w, DecimalPlaces = decimals, Maximum = 1000000, BackColor = Color.FromArgb(30, 41, 59), ForeColor = Color.White };
    }
}